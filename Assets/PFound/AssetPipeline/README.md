# PFound.AssetPipeline

Editor-only asset-import quality tooling: audit textures/meshes against an import policy, apply the
fixes, and build sprite atlases — over the `AssetGroup`s that `PFound.ContentDelivery` already uses
to ship content. The policy/evaluation layer is engine-free pure C# (unit-testable); the editor
layer reads Unity importers and rewrites their settings.

## Model

- **Policy** — an `AssetPolicy` (plain C# class, not a ScriptableObject) of import rules: max
  texture size, require-compressed, disallow crunch / RGBA32 / read-write / sprite mipmaps, minimum
  mesh compression, etc. `AssetPolicy.MobileDefaults()` is the ready-made baseline.
- **Facts** — the editor readers (`TextureImporterReader`, `MeshImporterReader`) flatten a Unity
  importer into an engine-free `TextureImporterFacts` / `MeshImporterFacts`, so the evaluator reads
  only data — never a `UnityEditor` type — and runs under `mono`/`csc`.
- **Violations** — `AssetPolicyEvaluator` compares facts to policy and produces `PolicyViolation`
  values (`AssetPath`, `ViolationCode`, `Detail`), collected into an `AssetAuditReport`
  (`AssetsScanned`, `Violations`, `ViolationCount`, `IsClean`, `CountOf(code)`).

## Public API

**Core (`PFound.AssetPipeline.Core`, engine-free):**
- `AssetPolicy` — public rule fields; `AssetPolicy.MobileDefaults()`, `string Describe()`.
- `AssetPolicyEvaluator` (static) — `EvaluateTexture(facts, policy, into)`,
  `EvaluateMesh(facts, policy, into)`, `List<PolicyViolation> Evaluate(textures, meshes, policy)`.
- `PolicyViolation` / `ViolationCode`, `AssetAuditReport`, `TextureImporterFacts` /
  `MeshImporterFacts`, `AtlasInputHasher.Compute(memberIdentities)` (order-independent 32-hex hash
  for change detection).

**Editor (`PFound.AssetPipeline.Editor`, callable from your own editor scripts):**
- `AssetAuditor` (static) — `Audit(groups, policy)`, `AuditAll(groups, policy)` (+ duplicate
  dependencies), `AuditPaths(paths, policy)`, `CollectAssetPaths(groups)`.
- `AssetOptimizer` (static) — `int Apply(report, policy)` / `Apply(violations, policy)`: rewrites
  importer settings and reimports; returns the number of assets changed.
- `SpriteAtlasBuilder` (static) — `Build(atlasPath, spritePaths)`, `BuildFromFolder(...)`,
  `BuildFromGroup(atlasPath, group)`, `IsUpToDate(...)`, `GetStoredContentHash(...)`,
  `AddAtlasToGroup(group, atlasPath, address)`.
- `AssetAuditReportExporter` (static) — `ToJson(report, ...)`, `Write(report, duplicates,
  directory)` (`ReportFileName = "asset-audit.json"`).

## Setup / wiring

**Editor-only tooling — no runtime component, no scene object, no `DontDestroyOnLoad`, no DI
registration.** The `Editor` assembly is `includePlatforms: ["Editor"]`; the `Core` assembly is
`noEngineReferences:true`. Nothing initializes at load — you drive it from menu commands or your own
editor scripts.

It operates on **`AssetGroup` assets from `PFound.ContentDelivery`** — AssetPipeline does not define
its own config asset. The **policy is in-memory**: hard-code `AssetPolicy.MobileDefaults()` or
`new AssetPolicy { MaxTextureSize = 1024, ... }` and pass it explicitly on each call. There is no
global/authored policy asset to place.

Menu commands (under `PFound/Asset Pipeline/`):
- **Audit Assets (All Groups)** — read-only; logs violations and writes
  `<project>/AssetAudit/asset-audit.json`.
- **Optimize Assets (Apply Fixes — All Groups)** — audits, then (after a confirm dialog) rewrites
  importer settings and reimports.
- **Build Sprite Atlas (Selected Asset Group)** — select an `AssetGroup` in the Project window
  first; builds `<groupDir>/<groupName>_atlas.spriteatlas` and stamps its input content hash.

From an editor script:

```csharp
using PFound.AssetPipeline.Core;
using PFound.AssetPipeline.Editor;

var policy = AssetPolicy.MobileDefaults();        // or new AssetPolicy { MaxTextureSize = 1024 }
AssetAuditReport report = AssetAuditor.Audit(groups, policy);
if (!report.IsClean)
    Debug.Log($"{report.ViolationCount} violations\n{report}");   // let a genuinely-bad path throw
int changed = AssetOptimizer.Apply(report, policy);               // opt-in mutation
AssetAuditReportExporter.Write(report, duplicates: null, directory: "AssetAudit");
```

Typical flow: author assets into `AssetGroup`s (ContentDelivery) → **Audit** → fix (manually or
**Optimize**) → **Build Sprite Atlas** where needed → **Build Content** (ContentDelivery).

## Testing

The engine-free core has a standalone csc/mono runner (`Core/Tests/Program.cs`); the editor tooling
has EditMode suites under `Tests/` (`TextureAuditTests`, `MeshAuditTests`, `DuplicateAuditTests`,
`SpriteAtlasBuilderTests`, `TextureMemoryRulesTests`).

## Layout

- `Core/Runtime/` — policy, evaluator, facts, violations, hashing. Assembly
  `PFound.AssetPipeline.Core` (`noEngineReferences:true`).
- `Editor/` — auditor, optimizer, atlas builder, importer readers, menu, exporter. Assembly
  `PFound.AssetPipeline.Editor` (Editor-only; references ContentDelivery for `AssetGroup`).

Part of the PFound modular Unity foundation.
</content>
