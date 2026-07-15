# PFound.Utilities

Per-domain Unity utility sub-libraries (math, strings, color, pooling, filesystem, collections, and
more). Each sub-module is an **independent assembly** (`PFound.Utilities.<Name>`) — **reference only
the ones you use**; there is no umbrella assembly. **Almost everything here is pure static helpers or
plain value types** — call them directly, no setup. The handful with state or a lifecycle are noted
below and carry setup instructions in their own leaf README.

**Tier** legend: `core` = pure C#, no engine reference (`noEngineReferences: true`); `engine` =
references `UnityEngine`; `editor` = editor-only assembly.

## Module Index

| Sub-module | Purpose | Tier | Docs |
|---|---|---|---|
| AnimationCurveTools | Copy, compare, and manipulate `AnimationCurve` keyframes. | engine | [README](AnimationCurveTools/README.md) |
| ApplicationTools | Command-line args, clipboard, application version. | engine | [README](ApplicationTools/README.md) |
| CameraTools | Camera frustum / on-screen visibility tests. | engine | [README](CameraTools/README.md) |
| Collections | Enumerable and collection extension helpers. | core | [README](Collections/README.md) |
| ColorTools | Color channel ops, conversions, gradients. | engine | [README](ColorTools/README.md) |
| CryptoTools | AES encryption, hashing, hex, MurmurHash2. | core | [README](CryptoTools/README.md) |
| DataTypes | Value types/containers: `UnixTime`, `MinMaxRange`, `KeyValue`, `CircularArray`. | core | [README](DataTypes/README.md) |
| DateTimeTools | `DateTime` / `TimeSpan` extension helpers. | core | [README](DateTimeTools/README.md) |
| DebugTools | Scoped error-suppression + log rate limiting. | engine | [README](DebugTools/README.md) |
| DelegateTools | Inspect delegate invocation lists. | engine | [README](DelegateTools/README.md) |
| EditorHelpers | Editor-time asset provisioning + import button. **Stateful, see leaf.** | editor | [README](EditorHelpers/README.md) |
| EnumTools | Allocation-conscious enum helpers. | core | [README](EnumTools/README.md) |
| FileSystemTools | Path/file/directory helpers: atomic writes, safe reads, temp naming. | core | [README](FileSystemTools/README.md) |
| GameObjectTools | `GameObject`/`Transform` hierarchy, component, and factory extensions. | engine | [README](GameObjectTools/README.md) |
| Idle | `IdleWatcher` — user-inactivity detection. **Stateful, see leaf.** | engine | [README](Idle/README.md) |
| InputTools | Convenience helpers over Unity input. | engine | [README](InputTools/README.md) |
| MathTools | Scalar/interp/stats/PID core + Unity vector/rect/bounds/random layer. | core (+ engine layer) | [README](MathTools/README.md) |
| MeshTools | Mesh building and editing helpers. | engine | [README](MeshTools/README.md) |
| Messaging | In-process event channels / state switch. **Stateful, see leaf.** | engine | [README](Messaging/README.md) |
| NetworkTools | IP address extensions and hostname resolution. | core | [README](NetworkTools/README.md) |
| ParticleTools | Particle system control helpers. | engine | [README](ParticleTools/README.md) |
| Pause | Reference-counted pause coordinator with named reasons. **Stateful, see leaf.** | core | [README](Pause/README.md) |
| PhysicsTools | Rigidbody motion extensions and physics queries. | engine | [README](PhysicsTools/README.md) |
| Pooling | Object/collection pools and scoped temporaries. **See leaf.** | engine | [README](Pooling/README.md) |
| Profiling | `ProfilerMarkers` for profiler sample scopes. | engine | [README](Profiling/README.md) |
| Reflection | Member reflection helpers with caching. | core | [README](Reflection/README.md) |
| SceneTools | Scene loading/query toolkit. | engine | [README](SceneTools/README.md) |
| ScreenTools | Screen/resolution toolkit. | engine | [README](ScreenTools/README.md) |
| SerializableCollections | Inspector-editable `SerializableHashSet` / `SerializedDictionary`. | engine | [README](SerializableCollections/README.md) |
| StreamTools | Stream comparison and endianness helpers. | core | [README](StreamTools/README.md) |
| StringTools | Tag processing + string engine helpers. | engine | [README](StringTools/README.md) |
| TypeTools | Human-readable type display names. | core | [README](TypeTools/README.md) |

## Stateful sub-modules

The static-helper sub-modules need **no setup** — call the methods. The stateful ones own an instance
the consumer creates and drives; none require a MonoBehaviour or a scene object (fail-fast: they throw
on bad input rather than defending against it). Setup/wiring for each lives in its leaf README:
**Idle**, **Messaging**, **Pause**, **Pooling**, and **EditorHelpers** (editor-only).

## Layout

Each sub-module: `<Name>/` (or `<Name>/Runtime/`) with `PFound.Utilities.<Name>.asmdef`, plus
`Tests/` where present. A few are layered (`MathTools` = Core + Unity, `StringTools` = Engine +
Runtime); `EditorHelpers` is `.Editor`-only.
