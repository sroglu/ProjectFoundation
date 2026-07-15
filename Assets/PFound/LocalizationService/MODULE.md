# LocalizationService

## Purpose

Key→text localization with a pluggable content source, active/fallback language resolution,
parameter formatting and `{tag}` substitution, device/preference-driven startup language selection,
and a CSV/Google-Sheets editor pipeline. The core is engine-free and synchronous; a thin Unity layer
adds file loading, culture, and preference persistence. There is **no `MonoBehaviour` host** — the
consumer owns plain objects.

## Assemblies

- `PFound.LocalizationService` (runtime) — engine-free core (source contract, spine, facade,
  formatting, parameters, selection, CSV/INI parsing). `noEngineReferences: true`,
  `autoReferenced: false`.
- `PFound.LocalizationService.Unity` — engine-facing driver + file/culture/preference glue.
  References `PFound.LocalizationService`, `PFound.Compression`. `autoReferenced: false`.
- `PFound.LocalizationService.BestHttp` — remote/CDN table acquisition over BestHTTP (rootNamespace
  `PFound.LocalizationService.Unity`). References `PFound.LocalizationService`,
  `PFound.LocalizationService.Unity`, `BestHTTP`. Gated by the `PFOUND_BESTHTTP` define constraint,
  `autoReferenced: false` — the module compiles without the library present.
- `PFound.LocalizationService.Editor` — CSV / Google-Sheets / enum authoring tools (rootNamespace
  `PFound.LocalizationService.EditorTools`). References the runtime, `PFound.Compression`,
  `PFound.Utilities.EditorHelpers`.
- `PFound.LocalizationService.Tests` — NUnit suite (mono-testable core).

## Dependencies

- `PFound.Compression` — the Unity layer inflates `.lzma` tables via the shared codec.
- `PFound.Utilities.EditorHelpers` — editor tooling only (`ImportableAsset`/`ImportableAssetEditor`
  for the Google-Sheets config).
- `BestHTTP` — third-party HTTP transport, used **only** by the `PFound.LocalizationService.BestHttp`
  assembly for remote/CDN table acquisition; gated behind the `PFOUND_BESTHTTP` define constraint.
- Scripting defines: `PFOUND_BESTHTTP` (compiles the BestHTTP remote-acquisition assembly),
  `PFOUND_LOCALIZATION_QA` (compiles the non-shipping QA diagnostics hook on the catalog). See
  `## Conditional Compilation`. The engine-free core itself has no engine or third-party dependency.

## Key Types

**Core (`PFound.LocalizationService`)**

- `ILocalizationSource` — supplies the key→text table per language. Implementations:
  `InMemoryLocalizationSource` (code/tests), `DirectoryLocalizationSource` (folder of INI text files;
  definitions eager, content lazy). `ContentTables` backs the in-memory tables.
- `LocalizationService` — the spine: owns active/fallback tables + cache and language switching.
- `LocalizationCatalog` — the facade: layers parameter definitions, dynamic (template) keys, `{tag}`
  substitution, and the `FormatterRegistry` on top of the spine. Implements `ILocalizationContext`.
- `LanguageKey` (case-insensitive language code, e.g. `"en"`), `LocalizationKey` (implicit from
  `string`).
- `LocalizationKeyReference` / `LanguageKeyReference` — `[Serializable]` authoring counterparts of the
  readonly-struct runtime keys (serialized field `Key`); convert to the runtime key via `ToKey()` or
  the implicit operator. Used by inspector/ScriptableObject content that authors keys.
- `LocalizationDefinitions`, `ParameterDefinition` / `ParameterSpec`, `LanguageInfo`,
  `LanguageRedirections`, `LanguageSelector` (static device/preference resolution).
- `FormatterRegistry` + `IValueFormatter` (`BuiltInFormatters`, `NumberAbbreviator`),
  `ILocalizationValue` + `LocalizationValues`, `IValueKeyResolver`. `BuiltInFormatters` renders whole
  days in the `Duration` format as the count + a localized day-unit label (key
  `LocalizationConstants.DayAbbreviationKey`).
- `LocalizationDiagnostics` — build-gated QA layer (show-keys + pseudo-localization), engine-free and
  deterministic. Surfaced on the catalog only under `PFOUND_LOCALIZATION_QA`.
