using System;
using System.Collections.Generic;
using System.IO;

namespace PFound.Utilities.FileSystemTools
{
	/// <summary>
	/// Directory-level helpers built on <c>System.IO</c>: emptiness checks, enumeration, recursive
	/// removal, pruning of empty subtrees, and creation from a file path or as a fresh temp folder.
	/// Engine-free.
	/// </summary>
	public static class DirectoryTools
	{
		// ── Inspection ──────────────────────────────────────────────────────

		/// <summary>
		/// True when the directory exists and holds no files or subdirectories. A missing directory
		/// is treated as empty.
		/// </summary>
		public static bool IsDirectoryEmpty(string path)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
			if (!Directory.Exists(path)) return true;

			// Enumerate lazily so we stop at the first entry instead of materialising the whole listing.
			using (var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator())
			{
				return !entries.MoveNext();
			}
		}

		/// <summary>
		/// Lists file paths inside a directory, optionally recursing into subdirectories and filtering
		/// by a search pattern (e.g. <c>"*.json"</c>). A missing directory yields an empty sequence.
		/// </summary>
		public static IReadOnlyList<string> ListFilesInDirectory(string path, bool recursive = false, string searchPattern = "*")
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
			if (!Directory.Exists(path)) return Array.Empty<string>();

			var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			return new List<string>(Directory.EnumerateFiles(path, searchPattern ?? "*", option));
		}

		// ── Removal ─────────────────────────────────────────────────────────

		/// <summary>
		/// Recursively deletes a directory and everything beneath it. A missing directory is a no-op.
		/// </summary>
		public static void DeleteWithContent(string path)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
			if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
		}

		/// <summary>
		/// Prunes every empty subdirectory beneath <paramref name="path"/> (deepest first). When
		/// <paramref name="deleteRootIfEmpty"/> is set and the root itself ends up empty, it is
		/// removed too. Returns the number of directories deleted.
		/// </summary>
		public static int DeleteEmptySubdirectories(string path, bool deleteRootIfEmpty = false)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
			if (!Directory.Exists(path)) return 0;

			var deleted = 0;
			foreach (var child in Directory.GetDirectories(path))
			{
				deleted += DeleteEmptySubdirectories(child, deleteRootIfEmpty: true);
			}

			if (deleteRootIfEmpty && IsDirectoryEmpty(path))
			{
				Directory.Delete(path, recursive: false);
				deleted++;
			}

			return deleted;
		}

		// ── Creation ────────────────────────────────────────────────────────

		/// <summary>
		/// Ensures the directory that would contain <paramref name="filePath"/> exists, creating it
		/// (and any missing ancestors) when necessary. A file path with no directory component is a
		/// no-op.
		/// </summary>
		public static void CreateFromFilePath(string filePath)
		{
			if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

			var directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);
		}

		/// <summary>
		/// Creates and returns a brand-new, uniquely named directory under the system temp location.
		/// The returned path is guaranteed not to have pre-existed.
		/// </summary>
		public static string CreateTemporaryDirectory()
		{
			while (true)
			{
				var candidate = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
				if (Directory.Exists(candidate) || File.Exists(candidate)) continue;
				Directory.CreateDirectory(candidate);
				return candidate;
			}
		}
	}
}
