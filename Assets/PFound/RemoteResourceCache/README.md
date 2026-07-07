# PFound.RemoteResourceCache

A three-tier cache for remote binary resources: **memory → disk → remote**, with single-flight
loads, ref-count pinning, retry with backoff, TTL, and pluggable eviction. The core is engine-free
pure C#; a thin Unity adapter wires it to `Texture2D`.

## Model

- **Memory tier** — decoded values held in RAM, bounded by count and/or bytes, evicted by
  `EvictionStrategy` (`Lru`/`Lfu`/`TimeBased`). Pinned entries are exempt from eviction.
- **Disk tier** — raw bytes persisted via an `IBlobStore`, with a metadata index, TTL, and its own
  eviction bounds.
- **Remote tier** — fetched through a pluggable `IResourceTransport`; transient faults are retried
  per `RetryPolicy`, terminal faults (not-found, decode) fail immediately.
- **Single-flight** — concurrent requests for one key share a single in-flight load.

## Public API

**`ResourceCache<T>`** (`PFound.RemoteResourceCache.Core`, generic over the decoded type):

- `Task<ResourceResult<T>> GetAsync(string key, CancellationToken ct = default)` — fetch from the
  nearest tier; no pinning.
- `Task<ResourceResult<T>> AcquireAsync(string key, CancellationToken ct = default)` — like `GetAsync`
  but pins the result in memory (ref-count +1); balance with `Release(key)`.
- `void Release(string key)` — decrement the pin count; at zero the entry becomes evictable.
- `bool TryGet(string key, out T value)` — synchronous memory-tier-only peek (never touches
  disk/network).
- `Task PreloadAsync(IEnumerable<string> keys, CancellationToken ct = default)` — concurrently warm
  many keys.
- `bool RemoveFromMemory(string key)`, `void ClearMemory()`, `Task ClearDiskAsync()`,
  `void FlushDisk()` (persist the disk index — call on shutdown so LFU/TimeBased ordering survives).
- Diagnostics: `int MemoryCount`, `long MemoryBytes`, `int DiskCount`, `long DiskBytes`,
  `bool InMemory(key)`, `bool IsPinned(key)`.

**Result:** `ResourceResult<T>` — `bool Success`, `T Value`, `CacheTier Tier`
(`Memory`/`Disk`/`Remote`/`None`), `ResourceFailure Failure`. `ResourceFailure` carries a
`ResourceFailureKind` (`NotFound`/`Network`/`Decode`/`RetryExhausted`), message, and cause.

**Policies:** `CachePolicy` (memory/disk `Max*Count`/`Max*Bytes`, `MemoryEviction`/`DiskEviction`,
`Ttl`, `DiskFlushInterval`, per-key `Cacheable` gate; `CachePolicy.Default`) and `RetryPolicy`
(`MaxAttempts`, `BaseBackoff`, `BackoffMultiplier`, explicit `Delays`; `RetryPolicy.Default` = 3
attempts / 200ms exponential, `RetryPolicy.None`).

**Seams:** `IResourceTransport.FetchAsync(key, ct)` (the key *is* the URL; throw
`ResourceNotFoundException` for a terminal miss, any other exception is treated as transient),
`IBlobStore` (disk persistence; `FileBlobStore` is the file-backed implementation),
`ResourceDecoder<T>` / `ResourceSizer<T>` delegates.

**Unity adapter** (`PFound.RemoteResourceCache`): `RemoteTextureCache.Create(transport = null,
diskSubdirectory = "RemoteResourceCache", policy = null, retry = null)` returns a
`ResourceCache<Texture2D>` wired to a `FileBlobStore` under `Application.persistentDataPath`, the
`TextureResourceDecoders.DecodeTexture` decoder, a 4-bytes/pixel sizer, and a `Texture2D` disposer.
`TextureResourceDecoders` also exposes `DecodeSprite` / `SizeOfSprite` for reuse.

## Setup / wiring

Pure library — **no scene object, MonoBehaviour, or DI registration.** You `new`/create a cache and
call it; construct one per resource type (usually once, held by whatever owns the resource lifetime)
since each instance owns its own memory + disk tiers.

**Transport is the one thing you must supply.** The core takes an `IResourceTransport` in its
constructor. The Unity adapter instead reads a process-wide default from
`RemoteResourceCacheDefaults.Transport` when you don't pass one:

- **BestHTTP (settled default).** Add `PFOUND_BESTHTTP` to Scripting Define Symbols. The
  `PFound.RemoteResourceCache.BestHttp` assembly (define-constrained on `PFOUND_BESTHTTP`) then
  compiles `BestHttpDefaultTransport`, which auto-registers `BestHttpResourceTransport` into
  `RemoteResourceCacheDefaults.Transport` via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`.
  Nothing else to wire — `RemoteTextureCache.Create()` picks it up.
- **Custom transport.** Pass your own `IResourceTransport` to `Create(transport: ...)` or to the
  `ResourceCache<T>` constructor, or assign `RemoteResourceCacheDefaults.Transport` yourself before
  the first fetch. If neither a passed nor a registered transport exists, the first fetch throws
  (fail-fast — no defensive null guard).

```csharp
// Texture cache with the registered (BestHTTP) transport + defaults:
var cache = RemoteTextureCache.Create();
var result = await cache.GetAsync("https://cdn.example.com/hero.png");
if (result.Success) icon.texture = result.Value;   // result.Tier says which tier served it
else Debug.LogError($"{result.Failure.Kind}: {result.Failure.Message}");

// Pin something that must stay resident, then release:
var pinned = await cache.AcquireAsync(url);
// ... use pinned.Value ...
cache.Release(url);

cache.FlushDisk();   // on shutdown, so eviction ordering persists to next session
```

Fully manual wiring (any resource type):

```csharp
var store = new FileBlobStore(Path.Combine(Application.persistentDataPath, "cache"));
var disk  = new DiskCache(store, Path.Combine(store.Root, "index.bin"));
var cache = new ResourceCache<Texture2D>(
    myTransport, disk, TextureResourceDecoders.DecodeTexture,
    new CachePolicy { MaxMemoryBytes = 50_000_000, MaxDiskBytes = 500_000_000 },
    RetryPolicy.Default, TextureResourceDecoders.SizeOfTexture,
    disposer: tex => Object.Destroy(tex));
```

## Testing

The engine-free core has a standalone csc/mono runner (`Core/Tests/Program.cs`, asmdef
`noEngineReferences:true`); the Unity adapter is covered by EditMode
(`Tests/RemoteResourceCacheAdapterTests.cs`) and PlayMode
(`Tests/PlayMode/RemoteTextureDecodePlayModeTests.cs`) suites.

## Layout

- `Core/Runtime/` — engine-free cache, tiers, policies, interfaces. Assembly
  `PFound.RemoteResourceCache.Core` (`noEngineReferences:true`).
- `Runtime/` — Unity adapter (`RemoteTextureCache`, decoders, defaults). Assembly
  `PFound.RemoteResourceCache`.
- `BestHttp/` — optional BestHTTP transport, define-constrained on `PFOUND_BESTHTTP`. Assembly
  `PFound.RemoteResourceCache.BestHttp`.

All assemblies are `autoReferenced:false`.

Part of the PFound modular Unity foundation.
</content>
