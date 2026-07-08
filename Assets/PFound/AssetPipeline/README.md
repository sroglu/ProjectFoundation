# PFound.AssetPipeline

Editor-only build-prep tooling: audit textures/meshes against an import policy (default + per-platform
iOS/Android), apply the fixes, and build/maintain sprite atlases — over the `AssetGroup`s that
`PFound.ContentDelivery` already uses to ship content. The audit is gated by a reference graph
(unreferenced assets skipped, atlas members exempt from format rules). The policy/evaluation layer is
engine-free pure C#; the editor layer reads and rewrites Unity importers.

## Quick reference

Menu commands under `PFound/Asset Pipeline/`:

| Menu path | What it does |
|-----------|--------------|
| `Audit Assets (All Groups)` | Read-only. Logs policy + duplicate-dependency + duplicate-texture findings, writes `<project>/AssetAudit/asset-audit.json`. |
| `Optimize Assets (Apply Fixes — All Groups)` | Audits, then (after a confirm dialog) rewrites default + per-platform importer settings and reimports — batched, with cancelable progress. |
| `Build Sprite Atlas (Selected Asset Group)` | Builds a `.spriteatlas` from the selected `AssetGroup` (packing + per-platform + auto page-size) and stamps its input content hash. |
| `Regenerate All Sprite Atlases` | Rebuilds every out-of-date managed atlas (cancelable). |
| `Clear Obsolete Atlas Sprites` | Drops packables no longer resolving to a referenced sprite. |
| `List Packed + Directly-Referenced Sprites` | Flags sprites loaded twice (atlas + direct reference). |
| `Find Content-Identical Duplicate Textures` | Groups distinct texture files sharing a pixel content hash. |

Two callbacks run automatically: a postprocessor regenerates a managed atlas when a member is reimported;
a build preprocessor disables include-in-build on managed atlases so their sprites ship once (via bundle).

The policy is in-memory (`AssetPolicy.MobileDefaults()` or `new AssetPolicy { ... }`); atlas settings are
`AtlasBuildSettings.MobileDefaults()` or a custom instance — no config asset.

## Dependencies

`PFound.ContentDelivery` (Editor assembly only; the Core assembly has none). Editor-only tooling — no
runtime component, no DI registration.

## Docs

Deep reference: [MODULE.md](MODULE.md).
