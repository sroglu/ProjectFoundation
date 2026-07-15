using System;
using System.IO;
using System.Text;

namespace PFound.Utilities.FileSystemTools
{
	/// <summary>
	/// Pure-<c>System.IO</c> helpers for manipulating and inspecting path strings. Nothing here
	/// touches the disk; every method operates on the textual form of a path so the type stays
	/// engine-free and unit-testable outside of Unity.
	/// </summary>
	public static class PathTools
	{
		/// <summary>Forward slash — the separator preferred for portable / serialized paths.</summary>
		public const char ForwardSeparator = '/';

		/// <summary>Backslash — the Windows-native separator.</summary>
		public const char BackwardSeparator = '\\';

		private static readonly char[] AnySeparator = { ForwardSeparator, BackwardSeparator };

		// ── Relative / absolute ─────────────────────────────────────────────

		/// <summary>
		/// Produces the relative path that navigates from <paramref name="basePath"/> (treated as a
		/// directory) to <paramref name="targetPath"/>. Both inputs are resolved to absolute form
		/// first. The result uses the platform separator.
		/// </summary>
		public static string MakeRelativePath(string basePath, string targetPath)
		{
			if (string.IsNullOrEmpty(basePath)) throw new ArgumentNullException(nameof(basePath));
			if (string.IsNullOrEmpty(targetPath)) throw new ArgumentNullException(nameof(targetPath));

			var baseUri = new Uri(AddDirectorySeparatorToEnd(Path.GetFullPath(basePath)));
			var targetUri = new Uri(Path.GetFullPath(targetPath));

			if (baseUri.Scheme != targetUri.Scheme)
				return targetPath; // Different volumes/schemes: no relative form exists.

			var relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString());
			return relative.Replace(ForwardSeparator, Path.DirectorySeparatorChar);
		}

		/// <summary>
		/// True when <paramref name="path"/> denotes an absolute, fully-qualified location (a drive
		/// root on Windows or a leading slash on POSIX) rather than a drive-relative or bare
		/// relative fragment.
		/// </summary>
		public static bool IsFullPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return false;
			if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
			if (!Path.IsPathRooted(path)) return false;

			var root = Path.GetPathRoot(path);
			// "\folder" or "/folder" is rooted yet drive-relative — not fully qualified on Windows.
			if (root == BackwardSeparator.ToString() || root == ForwardSeparator.ToString())
				return Path.DirectorySeparatorChar == ForwardSeparator; // POSIX: leading '/' IS full.