- INI/CSV: `IniDocument` (validates on `Write()` — see below), `CsvReader`, `LocalizationTableBuilder`.
  `LocalizableEnumAttribute` (`[LocalizableEnum]`), `EnumKeyGenerator`. `LocalizationLog`
  (pluggable log sink).

**Unity (`PFound.LocalizationService.Unity`)**

- `UnityLocalizationController` — the engine-facing driver. `SwitchLanguage` sets **process-wide**
  culture (`CultureInfo.DefaultThreadCurrentCulture`/`UICulture`) plus the current thread and the
  catalog's formatting culture.
- `TableFileLoader` — reads tables from StreamingAssets / persistentDataPath, `.lzma`-aware. The
  `.Hashes` partial adds content-address hash resolution + remote path/hash builders + the
  local-vs-remote refresh trigger.
- `DeviceCultureProvider`, `ILanguagePreferenceStore` (default `PlayerPrefsLanguageStore`),
  `LocalizationHashData` (readonly `ContentHash`/`DefinitionsHash` pair, `IsValid`, `Invalid`).

**BestHTTP (`PFound.LocalizationService.BestHttp`, `PFOUND_BESTHTTP`-gated)**

- `RemoteTableAcquisition` — downloads the hash-addressed content + definitions tables from a CDN base
  URL into the writable persistent Localizables folder (its own BestHTTP transport), skipping the
  transfer when the local hashes already match the remote manifest.

**Editor (`PFound.LocalizationService.EditorTools`)** — `CsvTableConverter`,
`LocalizableEnumScanner`, `GoogleSheetsConfig` + `GoogleSheetsConfigEditor`.

## Public API

**Lookup (`LocalizationCatalog`)**

- `string Get(LocalizationKey key, params ILocalizationValue[] values)`.
- `bool TryGet(key, out string text, params ...)` and the fallback-aware overload
  `bool TryGet(key, out string text, out bool fromFallback, params ...)`.
- `string GetEnsured(key, ...)` — throws when missing.
- `string GetOrErrorText(key, ...)` — logs + returns a visible `# [key] #` placeholder.
- Config seams: `SetValueKeyResolver(IValueKeyResolver)`, `SetCulture(CultureInfo)`,
  `FormatterRegistry Formatters`, `LocalizationDefinitions Definitions`, `CultureInfo Culture`.
- Language passthrough: `LanguageKey ActiveLanguage`, `IReadOnlyList<LanguageKey> SupportedLanguages`,
  `bool IsLanguageSupported(LanguageKey)`, `void SwitchLanguage(LanguageKey)`.
- Constructor: `new LocalizationCatalog(ILocalizationSource content, LanguageKey fallbackLanguage,
  LocalizationDefinitions definitions = null, FormatterRegistry formatters = null, CultureInfo culture
  = null)`.

**Driver (`UnityLocalizationController`)**

- `new UnityLocalizationController(LocalizationCatalog catalog, ILanguagePreferenceStore preferences =
  null)` — default store is `PlayerPrefsLanguageStore`.
- `LanguageKey ActivateStartupLanguage()` — pick + apply the startup language once at boot.
- `void SwitchLanguage(LanguageKey language, bool saveAsPreference = true)` — persists + reloads + sets
  thread culture.
- `LocalizationCatalog Catalog`, `LanguageKey ActiveLanguage`.

**Loading (`TableFileLoader`, static)** — `StreamingAssetsTablesDir`, `PersistentTablesDir`,
`ReadTable(path)`, `(LocalizationDefinitions definitions, InMemoryLocalizationSource content)
LoadFrom(string directory)`.

**Hash / remote paths (`TableFileLoader.Hashes`, static)**

- `string GetFileHashFromFileName(string fileName)`, `string GetHashFromUrl(string url)`.
- `string PersistentContentFilePath(string hash)`, `string PersistentDefinitionsFilePath(string hash)`.
- `string RemoteContentPath(string hash)`, `string RemoteDefinitionsPath(string hash)`.
- `bool NeedsRefresh(LocalizationHashData local, LocalizationHashData remote)` — the update trigger.
- `void ClearPersistentTablesDir()`, `LocalizationHashData GetLocalFileHashes()` /
  `GetLocalFileHashes(string directory)`.

