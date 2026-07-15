# FileSystemTools
Path, file, and directory helpers (pure C#, no engine dependency): path normalization, atomic writes, safe reads, temp/unique naming, directory cleanup.

**Key classes:** `PathTools`, `FileTools`, `DirectoryTools`
**Assembly:** `PFound.Utilities.FileSystemTools`
**Tier:** core
**Depends on:** none

## Current API
- `PathTools` — `MakeRelativePath`, `IsFullPath`/`IsRelativePath`, `ChangeFileExtension`/`AddFileExtension`, `GetParentDirectoryName`, `RemoveFirst/LastDirectoryFromPath`, `FixDirectorySeparatorChars(...)` (+ ToForward/ToBackward), `AddDirectorySeparatorToEnd`, `SplitPath`, `GetPathSegment`, `PathCompare`.
- `FileTools` — `ToFileSizeString`, `IsFileLocked`, `GenerateUniqueFilePath`, `GenerateUniqueNumberedName`, `AtomicWriteAllText`/`AtomicWriteAllBytes`, `SafeReadAllText`/`SafeReadAllBytes`, `Copy`.
- `DirectoryTools` — `IsDirectoryEmpty`, `ListFilesInDirectory`, `DeleteWithContent`, `DeleteEmptySubdirectories`, `CreateFromFilePath`, `CreateTemporaryDirectory`.

## Limitations
- Synchronous `System.IO`; no async variants. Fail-fast on invalid input rather than defensive guards.