			return true;
		}

		/// <summary>Inverse of <see cref="IsFullPath"/> for any syntactically valid path.</summary>
		public static bool IsRelativePath(string path) => !IsFullPath(path);

		// ── Extensions ──────────────────────────────────────────────────────

		/// <summary>Normalises an extension token so it always begins with a single leading dot.</summary>
		private static string NormalizeExtension(string extension)
		{
			if (string.IsNullOrEmpty(extension)) return string.Empty;
			return extension[0] == '.' ? extension : "." + extension;
		}

		/// <summary>
		/// Replaces the file's existing extension with <paramref name="newExtension"/>. Passing an
		/// empty extension strips it. A leading dot is optional.
		/// </summary>
		public static string ChangeFileExtension(string path, string newExtension)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));
			return Path.ChangeExtension(path, string.IsNullOrEmpty(newExtension)
				? null
				: NormalizeExtension(newExtension));
		}

		/// <summary>
		/// Appends <paramref name="extension"/> to the path without disturbing any extension that is
		/// already present (e.g. <c>archive.tar</c> → <c>archive.tar.gz</c>).
		/// </summary>
		public static string AddFileExtension(string path, string extension)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));
			return path + NormalizeExtension(extension);
		}

		// ── Segment queries ─────────────────────────────────────────────────

		/// <summary>
		/// Name of the directory that immediately contains <paramref name="path"/>. For
		/// <c>a/b/c.txt</c> this is <c>b</c>.
		/// </summary>
		public static string GetParentDirectoryName(string path)
		{
			if (string.IsNullOrEmpty(path)) return string.Empty;
			var trimmed = path.TrimEnd(AnySeparator);
			var parent = Path.GetDirectoryName(trimmed);
			return string.IsNullOrEmpty(parent) ? string.Empty : Path.GetFileName(parent);
		}

		/// <summary>Drops the leading segment, e.g. <c>a/b/c.txt</c> → <c>b/c.txt</c>.</summary>
		public static string RemoveFirstDirectoryFromPath(string path)
		{
			var segments = SplitPath(path);
			if (segments.Length <= 1) return string.Empty;
			return string.Join(Path.DirectorySeparatorChar.ToString(), segments, 1, segments.Length - 1);
		}

		/// <summary>Drops the trailing segment, e.g. <c>a/b/c</c> → <c>a/b</c>.</summary>
		public static string RemoveLastDirectoryFromPath(string path)
		{
			var segments = SplitPath(path);
			if (segments.Length <= 1) return string.Empty;
			return string.Join(Path.DirectorySeparatorChar.ToString(), segments, 0, segments.Length - 1);
		}

		// ── Separator normalisation ─────────────────────────────────────────

		/// <summary>Rewrites every separator to the current platform's separator character.</summary>
		public static string FixDirectorySeparatorChars(string path)
			=> FixDirectorySeparatorChars(path, Path.DirectorySeparatorChar);

		/// <summary>Rewrites every separator to <paramref name="separator"/>.</summary>
		public static string FixDirectorySeparatorChars(string path, char separator)
		{
			if (path == null) return null;
			var builder = new StringBuilder(path.Length);
			foreach (var c in path)
				builder.Append(c == ForwardSeparator || c == BackwardSeparator ? separator : c);
			return builder.ToString();
		}

		/// <summary>Forces all separators to forward slashes (portable / serialized form).</summary>
		public static string FixDirectorySeparatorCharsToForward(string path)
			=> FixDirectorySeparatorChars(path, ForwardSeparator);

		/// <summary>Forces all separators to backslashes (Windows-native form).</summary>
		public static string FixDirectorySeparatorCharsToBackward(string path)
			=> FixDirectorySeparatorChars(path, BackwardSeparator);

		/// <summary>
		/// Guarantees the path ends with a directory separator, appending the platform separator
		/// only when one is not already present. Empty input is returned unchanged.
		/// </summary>
		public static string AddDirectorySeparatorToEnd(string path)
			=> AddDirectorySeparatorToEnd(path, Path.DirectorySeparatorChar);

		/// <summary>As <see cref="AddDirectorySeparatorToEnd(string)"/> but with an explicit separator.</summary>
		public static string AddDirectorySeparatorToEnd(string path, char separator)
		{
			if (string.IsNullOrEmpty(path)) return path;
			var last = path[path.Length - 1];
			if (last == ForwardSeparator || last == BackwardSeparator) return path;
			return path + separator;
		}

		// ── Splitting / indexing ────────────────────────────────────────────

		/// <summary>
		/// Breaks a path into its non-empty segments, accepting either separator style. Trailing and
		/// duplicated separators produce no empty entries.
		/// </summary>
		public static string[] SplitPath(string path)
		{
			if (string.IsNullOrEmpty(path)) return Array.Empty<string>();
			return path.Split(AnySeparator, StringSplitOptions.RemoveEmptyEntries);
		}

		/// <summary>
		/// Returns the segment at <paramref name="index"/>. Negative indices count back from the end
		/// (<c>-1</c> is the last segment). Out-of-range indices yield <c>null</c>.
		/// </summary>
		public static string GetPathSegment(string path, int index)
		{
			var segments = SplitPath(path);
			if (segments.Length == 0) return null;
			if (index < 0) index += segments.Length;
			if (index < 0 || index >= segments.Length) return null;
			return segments[index];
		}

		// ── Comparison ──────────────────────────────────────────────────────

		/// <summary>
		/// Ordinal comparison of two paths after normalising separators and trimming trailing ones.
		/// Case is ignored on platforms with case-insensitive file systems (Windows). Mirrors the
		/// sign convention of <see cref="string.CompareTo(string)"/>.
		/// </summary>
		public static int PathCompare(string a, string b)
		{
			var na = Normalize(a);
			var nb = Normalize(b);
			var comparison = Path.DirectorySeparatorChar == BackwardSeparator
				? StringComparison.OrdinalIgnoreCase
				: StringComparison.Ordinal;
			return string.Compare(na, nb, comparison);
		}

		private static string Normalize(string path)
		{
			if (string.IsNullOrEmpty(path)) return string.Empty;
			return FixDirectorySeparatorChars(path).TrimEnd(AnySeparator);
		}
	}
}
