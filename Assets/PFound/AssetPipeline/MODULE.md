# AssetPipeline

## Purpose

Editor-only build-prep tooling that audits authored textures and meshes against an import policy,
optionally applies the fixes, and builds sprite atlases. It works over the `AssetGroup`s that
`PFound.ContentDelivery` already uses to ship content, so "is this asset imported well?" and "is it
wastefully duplicated across bundles?" are answered from the same authoring source the bundle build
reads. The policy/decision layer is engine-free pure C# (unit-testable); the editor layer reads and
rewrites Unity importer settings.

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
  atlas API).
- **Third-party / scripting defines:** none.

## Key Types

**Core (`PFound.AssetPipeline.Core`, engine-free):**
- `AssetPolicy` — the set of import rules (plain C# class, one field per knob).
- `AssetPolicyEvaluator` — the pure decision layer: facts + policy → violations.
- `TextureImporterFacts` / `MeshImporterFacts` — one importer's settings as plain data.
- `TextureCompressionLevel` / `NpotScale` / `MeshCompressionLevel` — engine-free mirrors of the Unity
  importer enums.
- `PolicyViolation` (readonly struct) + `ViolationCode` (enum) — one breach: asset path, rule, detail.
- `AssetAuditReport` — a scan result: policy description, assets scanned, violations.
- `AtlasInputHasher` — order-independent SHA-256 digest of an atlas input set (change detection).

**Editor (`PFound.AssetPipeline.Editor`):**
- `AssetAuditor` — audit pass over `AssetGroup`s; reports, never mutates.
- `AssetAuditResult` — full-audit bundle: the per-asset `AssetAuditReport` + cross-bundle
  `DuplicateDependency` findings (from ContentDelivery).
- `AssetOptimizer` — the opt-in apply pass: rewrites importer settings and reimports.
- `SpriteAtlasBuilder` + `SpriteAtlasBuildResult` — builds a `SpriteAtlas` from a group/folder/paths.
- `TextureImporterReader` / `MeshImporterReader` — translate a Unity importer into engine-free facts.
- `AssetAuditReportExporter` — serializes a report (+ duplicates) to JSON.
- `AssetPipelineMenu` — the three `MenuItem` entry points.

## Public API

**`AssetPolicy` (Core):**
- Rule fields: `int MaxTextureSize` (default 2048), `bool RequireCompressed`,
  `bool DisallowUncompressedRgba32`, `bool DisallowCrunch`, `bool RequirePowerOfTwoOrNpotScale`,
  `bool DisallowTextureReadWrite`, `bool DisallowSpriteMipmaps`, `bool DisallowMeshReadWrite`,
  `MeshCompressionLevel MinMeshCompression`.
- `static AssetPolicy MobileDefaults()` — the shipped baseline (the default-constructed policy).
- `string Describe()` — one-line summary of the active rules (embedded in the report).

**`AssetPolicyEvaluator` (Core, static):**
- `void EvaluateTexture(TextureImporterFacts f, AssetPolicy policy, List<PolicyViolation> into)`
- `void EvaluateMesh(MeshImporterFacts f, AssetPolicy policy, List<PolicyViolation> into)`
- `List<PolicyViolation> Evaluate(IEnumerable<TextureImporterFacts> textures, IEnumerable<MeshImporterFacts> meshes, AssetPolicy policy)`

**`AssetAuditReport` (Core):** `int ViolationCount`, `bool IsClean`, `int CountOf(ViolationCode)`,
`ToString()`; fields `PolicyDescription`, `AssetsScanned`, `IReadOnlyList<PolicyViolation> Violations`.

**`AtlasInputHasher` (Core, static):** `string Compute(IEnumerable<string> memberIdentities)` — 32 hex
chars, order-independent.

**`AssetAuditor` (Editor, static):**
- `AssetAuditReport Audit(IReadOnlyList<AssetGroup> groups, AssetPolicy policy)`
- `AssetAuditResult AuditAll(IReadOnlyList<AssetGroup> groups, AssetPolicy policy)` (+ duplicate deps)
- `AssetAuditReport AuditPaths(IEnumerable<string> assetPaths, AssetPolicy policy)` — the testable seam
- `List<string> CollectAssetPaths(IReadOnlyList<AssetGroup> groups)`

**`AssetOptimizer` (Editor, static):**
- `int Apply(AssetAuditReport report, AssetPolicy policy)`
- `int Apply(IReadOnlyList<PolicyViolation> violations, AssetPolicy policy)` — returns assets changed.

**`SpriteAtlasBuilder` (Editor, static):**
- `SpriteAtlasBuildResult Build(string atlasPath, IReadOnlyList<string> spriteAssetPaths)`
- `SpriteAtlasBuildResult BuildFromFolder(string atlasPath, string folder)`
- `SpriteAtlasBuildResult BuildFromGroup(string atlasPath, AssetGroup group)`
- `string ComputeContentHash(IEnumerable<string> spriteAssetPaths)`
- `string GetStoredContentHash(string atlasPath)`
- `bool IsUpToDate(string atlasPath, IReadOnlyList<string> spriteAssetPaths)`
- `AssetEntry AddAtlasToGroup(AssetGroup group, string atlasPath, string address)`

**`AssetAuditReportExporter` (Editor, static):** `const string ReportFileName = "asset-audit.json"`;
`string ToJson(AssetAuditReport report, IReadOnlyList<DuplicateDependency> duplicates = null, bool prettyPrint = true)`;
`string Write(AssetAuditReport report, IReadOnlyList<DuplicateDependency> duplicates, string directory)`.

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
| `PFound/Asset Pipeline/Audit Assets (All Groups)` | Read-only. Loads every group via `ContentDeliveryMenu.LoadAllGroups()`, runs `AssetAuditor.AuditAll(...)` with `AssetPolicy.MobileDefaults()`, logs the result, and writes `asset-audit.json` under `<project>/AssetAudit/`. |
| `PFound/Asset Pipeline/Optimize Assets (Apply Fixes — All Groups)` | Audits, and if not clean, shows a confirm dialog, then calls `AssetOptimizer.Apply(...)` to rewrite importer settings and reimport. Opt-in mutation, never a side effect of the audit. |
| `PFound/Asset Pipeline/Build Sprite Atlas (Selected Asset Group)` | Requires an `AssetGroup` selected in the Project window (warns otherwise). Builds `<groupDir>/<group.ResolveBundleName()>_atlas.spriteatlas` via `SpriteAtlasBuilder.BuildFromGroup(...)` and stamps its input content hash. |

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
│   │   ├── AssetPolicyEvaluator.cs       # facts + policy → violations (pure)
│   │   ├── ImporterFacts.cs              # TextureImporterFacts, MeshImporterFacts
│   │   ├── PolicyViolation.cs            # PolicyViolation struct + ViolationCode enum
│   │   ├── AssetAuditReport.cs           # scan result (policy desc, count, violations)
│   │   ├── AtlasInputHasher.cs           # order-independent SHA-256 input digest
│   │   └── PFound.AssetPipeline.Core.asmdef
│   └── Tests/                    # standalone csc/mono runner
│       ├── Program.cs
│       └── PFound.AssetPipeline.Core.Tests.asmdef
├── Editor/                       # PFound.AssetPipeline.Editor (Editor-only)
│   ├── AssetPipelineMenu.cs             # the 3 MenuItem entry points
│   ├── AssetAuditor.cs                  # audit pass over AssetGroups
│   ├── AssetAuditResult.cs              # report + duplicate-dependency findings
│   ├── AssetOptimizer.cs               # opt-in apply pass (rewrites importers)
│   ├── SpriteAtlasBuilder.cs           # builds SpriteAtlas + SpriteAtlasBuildResult
│   ├── TextureImporterReader.cs        # TextureImporter → TextureImporterFacts
│   ├── MeshImporterReader.cs           # ModelImporter → MeshImporterFacts
│   ├── AssetAuditReportExporter.cs     # report (+ duplicates) → JSON
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

- **Default-platform only.** The texture reader/optimizer inspect and write the importer's top-level
  (default-platform) settings. Per-platform overrides (iOS/Android via
  `SetPlatformTextureSettings`) are reliable but a deliberately deferred capability — not yet exposed
  by the policy.
- **No per-entry Read/Write opt-out wired.** `ReadWriteRequired` exists on the facts but the readers
  always default it to `false`, so a texture/mesh that legitimately needs Read/Write is still flagged.
- **Two importer kinds.** Only `TextureImporter` and `ModelImporter` are inspected; audio and other
  importer types are out of scope.
- **In-memory policy only.** No authored/serialized policy asset — a project hard-codes or constructs
  the policy in editor code and passes it explicitly. The menu commands use
  `AssetPolicy.MobileDefaults()`; overriding them requires your own menu/script.
```
