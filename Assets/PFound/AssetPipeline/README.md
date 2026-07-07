# PFound.AssetPipeline

Editor-only build-prep tooling: audit textures/meshes against an import policy, apply the fixes, and
build sprite atlases — over the `AssetGroup`s that `PFound.ContentDelivery` already uses to ship
content. The policy/evaluation layer is engine-free pure C#; the editor layer reads and rewrites Unity
importers.

## Quick reference

Menu commands under `PFound/Asset Pipeline/`:

| Menu path | What it does |
|-----------|--------------|
| `Audit Assets (All Groups)` | Read-only. Logs policy + duplicate violations, writes `<project>/AssetAudit/asset-audit.json`. |
| `Optimize Assets (Apply Fixes — All Groups)` | Audits, then (after a confirm dialog) rewrites importer settings and reimports. |
| `Build Sprite Atlas (Selected Asset Group)` | Builds a `.spriteatlas` from the selected `AssetGroup` and stamps its input content hash. |

The policy is in-memory (`AssetPolicy.MobileDefaults()` or `new AssetPolicy { ... }`) — no config asset.

## Dependencies

`PFound.ContentDelivery` (Editor assembly only; the Core assembly has none). Editor-only tooling — no
runtime component, no DI registration.

## Docs

Deep reference: [MODULE.md](MODULE.md).
