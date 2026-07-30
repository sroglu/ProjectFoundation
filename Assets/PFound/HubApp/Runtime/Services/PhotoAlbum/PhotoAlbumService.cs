using System;
using System.Collections.Generic;
using System.IO;
using PFound.HubApp.MiniGame.Signals;
using PFound.HubApp.Save;
using PFound.Signaling;
using UnityEngine;

namespace PFound.HubApp.Services.PhotoAlbum
{
	/// <summary>
	/// Concrete <see cref="IPhotoAlbumService"/>. JPEG quality is fixed at 80 per
	/// HubApp_TDD §5.4.3 — tunable later via ctor if a quality settings UI lands.
	/// </summary>
	public class PhotoAlbumService : IPhotoAlbumService
	{
		public const int DefaultMaxPhotos = 200;
		private const int JpegQuality = 80;
		private const string PhotoIdPrefix = "ph_";

		/// <summary>Production photos root. Tests inject a temp directory via the ctor instead.</summary>
		public static string DefaultPhotosRoot => Path.Combine(Application.persistentDataPath, "photos");

		private readonly ISaveService _save;
		private readonly SignalTracker _signals;
		private readonly string _photosRoot;
		private readonly int _maxPhotos;

		public PhotoAlbumService(ISaveService save, SignalTracker signals, string photosRoot, int maxPhotos = DefaultMaxPhotos)
		{
			if (save == null) throw new ArgumentNullException(nameof(save));
			if (signals == null) throw new ArgumentNullException(nameof(signals));
			if (string.IsNullOrEmpty(photosRoot))
				throw new ArgumentException("photosRoot must not be empty.", nameof(photosRoot));
			if (maxPhotos < 1)
				throw new ArgumentOutOfRangeException(nameof(maxPhotos), "maxPhotos must be at least 1.");

			_save = save;
			_signals = signals;
			_photosRoot = photosRoot;
			_maxPhotos = maxPhotos;
		}

		public int MaxPhotosPerProfile => _maxPhotos;

		public PhotoEntry Capture(string profileId, Texture2D snapshot, string sourceGame, string sceneTag)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));
			if (snapshot == null)
				throw new ArgumentNullException(nameof(snapshot));

			var entry = new PhotoEntry
			{
				Id = GeneratePhotoId(),
				Filename = null, // set below once we know the resolved filename
				SourceGame = sourceGame,
				SceneTag = sceneTag,
				TakenAt = DateTime.UtcNow,
				Width = snapshot.width,
				Height = snapshot.height
			};

			var filename = entry.Id + ".jpg";
			entry.Filename = filename;

			var profileDir = Path.Combine(_photosRoot, profileId);
			Directory.CreateDirectory(profileDir);
			var fullPath = Path.Combine(profileDir, filename);

			// JPEG bytes go straight to disk (no atomic rename — losing one photo on crash is
			// fine; the save.json append happens after, so a partial photo file with no save
			// entry is just an orphan that the next FIFO sweep ignores).
			var jpg = snapshot.EncodeToJPG(JpegQuality);
			File.WriteAllBytes(fullPath, jpg);

			var bag = GetOrCreateShared(profileId).Photos;
			bag.Add(entry);

			EvictOldestIfOverCap(profileId, bag);

			_signals.Queue<PhotoCapturedSignal>(causer: null);
			return entry;
		}

		public IReadOnlyList<PhotoEntry> GetPhotosFor(string profileId)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));

			return _save.Schema.Shared.TryGetValue(profileId, out var shared) ? shared.Photos : Array.Empty<PhotoEntry>();
		}

		public string GetPhotoPath(string profileId, PhotoEntry entry)
		{
			if (string.IsNullOrEmpty(profileId))
				throw new ArgumentException("profileId must not be empty.", nameof(profileId));
			if (entry == null) throw new ArgumentNullException(nameof(entry));
			if (string.IsNullOrEmpty(entry.Filename))
				throw new ArgumentException("entry.Filename must not be empty.", nameof(entry));

			return Path.Combine(_photosRoot, profileId, entry.Filename);
		}

		private SharedProfileData GetOrCreateShared(string profileId)
		{
			if (!_save.Schema.Shared.TryGetValue(profileId, out var shared))
			{
				shared = new SharedProfileData();
				_save.Schema.Shared[profileId] = shared;
			}
			return shared;
		}

		private void EvictOldestIfOverCap(string profileId, List<PhotoEntry> bag)
		{
			while (bag.Count > _maxPhotos)
			{
				var oldest = bag[0];
				bag.RemoveAt(0);

				// Delete file too — keeping orphans on disk would defeat the cap (storage grows
				// unbounded). Tolerate a missing file: the only way to hit this is a previous
				// capture killed mid-write or manual user file deletion — both observable
				// external states, not internal invariant violations. The save entry being
				// stale is the data we care about; removing it is the actual eviction.
				var path = Path.Combine(_photosRoot, profileId, oldest.Filename);
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		private static string GeneratePhotoId()
		{
			// 12 hex chars = 6 bytes of entropy (7.2e13 keyspace). Photo capture is rare-ish
			// (a few per session); collisions are not a real concern at this width.
			return PhotoIdPrefix + Guid.NewGuid().ToString("N").Substring(0, 12);
		}
	}
}
