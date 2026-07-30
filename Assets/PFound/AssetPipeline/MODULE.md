# AssetPipeline

> **Module group — Content & Assets.** Sibling modules in this group: `ContentDelivery`, `RemoteResourceCache`, `Compression`. Grouped by purpose — see the catalog `Assets/PFound/README.md` and each module's **Dependencies** for exact edges.

## Purpose

Editor-only build-prep tooling that audits authored textures and meshes against an import policy
(default AND per-platform iOS/Android settings), optionally applies the fixes, and builds/maintains
sprite atlases (packing layout, per-platform format/compression, auto page-size, auto-regenerate on
import, and a build preprocessor that stops double-shipping). It works over the `AssetGroup`s that
`PFound.ContentDelivery` already uses to ship content, so "is this asset imported well?", "is it
wastefully duplicated across bundles?", and "is this the same image under two files?" are answered from
the same authoring source the bundle build reads. The audit is gated by a reference graph — unreferenced
assets are skipped and atlas members are exempt from per-texture format rules. The policy/decision layer
is engine-free pure C# (unit-testable, 41 tests); the editor layer reads and rewrites Unity importer
settings and injects the graph.

## Scope boundary — build-time asset prep vs ContentDelivery

AssetPipeline is **editor / build-time** tooling: it audits import settings and builds atlases over the
`AssetGroup`s. It ships and loads nothing at runtime. `PFound.ContentDelivery` is the module that
**builds the bundles + catalog and delivers/loads assets at runtime** over those same groups. Rule:
"is this asset imported/packed well?" → AssetPipeline; "get me this asset at runtime by address" →
ContentDelivery. Shared AssetGroup authoring source, different stage.

## Assemblies

| Assembly | Location | Platform | Notes |
|----------|----------|----------|-------|
| `PFound.AssetPipeline.Core` | `Core/Runtime/` | any | `noEngineReferences: true`, `autoReferenced: false`. No engine or `UnityEditor` types — pure data + rules. |
| `PFound.AssetPipeline.Editor` | `Editor/` | `Editor` only | `autoReferenced: false`. References Core + ContentDelivery. |
| `PFound.AssetPipeline.Core.Tests` | `Core/Tests/` | any | `noEngineReferences: true`. Standalone `csc`/`mono` runner (`Program.cs`). |
| `PFound.AssetPipeline.Tests` | `Tests/` | `Editor` only | NUnit EditMode suites (`UNITY_INCLUDE_TESTS`). |

`autoReferenced: false` is deliberate: nothing pulls this tooling in implicitly — a consumer editor
script references the assembly on purpose.

## Dependencies

- **PFound modules:** `PFound.ContentDelivery.Editor`, `PFound.ContentDelivery`,
  `PFound.ContentDelivery.Core` (Editor assembly only). The Core assembly has **no** dependencies.
- **Unity (Editor assembly only):** `UnityEditor`, `UnityEditor.U2D` / `UnityEngine.U2D` (sprite
  atlas API), `UnityEditor.Build` / `UnityEditor.Build.Reporting` (the build preprocessor).
- **Third-party / scripting defines:** none.

## Key Types

