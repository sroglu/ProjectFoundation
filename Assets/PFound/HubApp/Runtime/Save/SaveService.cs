using System;
using System.IO;
using PFound.Utilities.FileSystemTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PFound.HubApp.Save
{
	/// <summary>
	/// Concrete <see cref="ISaveService"/>. Persists <see cref="SaveSchema"/> as JSON to
	/// <c>{saveRoot}/save.json</c> using <see cref="FileTools.AtomicWriteAllText"/> for crash-safe
	/// writes (temp + fsync + atomic rename + rotating .bak) and <see cref="FileTools.SafeReadAllText"/>
	/// for .bak fallback on load.
	/// </summary>
	public class SaveService : ISaveService
	{
		public const string SaveFileName = "save.json";

		/// <summary>Production save root. Tests inject a temp directory via the ctor instead.</summary>
		public static string DefaultSaveRoot => Application.persistentDataPath;

		private readonly string _saveRoot;
		private readonly MigrationPipeline _migrations;

		public SaveSchema Schema { get; private set; }

		public string SavePath => Path.Combine(_saveRoot, SaveFileName);

		public SaveService(string saveRoot, MigrationPipeline migrations = null)
		{
			if (string.IsNullOrEmpty(saveRoot))
				throw new ArgumentException("saveRoot must not be empty.", nameof(saveRoot));

			_saveRoot = saveRoot;
			_migrations = migrations ?? new MigrationPipeline();
		}

		public void Load()
		{
			var text = FileTools.SafeReadAllText(SavePath);
			if (text == null)
			{
				// Documented "first launch" path: no save file (or both main + .bak unreadable
				// at the IO layer). Bring up a fresh in-memory schema; first Save() persists it.
				Schema = new SaveSchema();
				return;
			}

			// JsonReaderException on malformed JSON propagates intentionally — silently falling
			// back to defaults would mask real corruption bugs.
			var root = JObject.Parse(text);

			// Boundary contract check: every save file MUST carry SchemaVersion. If missing,
			// the cast below throws — stack trace pinpoints this line as the violation.
			var savedVersion = (int)root["SchemaVersion"];

			_migrations.Apply(root, savedVersion, SaveSchema.CurrentSchemaVersion);

			Schema = root.ToObject<SaveSchema>();
		}

		public void Save() => Flush();

		public void Flush()
		{
			var json = JsonConvert.SerializeObject(Schema, Formatting.Indented);
			FileTools.AtomicWriteAllText(SavePath, json);
		}

		public void Reset()
		{
			Schema = new SaveSchema();
		}
	}
}
