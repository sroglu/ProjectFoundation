using System;
using System.IO;
using System.Text;
using PFound.Utilities.FileSystemTools;

namespace PFound.Utilities.FileSystemTools.Tests
{
	/// <summary>
	/// Standalone Core test runner for the FileSystemTools module. Following the project convention
	/// (engine-free modules verified without Unity), this compiles and runs under <c>csc</c>+<c>mono</c>
	/// via its <see cref="Main"/> entry point and reports pass/total. Unity ignores the entry point.
	/// </summary>
	public static class FileSystemToolsTests
	{
		private static int _passed;
		private static int _total;

		public static int Main()
		{
			// ── PathTools ────────────────────────────────────────────────
			Case("SplitPath ignores empties/mixed separators", () =>
			{
				var seg = PathTools.SplitPath("a/b\\c//d");
				AssertEqual(4, seg.Length);
				AssertEqual("a", seg[0]);
				AssertEqual("d", seg[3]);
			});

			Case("GetPathSegment supports negative index", () =>
			{
				AssertEqual("b", PathTools.GetPathSegment("a/b/c", 1));
				AssertEqual("c", PathTools.GetPathSegment("a/b/c", -1));
				AssertEqual(null, PathTools.GetPathSegment("a/b/c", 9));
			});

			Case("ChangeFileExtension replaces, AddFileExtension appends", () =>
			{
				AssertEqual("f.png", NormSep(PathTools.ChangeFileExtension("f.txt", "png")));
				AssertEqual("f.png", NormSep(PathTools.ChangeFileExtension("f.txt", ".png")));
				AssertEqual("a.tar.gz", NormSep(PathTools.AddFileExtension("a.tar", "gz")));
			});

			Case("GetParentDirectoryName", () =>
				AssertEqual("b", PathTools.GetParentDirectoryName("a/b/c.txt")));

			Case("RemoveFirst/LastDirectoryFromPath", () =>
			{
				AssertEqual("b/c.txt", NormSep(PathTools.RemoveFirstDirectoryFromPath("a/b/c.txt")));
				AssertEqual("a/b", NormSep(PathTools.RemoveLastDirectoryFromPath("a/b/c")));
			});

			Case("FixDirectorySeparatorChars to forward/backward", () =>
			{
				AssertEqual("a/b/c", PathTools.FixDirectorySeparatorCharsToForward("a\\b/c"));
				AssertEqual("a\\b\\c", PathTools.FixDirectorySeparatorCharsToBackward("a/b\\c"));
			});

			Case("AddDirectorySeparatorToEnd is idempotent", () =>
			{
				AssertEqual("a/", PathTools.AddDirectorySeparatorToEnd("a", '/'));
				AssertEqual("a/", PathTools.AddDirectorySeparatorToEnd("a/", '/'));
			});

			Case("IsFullPath vs IsRelativePath", () =>
			{
				AssertTrue(PathTools.IsRelativePath("some/relative/file.txt"));
				AssertTrue(!PathTools.IsRelativePath(Path.GetFullPath(".")));
			});

			Case("MakeRelativePath round-trips against GetFullPath", () =>
			{
				var root = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var target = Path.Combine(root, "sub", "leaf.txt");
					var rel = PathTools.MakeRelativePath(root, target);
					var recombined = Path.GetFullPath(Path.Combine(root, rel));
					AssertEqual(Path.GetFullPath(target), recombined);
				}
				finally { DirectoryTools.DeleteWithContent(root); }
			});

			Case("PathCompare normalises separators and trailing slash", () =>
			{
				AssertEqual(0, PathTools.PathCompare("a/b/c", "a\\b\\c\\"));
				AssertTrue(PathTools.PathCompare("a/a", "a/b") < 0);
			});

			// ── FileTools: sizes / naming / locks ────────────────────────
			Case("ToFileSizeString binary steps", () =>
			{
				AssertEqual("0 B", FileTools.ToFileSizeString(0));
				AssertEqual("512 B", FileTools.ToFileSizeString(512));
				AssertEqual("1 KB", FileTools.ToFileSizeString(1024));
				AssertEqual("1.5 KB", FileTools.ToFileSizeString(1536));
				AssertEqual("1 MB", FileTools.ToFileSizeString(1024L * 1024));
				AssertEqual("-1 KB", FileTools.ToFileSizeString(-1024));
			});

			Case("GenerateUniqueNumberedName appends counter", () =>
			{
				var taken = new System.Collections.Generic.HashSet<string> { "doc", "doc (1)" };
				AssertEqual("doc (2)", FileTools.GenerateUniqueNumberedName("doc", taken.Contains));
				AssertEqual("fresh", FileTools.GenerateUniqueNumberedName("fresh", taken.Contains));
			});