**Core (`PFound.AssetPipeline.Core`, engine-free):**
- `AssetPolicy` — the set of import rules (plain C# class, one field per knob).
- `AssetPolicyEvaluator` — the pure decision layer: facts + policy (+ optional reference graph) → violations.
- `IAssetReferenceGraph` — the injected "is this referenced / atlas-packed?" seam the evaluator gates on
  (keeps gating out of the pure layer; the editor supplies a real implementation).
- `TextureImporterFacts` / `PlatformTextureFacts` / `MeshImporterFacts` — one importer's settings as plain
  data, including per-platform (iOS/Android) texture overrides and the mesh's `MeshSource`.
- `TextureCompressionLevel` / `NpotScale` / `MeshCompressionLevel` / `MeshSource` — engine-free mirrors of
  the Unity importer enums.
- `PolicyViolation` (readonly struct) + `ViolationCode` (enum) — one breach: asset path, rule, build-target
  scope (`Platform`, empty = default), detail.
- `AssetAuditReport` — a scan result: policy description, assets scanned, violations.
- `AtlasInputHasher` — order-independent SHA-256 digest of an atlas input set (change detection).
- `AtlasSizeCalculator` — pure page-size math: smallest clamped POT page covering the members' area + largest dim.
- `DuplicateTextureFinder` + `DuplicateTextureGroup` — groups distinct files that share a pixel content hash.

**Editor (`PFound.AssetPipeline.Editor`):**
- `AssetAuditor` — audit pass over `AssetGroup`s; reports, never mutates. Builds and injects the reference graph.
- `AssetReferenceGraph` — `IAssetReferenceGraph` backed by the AssetDatabase (reverse-dependency closure +
  sprite-atlas membership; seedable roots).
- `AssetAuditResult` — full-audit bundle: the per-asset `AssetAuditReport` + cross-bundle `DuplicateDependency`
  findings (from ContentDelivery) + content-identical `DuplicateTextureGroup` findings.
- `AssetOptimizer` — the opt-in apply pass: rewrites default + per-platform importer settings, batched inside
  one asset-editing scope with a cancelable progress hook.
- `SpriteAtlasBuilder` + `SpriteAtlasBuildResult` + `AtlasBuildSettings` — builds a `SpriteAtlas` from a
  group/folder/paths with packing layout, per-platform format/compression and auto page-size; bulk regenerate.
- `SpriteAtlasAutoRegenerator` (AssetPostprocessor) — rebuilds a managed atlas when a member is reimported.
- `SpriteAtlasBuildPreprocessor` (IPreprocessBuildWithReport) — turns include-in-build off for managed atlases
  before a player build so their sprites ship once (via bundle).
- `SpriteAtlasHygiene` — clear obsolete atlas members; list sprites both atlas-packed and directly referenced.
- `TextureImporterReader` / `MeshImporterReader` / `MeshAssetReader` — translate a Unity importer OR a raw
  `Mesh` asset into engine-free facts.
- `TextureContentDuplicateAnalyzer` — reads each texture's pixel hash and groups content-identical duplicates.
- `AssetAuditReportExporter` — serializes a report (+ duplicate deps + duplicate textures) to JSON.
- `AssetPipelineMenu` — the `MenuItem` entry points.

## Public API

**`AssetPolicy` (Core):**
- Rule fields: `int MaxTextureSize` (default 2048), `bool RequireCompressed`,
  `bool DisallowUncompressedRgba32`, `bool DisallowCrunch`, `bool RequirePowerOfTwoOrNpotScale`,
  `bool DisallowTextureReadWrite`, `bool DisallowSpriteMipmaps`, `bool DisallowMeshReadWrite`,
  `MeshCompressionLevel MinMeshCompression`.
- `static AssetPolicy MobileDefaults()` — the shipped baseline (the default-constructed policy).
- `string Describe()` — one-line summary of the active rules (embedded in the report).

**`AssetPolicyEvaluator` (Core, static):** each texture/mesh/batch method has an overload taking an optional
`IAssetReferenceGraph graph` (null = no gating). Passing a graph skips unreferenced assets and applies only the
Read/Write rule to atlas members. The evaluator also checks each **overridden** per-platform texture setting,
reporting on it with the breach's `Platform` set.
- `void EvaluateTexture(TextureImporterFacts f, AssetPolicy policy, List<PolicyViolation> into[, IAssetReferenceGraph graph])`
- `void EvaluateMesh(MeshImporterFacts f, AssetPolicy policy, List<PolicyViolation> into[, IAssetReferenceGraph graph])`
- `List<PolicyViolation> Evaluate(IEnumerable<TextureImporterFacts> textures, IEnumerable<MeshImporterFacts> meshes, AssetPolicy policy[, IAssetReferenceGraph graph])`

**`AtlasSizeCalculator` (Core, static):** `int ComputeMaxTextureSize(double totalMemberArea, int largestMemberDimension[, out bool clamped], int clamp = 4096)`; consts `MaxAtlasSize = 4096`, `MinAtlasSize = 32`.

**`DuplicateTextureFinder` (Core, static):** `List<DuplicateTextureGroup> Find(IEnumerable<KeyValuePair<string,string>> pathToContentHash)` — groups paths that share a content hash (each `DuplicateTextureGroup` = `{ ContentHash, Paths[] }`).

**`AssetAuditReport` (Core):** `int ViolationCount`, `bool IsClean`, `int CountOf(ViolationCode)`,
`ToString()`; fields `PolicyDescription`, `AssetsScanned`, `IReadOnlyList<PolicyViolation> Violations`.

**`AtlasInputHasher` (Core, static):** `string Compute(IEnumerable<string> memberIdentities)` — 32 hex
chars, order-independent.

**`AssetAuditor` (Editor, static):**
- `AssetAuditReport Audit(IReadOnlyList<AssetGroup> groups, AssetPolicy policy)` — gated (builds the reference graph, seeds group entries as roots)
- `AssetAuditResult AuditAll(IReadOnlyList<AssetGroup> groups, AssetPolicy policy)` (+ duplicate deps + duplicate textures)
- `AssetAuditReport AuditPaths(IEnumerable<string> assetPaths, AssetPolicy policy[, IAssetReferenceGraph graph])` — the testable seam
- `List<string> CollectAssetPaths(IReadOnlyList<AssetGroup> groups)`

**`AssetReferenceGraph` (Editor, `IAssetReferenceGraph`):** `static AssetReferenceGraph Build([IEnumerable<string> roots])`; `bool IsReferenced(string)`, `bool IsAtlasPacked(string)`.

**`AssetOptimizer` (Editor, static):** returns assets changed; the batch runs in one begin/end asset-editing scope.
- `int Apply(AssetAuditReport report, AssetPolicy policy[, ProgressCallback progress])`
- `int Apply(IReadOnlyList<PolicyViolation> violations, AssetPolicy policy[, ProgressCallback progress])`
- `delegate bool ProgressCallback(int index, int total, string assetPath)` — return true to cancel.

**`AtlasBuildSettings` (Editor):** packing (`Padding`, `EnableRotation`, `EnableTightPacking`), page size
(`AutoSize`, `FixedMaxTextureSize`, `MaxSizeClamp`), per-platform format/compression (default + iOS + Android);
`static AtlasBuildSettings MobileDefaults()`.

**`SpriteAtlasBuilder` (Editor, static):**
- `SpriteAtlasBuildResult Build(string atlasPath, IReadOnlyList<string> spriteAssetPaths[, AtlasBuildSettings settings])`
- `SpriteAtlasBuildResult BuildFromFolder(string atlasPath, string folder[, AtlasBuildSettings settings])`
- `SpriteAtlasBuildResult BuildFromGroup(string atlasPath, AssetGroup group[, AtlasBuildSettings settings])`
- `string ComputeContentHash(IEnumerable<string> spriteAssetPaths)`, `string GetStoredContentHash(string atlasPath)`,
  `bool IsUpToDate(string atlasPath, IReadOnlyList<string> spriteAssetPaths)`
- `List<string> ManagedAtlasPaths()`, `bool RegenerateIfOutOfDate(string atlasPath, AtlasBuildSettings settings)`,
  `int RegenerateAllManagedAtlases(AtlasBuildSettings settings[, ProgressCallback progress])`
- `AssetEntry AddAtlasToGroup(AssetGroup group, string atlasPath, string address)`
- `delegate bool ProgressCallback(int index, int total, string label)`

**`SpriteAtlasHygiene` (Editor, static):** `int ClearObsoleteMembers([IAssetReferenceGraph graph])`;
`List<string> FindPackedAndDirectlyReferencedSprites()`.

**`TextureContentDuplicateAnalyzer` (Editor, static):** `List<DuplicateTextureGroup> Analyze(IEnumerable<string> texturePaths)`, `List<DuplicateTextureGroup> AnalyzeProject()`.

**`AssetAuditReportExporter` (Editor, static):** `const string ReportFileName = "asset-audit.json"`;
`string ToJson(AssetAuditReport report, IReadOnlyList<DuplicateDependency> duplicates = null, IReadOnlyList<DuplicateTextureGroup> duplicateTextures = null, bool prettyPrint = true)`;
`string Write(AssetAuditReport report, IReadOnlyList<DuplicateDependency> duplicates, string directory, IReadOnlyList<DuplicateTextureGroup> duplicateTextures = null)`.

## Setup / wiring

**Editor-only tooling — no runtime component, no scene object, no `DontDestroyOnLoad`, no DI
registration, nothing initialized at load.** You drive it from the menu commands or from your own
editor scripts. The `Editor` assembly is `includePlatforms: ["Editor"]`; the `Core` assembly is
`noEngineReferences: true`, so the policy/evaluator run under `mono`/`csc` with no Unity present.

**No config asset — the policy is in-memory.** AssetPipeline defines **no** ScriptableObject/authored
policy asset. A policy is a plain `AssetPolicy` object constructed in code: either
`AssetPolicy.MobileDefaults()` (the default-constructed baseline) or `new AssetPolicy { ... }` with the
knobs a project wants, passed explicitly on every call. There is no global/registered policy to place.

**Menu commands** (all under `PFound/Asset Pipeline/`, defined in `AssetPipelineMenu`):

| Menu path | Effect |
|-----------|--------|
| `PFound/Asset Pipeline/Audit Assets (All Groups)` | Read-only. Loads every group via `ContentDeliveryMenu.LoadAllGroups()`, runs `AssetAuditor.AuditAll(...)` with `AssetPolicy.MobileDefaults()`, logs the result (policy + duplicate deps + duplicate textures), and writes `asset-audit.json` under `<project>/AssetAudit/`. |
| `PFound/Asset Pipeline/Optimize Assets (Apply Fixes — All Groups)` | Audits, and if not clean, shows a confirm dialog, then calls `AssetOptimizer.Apply(...)` (batched, cancelable progress) to rewrite default + per-platform importer settings and reimport. Opt-in mutation, never a side effect of the audit. |
| `PFound/Asset Pipeline/Build Sprite Atlas (Selected Asset Group)` | Requires an `AssetGroup` selected in the Project window (warns otherwise). Builds `<groupDir>/<group.ResolveBundleName()>_atlas.spriteatlas` via `SpriteAtlasBuilder.BuildFromGroup(...)` with packing + per-platform + auto page-size, and stamps its input content hash. |
| `PFound/Asset Pipeline/Regenerate All Sprite Atlases` | Rebuilds every out-of-date builder-managed atlas (`SpriteAtlasBuilder.RegenerateAllManagedAtlases`) with a cancelable progress bar. |
| `PFound/Asset Pipeline/Clear Obsolete Atlas Sprites` | Removes packables that no longer resolve to a referenced sprite from every managed atlas (`SpriteAtlasHygiene.ClearObsoleteMembers`). |
| `PFound/Asset Pipeline/List Packed + Directly-Referenced Sprites` | Warns for each sprite that is BOTH atlas-packed and directly referenced (a double-load candidate). |
| `PFound/Asset Pipeline/Find Content-Identical Duplicate Textures` | Scans all `t:Texture2D` for distinct files sharing a pixel content hash (`TextureContentDuplicateAnalyzer.AnalyzeProject`). |

Two callbacks run without a menu: `SpriteAtlasAutoRegenerator` (AssetPostprocessor) rebuilds a managed
atlas when one of its members is reimported and the stamped hash no longer matches; `SpriteAtlasBuildPreprocessor`
(`IPreprocessBuildWithReport`) disables include-in-build on managed atlases before a player build so their
sprites ship once (via bundle), never twice.

**From an editor script:**

```csharp
using PFound.AssetPipeline.Core;
using PFound.AssetPipeline.Editor;

// Policy is in-memory: the mobile baseline, or your own knobs.
var policy = AssetPolicy.MobileDefaults();
// var policy = new AssetPolicy { MaxTextureSize = 1024, MinMeshCompression = MeshCompressionLevel.Medium };

AssetAuditReport report = AssetAuditor.Audit(groups, policy);  // groups: IReadOnlyList<AssetGroup>
if (!report.IsClean)
    Debug.Log($"{report.ViolationCount} violation(s)\n{report}");

int changed = AssetOptimizer.Apply(report, policy);            // opt-in: rewrites importers, reimports
AssetAuditReportExporter.Write(report, duplicates: null, directory: "AssetAudit");
```

Fail-fast is the house rule: the evaluator and exporter throw `ArgumentNullException` on null inputs;
a genuinely-bad asset path is left to throw rather than being defensively swallowed.

**Typical flow:** author assets into `AssetGroup`s (ContentDelivery) → **Audit** → fix (by hand or
**Optimize**) → **Build Sprite Atlas** where useful → **Build Content** (ContentDelivery).

## File Structure

```
AssetPipeline/
├── README.md                     # thin landing page
├── MODULE.md                     # this document
├── Core/
│   ├── Runtime/                  # PFound.AssetPipeline.Core (engine-free)
│   │   ├── AssetPolicy.cs                # import rules + MobileDefaults() + Describe()
│   │   ├── AssetPolicyEnums.cs           # TextureCompressionLevel, NpotScale, MeshCompressionLevel
│   │   ├── AssetPolicyEvaluator.cs       # facts + policy (+ graph) → violations (pure)
│   │   ├── IAssetReferenceGraph.cs       # injected is-referenced / is-atlas-packed seam
│   │   ├── ImporterFacts.cs              # TextureImporterFacts, PlatformTextureFacts, MeshImporterFacts, MeshSource
│   │   ├── PolicyViolation.cs            # PolicyViolation struct (+ Platform) + ViolationCode enum
│   │   ├── AssetAuditReport.cs           # scan result (policy desc, count, violations)
│   │   ├── AtlasInputHasher.cs           # order-independent SHA-256 input digest
│   │   ├── AtlasSizeCalculator.cs        # pure atlas page-size math (area + largest dim, clamp 4096)
│   │   ├── DuplicateTextureFinder.cs     # groups distinct files sharing a content hash
│   │   └── PFound.AssetPipeline.Core.asmdef
│   └── Tests/                    # standalone csc/mono runner (41 tests)
│       ├── Program.cs
│       └── PFound.AssetPipeline.Core.Tests.asmdef
├── Editor/                       # PFound.AssetPipeline.Editor (Editor-only)
│   ├── AssetPipelineMenu.cs             # the MenuItem entry points
│   ├── AssetAuditor.cs                  # audit pass over AssetGroups (builds + injects the graph)
│   ├── AssetReferenceGraph.cs          # IAssetReferenceGraph over the AssetDatabase
│   ├── AssetAuditResult.cs              # report + duplicate-dependency + duplicate-texture findings
│   ├── AssetOptimizer.cs               # opt-in apply pass (default + per-platform, batched, cancelable)
│   ├── SpriteAtlasBuilder.cs           # builds SpriteAtlas (+ SpriteAtlasBuildResult), bulk regenerate
│   ├── AtlasBuildSettings.cs           # atlas packing + per-platform + auto-size config
│   ├── SpriteAtlasAutoRegenerator.cs   # AssetPostprocessor: rebuild a managed atlas on member reimport
│   ├── SpriteAtlasBuildPreprocessor.cs # IPreprocessBuildWithReport: managed atlases include-in-build=off
│   ├── SpriteAtlasHygiene.cs           # clear obsolete members; list packed+directly-referenced sprites
│   ├── TextureImporterReader.cs        # TextureImporter → TextureImporterFacts (+ per-platform)
│   ├── MeshImporterReader.cs           # ModelImporter → MeshImporterFacts
│   ├── MeshAssetReader.cs              # raw Mesh asset → MeshImporterFacts (readable flag on the mesh)
│   ├── TextureContentDuplicateAnalyzer.cs # pixel-hash duplicate-texture detection
│   ├── AssetAuditReportExporter.cs     # report (+ duplicates + duplicate textures) → JSON
│   └── PFound.AssetPipeline.Editor.asmdef
└── Tests/                        # NUnit EditMode suites
    ├── TextureAuditTests.cs
    ├── MeshAuditTests.cs
    ├── DuplicateAuditTests.cs
    ├── SpriteAtlasBuilderTests.cs
    ├── TextureMemoryRulesTests.cs
    └── PFound.AssetPipeline.Tests.asmdef
```

## Relationship to ContentDelivery

AssetPipeline is a build-prep layer that sits **on top of** ContentDelivery's authoring model rather
than owning its own:

- **`AssetGroup` is the shared authoring source.** `AssetAuditor.CollectAssetPaths` /
  `SpriteAtlasBuilder.BuildFromGroup` walk `group.Entries` (`AssetEntry.Asset`) — the same groups the
  bundle build reads — so the audit inspects exactly what will ship. `AssetPipelineMenu` loads every
  group via ContentDelivery's `ContentDeliveryMenu.LoadAllGroups()`.
- **Duplicate detection is reused, not reimplemented.** `AssetAuditor.AuditAll` delegates cross-bundle
  duplicate-dependency analysis to ContentDelivery's `BundleDuplicateAnalyzer.Analyze(groups)`,
  returning its `DuplicateDependency` values inside `AssetAuditResult`.
- **Atlases feed back into authoring.** `SpriteAtlasBuilder.AddAtlasToGroup` appends an `AssetEntry`
  for a built `.spriteatlas` to a group so it ships as a bundle; sprites inside resolve at runtime via
  the existing `main[sub]` sub-asset address, so no new runtime load path is introduced.
- **Bundle naming/hashing align.** Atlas file names use `group.ResolveBundleName()`, and the atlas
  input hash is content-addressed in the same spirit as ContentDelivery's bundle hashing.

## Downstream Dependents

None within PFound — this is leaf build-tooling with `autoReferenced: false`. Consumers are a game
project's own editor scripts / CI steps (and the menu commands an author invokes by hand).

## Limitations / Known Gaps

- **No per-entry Read/Write opt-out wired.** `ReadWriteRequired` exists on the facts but the readers
  always default it to `false`, so a texture/mesh that legitimately needs Read/Write is still flagged.
- **Texture + mesh importers only.** `TextureImporter`, `ModelImporter` and raw `Mesh` assets are
  inspected; audio and other importer types are out of scope.
- **In-memory policy / atlas settings.** No authored/serialized policy or atlas-settings asset — a
  project constructs `AssetPolicy` / `AtlasBuildSettings` in editor code and passes them explicitly. The
  menu commands use `AssetPolicy.MobileDefaults()` / `AtlasBuildSettings.MobileDefaults()`; overriding
  them requires your own menu/script.

### Intentional non-goals (recorded drops, not oversights)

These legacy behaviors were deliberately not carried:

- **Per-type texture classification** (UI / 3D / VAT / splash … folder-heuristic buckets with different
  rule sets). The policy is one uniform rule set; classification is a project concern, not the framework's.
- **Hero/boss size tiers** (path-substring `_Hero`/`_Boss` → larger max size). Size is one policy cap;
  per-asset exceptions are a project override.
- **Spine handling** (Spine-atlas-folder detection + Spine-specific format path).
- **NPOT physical pixel conversion** (rewriting non-power-of-two source pixels). The policy audits/fixes
  the importer's NPOT-scale lever instead; it never repacks source pixels.
```