**Remote acquisition (`RemoteTableAcquisition`, `PFOUND_BESTHTTP`)**

- `new RemoteTableAcquisition(string baseUrl)` — CDN base URL the hash-addressed paths compose against.
- `Task<bool> RefreshAsync(LocalizationHashData remote, CancellationToken cancellationToken = default)`
  — false when the local set is already current; true after downloading a fresh set.

**QA diagnostics (`LocalizationDiagnostics`, surfaced under `PFOUND_LOCALIZATION_QA`)**

- `bool ShowKeys`, `bool PseudoLocalize`, `float ExpansionFactor` (clamped non-negative).
- `string Apply(LocalizationKey key, string localized)`, `string Pseudoize(string text)`.
- On the catalog: `LocalizationDiagnostics Diagnostics { get; }` (compiled in only under the define).

**Formatter registry (`FormatterRegistry`)** — `void Register(IValueFormatter)`,
`bool Unregister(IValueFormatter)` (false if not registered), `void Clear()`.

**Authoring keys** — `LocalizationKeyReference` / `LanguageKeyReference`: `Key` field, `bool IsValid`,
`LocalizationKey ToKey()` / `LanguageKey ToKey()`, implicit conversions to/from `string` and the
runtime key.

## Setup / wiring

Plain library — no scene object, no singleton. Build the objects once at boot, keep the
`UnityLocalizationController` (or its `Catalog`) reference, and share it (e.g. register it in
`DependencyContainer`). The consumer, not the module, decides where it lives and how long it lasts.

```csharp
// 1. content source — folder of tables under StreamingAssets, .lzma-aware
var (definitions, content) = TableFileLoader.LoadFrom(TableFileLoader.StreamingAssetsTablesDir);
// (or DirectoryLocalizationSource for plain folders, or InMemoryLocalizationSource for code tables)

// 2. facade over the source, with a guaranteed fallback language
var catalog = new LocalizationCatalog(content, new LanguageKey("en"), definitions);

// 3. Unity driver: wires logging + persistence + thread culture
var controller = new UnityLocalizationController(catalog);   // default PlayerPrefs store
controller.ActivateStartupLanguage();                        // pick + apply startup language ONCE at boot

// use it:
string title = catalog.Get("menu.title");
controller.SwitchLanguage(new LanguageKey("tr"));            // persists + reloads + sets culture
```

Rules:

- Call `ActivateStartupLanguage()` exactly once during startup, before any UI reads text. It picks
  the startup language from the persisted preference + device culture (via `LanguageSelector`).
- The `fallbackLanguage` passed to `LocalizationCatalog` **must exist** in the source — it is loaded
  eagerly and throws if absent.
- Main-thread only.
- To swap persistence, pass your own `ILanguagePreferenceStore` to the controller.

**Remote / CDN table source (optional).** Define `PFOUND_BESTHTTP` to compile the
`PFound.LocalizationService.BestHttp` assembly, then before `LoadFrom(PersistentTablesDir)` refresh the
writable set against a manifest:

```csharp
var acquisition = new RemoteTableAcquisition(cdnBaseUrl);
await acquisition.RefreshAsync(remoteHashes);   // no-op when local hashes already match
var (definitions, content) = TableFileLoader.LoadFrom(TableFileLoader.PersistentTablesDir);
```

`RefreshAsync` clears the writable dir and pulls the hash-addressed files only when
`TableFileLoader.NeedsRefresh` sees a hash mismatch (`remoteHashes` are typically built from a manifest
via `TableFileLoader.GetHashFromUrl`). The transport lives in its own asmdef so the module compiles
without BestHTTP present.

**QA modes (non-shipping).** Define `PFOUND_LOCALIZATION_QA` to expose `catalog.Diagnostics`. Toggle
`Diagnostics.ShowKeys` (lookups return the raw key — spot missing/mis-wired keys) or
`Diagnostics.PseudoLocalize` (accented look-alikes + length expansion — spot font-coverage and layout
overflow). The gate compiles out entirely in shipping builds, so output is never altered.

**INI write-time validation.** `IniDocument.Write()` validates as it serializes (well-formed section
headers, no whitespace-padded keys/values, no reserved stored-newline escape in a value, no mixed
newline styles, no blank lines) and throws `InvalidOperationException` rather than emit a corrupt table.

