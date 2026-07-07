# PFound.RemoteResourceCache

A three-tier cache for remote binary resources — **memory → disk → remote** — with single-flight
loads, ref-count pinning, retry-with-backoff, disk TTL, and pluggable eviction. The cache core is
engine-free pure C# (`ResourceCache<T>`); a thin Unity adapter wires it to `Texture2D`.

## Quick reference

```csharp
// Pure library: construct one cache per resource type and call it. No scene object, MonoBehaviour, or DI.
string root = Path.Combine(Application.persistentDataPath, "cache");
var disk    = DiskCache.FromPolicy(new FileBlobStore(root), Path.Combine(root, "index.bin"), CachePolicy.Default);
var cache   = new ResourceCache<Texture2D>(
    myTransport,                            // IResourceTransport — the remote-tier seam
    disk,
    TextureResourceDecoders.DecodeTexture); // byte[] -> Texture2D

ResourceResult<Texture2D> result = await cache.GetAsync("https://cdn.example.com/hero.png");
if (result.Success) icon.texture = result.Value;   // result.Tier says which tier served it

// Textures with the settled BestHTTP transport auto-wired (PFOUND_BESTHTTP):
var textures = RemoteTextureCache.Create();
```

## Dependencies

Core is dependency-free engine-free C#; the optional BestHTTP transport is gated behind the
`PFOUND_BESTHTTP` scripting define.

## Docs

Deep reference: [MODULE.md](MODULE.md).
</content>