			Case("GenerateUniqueFilePath avoids existing file", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var p = Path.Combine(dir, "report.txt");
					File.WriteAllText(p, "x");
					var unique = FileTools.GenerateUniqueFilePath(p);
					AssertEqual("report (1).txt", Path.GetFileName(unique));
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("IsFileLocked: false for absent, detects exclusive hold", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var p = Path.Combine(dir, "locked.bin");
					AssertTrue(!FileTools.IsFileLocked(p));
					File.WriteAllText(p, "x");
					using (File.Open(p, FileMode.Open, FileAccess.Read, FileShare.None))
					{
						AssertTrue(FileTools.IsFileLocked(p));
					}
					AssertTrue(!FileTools.IsFileLocked(p));
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			// ── FileTools: atomic write + safe read + .bak rotation ──────
			Case("AtomicWrite creates dirs and reads back", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var p = Path.Combine(dir, "nested", "deep", "save.json");
					FileTools.AtomicWriteAllText(p, "hello");
					AssertTrue(File.Exists(p));
					AssertEqual("hello", FileTools.SafeReadAllText(p));
					AssertTrue(!File.Exists(p + FileTools.TempSuffix)); // temp cleaned up by rename
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("Second AtomicWrite rotates previous revision to .bak", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var p = Path.Combine(dir, "save.json");
					FileTools.AtomicWriteAllText(p, "v1");
					FileTools.AtomicWriteAllText(p, "v2");
					AssertEqual("v2", FileTools.SafeReadAllText(p));
					AssertTrue(File.Exists(p + FileTools.BackupSuffix));
					AssertEqual("v1", File.ReadAllText(p + FileTools.BackupSuffix));
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("SafeReadAllText falls back to .bak when primary lost", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var p = Path.Combine(dir, "save.json");
					FileTools.AtomicWriteAllText(p, "first");
					FileTools.AtomicWriteAllText(p, "second");
					File.Delete(p); // simulate corruption / loss of the primary
					AssertEqual("first", FileTools.SafeReadAllText(p));
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("SafeReadAllText/Bytes return null when nothing exists", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var p = Path.Combine(dir, "missing.dat");
					AssertEqual(null, FileTools.SafeReadAllText(p));
					AssertTrue(FileTools.SafeReadAllBytes(p) == null);
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("AtomicWriteAllBytes round-trips binary", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var p = Path.Combine(dir, "blob.bin");
					var payload = new byte[] { 0, 1, 2, 250, 255 };
					FileTools.AtomicWriteAllBytes(p, payload);
					var read = FileTools.SafeReadAllBytes(p);
					AssertEqual(payload.Length, read.Length);
					for (var i = 0; i < payload.Length; i++) AssertEqual(payload[i], read[i]);
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("Copy creates destination directory", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					var src = Path.Combine(dir, "src.txt");
					File.WriteAllText(src, "payload");
					var dst = Path.Combine(dir, "out", "copy.txt");
					FileTools.Copy(src, dst);
					AssertEqual("payload", File.ReadAllText(dst));
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			// ── DirectoryTools ───────────────────────────────────────────
			Case("IsDirectoryEmpty", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					AssertTrue(DirectoryTools.IsDirectoryEmpty(dir));
					File.WriteAllText(Path.Combine(dir, "x.txt"), "x");
					AssertTrue(!DirectoryTools.IsDirectoryEmpty(dir));
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("ListFilesInDirectory with recursion and pattern", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					File.WriteAllText(Path.Combine(dir, "a.json"), "{}");
					File.WriteAllText(Path.Combine(dir, "b.txt"), "x");
					Directory.CreateDirectory(Path.Combine(dir, "sub"));
					File.WriteAllText(Path.Combine(dir, "sub", "c.json"), "{}");

					AssertEqual(2, PathTools_Count(DirectoryTools.ListFilesInDirectory(dir))); // top only
					AssertEqual(2, PathTools_Count(DirectoryTools.ListFilesInDirectory(dir, true, "*.json")));
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("DeleteEmptySubdirectories prunes deepest-first", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					Directory.CreateDirectory(Path.Combine(dir, "empty1", "empty2"));
					Directory.CreateDirectory(Path.Combine(dir, "kept"));
					File.WriteAllText(Path.Combine(dir, "kept", "f.txt"), "x");

					var deleted = DirectoryTools.DeleteEmptySubdirectories(dir);
					AssertEqual(2, deleted); // empty2 then empty1
					AssertTrue(!Directory.Exists(Path.Combine(dir, "empty1")));
					AssertTrue(Directory.Exists(Path.Combine(dir, "kept")));
				}
				finally { DirectoryTools.DeleteWithContent(dir); }
			});

			Case("DeleteWithContent removes populated tree", () =>
			{
				var dir = DirectoryTools.CreateTemporaryDirectory();
				Directory.CreateDirectory(Path.Combine(dir, "sub"));
				File.WriteAllText(Path.Combine(dir, "sub", "f.txt"), "x");
				DirectoryTools.DeleteWithContent(dir);
				AssertTrue(!Directory.Exists(dir));
			});

			Case("CreateTemporaryDirectory yields a fresh existing dir", () =>
			{
				var a = DirectoryTools.CreateTemporaryDirectory();
				var b = DirectoryTools.CreateTemporaryDirectory();
				try
				{
					AssertTrue(Directory.Exists(a));
					AssertTrue(a != b);
				}
				finally
				{
					DirectoryTools.DeleteWithContent(a);
					DirectoryTools.DeleteWithContent(b);
				}
			});

			Console.WriteLine();
			Console.WriteLine("FileSystemTools Core: " + _passed + "/" + _total + " passed.");
			return _passed == _total ? 0 : 1;
		}

		// ── Tiny assertion harness ───────────────────────────────────────

		private static void Case(string name, Action body)
		{
			_total++;
			try
			{
				body();
				_passed++;
				Console.WriteLine("  PASS  " + name);
			}
			catch (Exception ex)
			{
				Console.WriteLine("  FAIL  " + name + "  -> " + ex.Message);
			}
		}

		private static void AssertTrue(bool condition)
		{
			if (!condition) throw new Exception("expected true");
		}

		private static void AssertEqual(object expected, object actual)
		{
			if (!Equals(expected, actual))
				throw new Exception("expected [" + (expected ?? "null") + "] but was [" + (actual ?? "null") + "]");
		}

		private static string NormSep(string p) => PathTools.FixDirectorySeparatorCharsToForward(p);

		private static int PathTools_Count(System.Collections.Generic.IReadOnlyList<string> list) => list.Count;
	}
}
