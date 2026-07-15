using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PFound.Utilities.FileSystemTools
{
	/// <summary>
	/// File-level helpers built on <c>System.IO</c>: human-readable sizes, lock detection,
	/// collision-free naming, crash-safe (atomic) writes with a rotating backup, tolerant reads
	/// that fall back to that backup, and directory-creating copies. Engine-free.
	/// </summary>
	public static class FileTools
	{
		/// <summary>Suffix appended to the previous revision when an atomic write rotates it.</summary>
		public const string BackupSuffix = ".bak";

		/// <summary>Suffix of the scratch file an atomic write streams into before renaming.</summary>
		public const string TempSuffix = ".tmp";

		private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

		private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

		// ── Reporting ───────────────────────────────────────────────────────

		/// <summary>
		/// Formats a byte count as a compact human-readable string using binary (1024) steps, e.g.
		/// <c>1536</c> → <c>"1.5 KB"</c>. Negative inputs are rendered with a leading minus sign.
		/// </summary>
		public static string ToFileSizeString(long byteCount)
		{
			if (byteCount == 0) return "0 B";

			var negative = byteCount < 0;
			double value = negative ? -(double)byteCount : byteCount;

			var unit = 0;
			while (value >= 1024d && unit < SizeUnits.Length - 1)
			{
				value /= 1024d;
				unit++;
			}

			// Whole bytes never carry a fractional part; larger units keep up to two decimals.
			var text = unit == 0
				? value.ToString("0", CultureInfo.InvariantCulture)
				: value.ToString("0.##", CultureInfo.InvariantCulture);

			return (negative ? "-" : string.Empty) + text + " " + SizeUnits[unit];
		}

		// ── Lock detection ──────────────────────────────────────────────────

		/// <summary>
		/// Probes whether a file is currently held open by another handle. Returns <c>false</c> when
		/// the file does not exist. Detection is best-effort: a lock acquired between this call and a
		/// later access can still race.
		/// </summary>
		public static bool IsFileLocked(string path)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
			if (!File.Exists(path)) return false;

			try
			{
				using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
				{
					return false;
				}
			}
			catch (IOException)
			{
				return true;
			}
		}

		// ── Collision-free naming ───────────────────────────────────────────

		/// <summary>
		/// Given a desired file path, returns one that does not yet exist on disk. If the target is
		/// free it is returned as-is; otherwise a numbered suffix is inserted before the extension
		/// (<c>report.txt</c> → <c>report (1).txt</c>).
		/// </summary>
		public static string GenerateUniqueFilePath(string desiredPath)
		{
			if (string.IsNullOrEmpty(desiredPath)) throw new ArgumentNullException(nameof(desiredPath));

			var directory = Path.GetDirectoryName(desiredPath);
			var stem = Path.GetFileNameWithoutExtension(desiredPath);
			var extension = Path.GetExtension(desiredPath);

			var uniqueName = GenerateUniqueNumberedName(stem, candidate =>
			{
				var full = string.IsNullOrEmpty(directory) ? candidate + extension : Path.Combine(directory, candidate + extension);
				return File.Exists(full) || Directory.Exists(full);
			});

			var result = uniqueName + extension;
			return string.IsNullOrEmpty(directory) ? result : Path.Combine(directory, result);
		}

		/// <summary>
		/// Returns <paramref name="baseName"/> unchanged if <paramref name="isTaken"/> reports it as
		/// free; otherwise appends an incrementing <c> (n)</c> counter until a free name is found.
		/// The predicate lets callers define "taken" over files, directories, or any namespace.
		/// </summary>
		public static string GenerateUniqueNumberedName(string baseName, Func<string, bool> isTaken)
		{
			if (baseName == null) throw new ArgumentNullException(nameof(baseName));
			if (isTaken == null) throw new ArgumentNullException(nameof(isTaken));

			if (!isTaken(baseName)) return baseName;

			for (var counter = 1; counter < int.MaxValue; counter++)
			{
				var candidate = baseName + " (" + counter.ToString(CultureInfo.InvariantCulture) + ")";
				if (!isTaken(candidate)) return candidate;
			}

			throw new InvalidOperationException("Exhausted the numbered-name space for: " + baseName);
		}

		// ── Crash-safe writes ───────────────────────────────────────────────

		/// <summary>
		/// Atomically writes UTF-8 text (no BOM). The content is streamed to a temp file, flushed to
		/// disk, the previous revision is rotated to <c>.bak</c>, then the temp file is renamed into
		/// place — so a crash mid-write never leaves a truncated primary file.
		/// </summary>
		public static void AtomicWriteAllText(string path, string contents)
			=> AtomicWriteAllText(path, contents, Utf8NoBom);

		/// <summary>As <see cref="AtomicWriteAllText(string,string)"/> with an explicit encoding.</summary>
		public static void AtomicWriteAllText(string path, string contents, Encoding encoding)
		{
			if (encoding == null) throw new ArgumentNullException(nameof(encoding));
			AtomicWriteAllBytes(path, encoding.GetBytes(contents ?? string.Empty));
		}

		/// <summary>
		/// Atomically writes raw bytes using the temp-flush-rotate-rename sequence described on
		/// <see cref="AtomicWriteAllText(string,string)"/>.
		/// </summary>
		public static void AtomicWriteAllBytes(string path, byte[] contents)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
			if (contents == null) throw new ArgumentNullException(nameof(contents));

			DirectoryTools.CreateFromFilePath(path);

			var tempPath = path + TempSuffix;
			var backupPath = path + BackupSuffix;

			// 1. Stream into the scratch file and force it all the way to stable storage.
			using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				if (contents.Length > 0) stream.Write(contents, 0, contents.Length);
				stream.Flush(flushToDisk: true);
			}

			// 2. Rotate the current revision aside so the primary slot is free for the rename.
			if (File.Exists(path))
			{
				if (File.Exists(backupPath)) File.Delete(backupPath);
				File.Move(path, backupPath);
			}

			// 3. Promote the scratch file to the primary — an atomic rename on every target FS.
			File.Move(tempPath, path);
		}

		// ── Tolerant reads ──────────────────────────────────────────────────

		/// <summary>
		/// Reads all UTF-8 text from <paramref name="path"/>. If the primary is missing or unreadable
		/// it transparently retries the rotated <c>.bak</c>. Returns <c>null</c> when neither can be
		/// read — the caller's signal to treat this as a first-run / no-data condition.
		/// </summary>
		public static string SafeReadAllText(string path)
		{
			var bytes = SafeReadAllBytes(path);
			return bytes == null ? null : Utf8NoBom.GetString(bytes);
		}

		/// <summary>
		/// Byte-oriented counterpart to <see cref="SafeReadAllText"/> with the same <c>.bak</c>
		/// fallback and <c>null</c>-on-absence contract.
		/// </summary>
		public static byte[] SafeReadAllBytes(string path)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

			if (TryReadAllBytes(path, out var primary)) return primary;
			if (TryReadAllBytes(path + BackupSuffix, out var backup)) return backup;
			return null;
		}

		private static bool TryReadAllBytes(string path, out byte[] bytes)
		{
			bytes = null;
			if (!File.Exists(path)) return false;
			try
			{
				bytes = File.ReadAllBytes(path);
				return true;
			}
			catch (IOException)
			{
				return false;
			}
			catch (UnauthorizedAccessException)
			{
				return false;
			}
		}

		// ── Copy ────────────────────────────────────────────────────────────

		/// <summary>
		/// Copies a file, optionally materialising the destination directory first. Overwrites the
		/// destination unless <paramref name="overwrite"/> is <c>false</c>.
		/// </summary>
		public static void Copy(string sourcePath, string destinationPath, bool overwrite = true, bool createDestinationDirectory = true)
		{
			if (string.IsNullOrEmpty(sourcePath)) throw new ArgumentNullException(nameof(sourcePath));
			if (string.IsNullOrEmpty(destinationPath)) throw new ArgumentNullException(nameof(destinationPath));

			if (createDestinationDirectory)
				DirectoryTools.CreateFromFilePath(destinationPath);

			File.Copy(sourcePath, destinationPath, overwrite);
		}
	}
}