## File Structure

```
LocalizationService/
  Runtime/                                # engine-free core (PFound.LocalizationService)
    ILocalizationSource.cs, LocalizationService.cs, LocalizationKeys.cs, LocalizationConstants.cs
    Sources/       ContentTables, DirectoryLocalizationSource, InMemoryLocalizationSource
    Facade/        LocalizationCatalog, IValueKeyResolver
    Formatting/    FormatterRegistry, BuiltInFormatters, NumberAbbreviator, IValueFormatter, ILocalizationContext
    Parameters/    ParameterDefinition, ParameterSpec, LocalizationValues, ILocalizationValue,
                   LocalizationFormat, LocalizableEnumAttribute, EnumKeyGenerator
    Model/         LanguageInfo, LocalizationDefinitions
    Selection/     LanguageSelector, LanguageRedirections
    Csv/           CsvReader, LocalizationTableBuilder
    Ini/           IniDocument
    Tags/          TagScanner
    Diagnostics/   LocalizationLog, LocalizationDiagnostics
  Unity/                                  # PFound.LocalizationService.Unity
    UnityLocalizationController, TableFileLoader(.Hashes), DeviceCultureProvider,
    ILanguagePreferenceStore, PlayerPrefsLanguageStore, LocalizationHashData
  BestHttp/                               # PFound.LocalizationService.BestHttp (PFOUND_BESTHTTP)
    RemoteTableAcquisition
  Editor/                                 # PFound.LocalizationService.Editor
    CsvTableConverter, LocalizableEnumScanner, GoogleSheets/{GoogleSheetsConfig,GoogleSheetsConfigEditor}
  Tests/                                  # LocalizationServiceTests, LocalizationV2Tests
```

## Downstream Dependents

None within PFound. Consumed directly by game bootstrap/UI code that references the runtime (and
`.Unity`) assembly.

## Conditional Compilation

- `PFOUND_BESTHTTP` — define constraint on the `PFound.LocalizationService.BestHttp` assembly; compiles
  `RemoteTableAcquisition` (remote/CDN table download over BestHTTP). Absent → the assembly is skipped
  and the module builds without the library. The rest of the module never references BestHTTP.
- `PFOUND_LOCALIZATION_QA` — exposes `LocalizationCatalog.Diagnostics` and its show-keys /
  pseudo-localization branches in the lookup path. Off by default; compiled out of shipping builds so
  resolved output is unchanged. Keep it a non-shipping (dev/QA) build flag.

## Extension points

- **Custom content source** — implement `ILocalizationSource` to feed the catalog from any backend
  (remote table, addressables, encrypted blob). The spine only needs per-language key→text tables.
- **Custom preference store** — implement `ILanguagePreferenceStore` and pass it to the controller to
  persist the language choice somewhere other than `PlayerPrefs`.
- **Custom formatters** — register `IValueFormatter` instances in the `FormatterRegistry` for typed
  parameter formatting (numbers, dates, abbreviations).
- **Value-key resolution** — supply an `IValueKeyResolver` via `SetValueKeyResolver` to map parameter
  values to nested localization keys (e.g. enum → localized label).

## Editor pipeline

`Editor/` provides the authoring side: `CsvTableConverter`, a Google-Sheets config + pull
(`GoogleSheets/`), and `LocalizableEnumScanner` (`[LocalizableEnum]` → generated keys). These emit the
INI text tables the runtime source consumes.

## Limitations / Known Gaps

- Main-thread, synchronous only — no async table streaming in the core (the Unity loader reads files
  eagerly; wrap it yourself for async load).
- The fallback language is eager and mandatory; a missing fallback table is a hard failure by design.
- No live/hot reload of tables at runtime beyond an explicit `SwitchLanguage`; edit-time regeneration
  is the editor pipeline's job.
- `{tag}` substitution and parameter formatting run per lookup — cache results for text drawn every
  frame.

## Testing

The core is engine-free — drive it with `InMemoryLocalizationSource` and assert
lookup/fallback/tag/formatting behavior (`LocalizationServiceTests`, `LocalizationV2Tests`) under
`csc`/`mono` or Unity EditMode. The Unity file/culture layer is verified in the editor.
