# PFound.Compression

Engine-free compression codecs for PFound. Today it ships a single clean-room `Lzma` static codec
(future codecs slot in as sibling classes in the same namespace) — no scene, MonoBehaviour, DI, or
lifecycle setup.

## Quick reference

```csharp
using PFound.Compression;

byte[] packed   = Lzma.Compress(payload);      // build/content-authoring time
byte[] original = Lzma.Decompress(packed);     // runtime, on a downloaded blob

Lzma.DecompressFile(bundlePath, cachePath);    // streamed path -> path, low peak memory
byte[] s = Lzma.CompressString("hello");       // UTF-8 string helper
```

`Decompress` inverts `Compress` from this same codec (the only property PFound needs — the editor
compresses, the runtime decompresses). Also: explicit dictionary size (`Compress(data, size)`),
stream/file/string helpers both directions, `ReadUncompressedLength`. Full surface in MODULE.md.

## Dependencies

None — `PFound.Compression` has an empty `references` list, `autoReferenced:false`,
`noEngineReferences:true`. Add it to your asmdef `references`.

## Docs

Deep reference: [MODULE.md](MODULE.md) — full API, `.lzma` stream layout, pooled-decode model, and
limitations. Codec source: `Runtime/Lzma.cs`.
