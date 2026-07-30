# Compression

> **Module group — Content & Assets.** Sibling modules in this group: `ContentDelivery`, `AssetPipeline`, `RemoteResourceCache`. Grouped by purpose — see the catalog `Assets/PFound/README.md` and each module's **Dependencies** for exact edges.

## Purpose

Engine-free compression codecs for PFound. It ships one `Lzma` codec — a static
library that compresses at build/content-authoring time and decompresses on the runtime hot path
(e.g. a downloaded content blob). `Decompress` inverts `Compress` from this same codec; that
round-trip property is the only guarantee the module makes.

Consumers resolve a codec through the pluggable `ICompressionCodec` / `CompressionCodecs` surface
rather than calling a concrete class, so the algorithm is swappable at runtime with no consumer
changes. `LzmaCodec` is the zero-dependency built-in registered as the default.

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `PFound.Compression` | `Runtime/PFound.Compression.asmdef` | `noEngineReferences: true`, `autoReferenced: false` — add it to a consumer's asmdef `references` explicitly |
| `PFound.Compression.Deflate` | `Providers/Deflate/PFound.Compression.Deflate.asmdef` | drop-in Deflate provider; `references: ["PFound.Compression"]`, `autoReferenced: true`, self-registers at play. Uses `UnityEngine` only for the `RuntimeInitializeOnLoadMethod` hook |
| `PFound.Compression.Tests` | `Tests/PFound.Compression.Tests.asmdef` | round-trip suite, `noEngineReferences: true` |

## Dependencies

None — empty `references` list, no PFound module, no third-party package, no scripting define. BCL
only.

## Key Types

`PFound.Compression` namespace:

- **`Lzma`** (static) — the codec. LZMA1 range coder over a literal/match model with slot-coded
  lengths/distances, emitting the classic `.lzma`/"alone" stream layout. All public entry points;
  the encoder, decoder, length coder, sliding output window, and match finder are private nested
  implementation detail.
- **`ICompressionCodec`** (interface) — the swappable `byte[]` round-trip surface: a stable
  `string Id`, `byte[] Compress(byte[])`, and `byte[] Decompress(byte[])`. The framework depends on
  this, not on a concrete algorithm.
- **`LzmaCodec`** (`sealed : ICompressionCodec`, `Id = "lzma"`) — zero-dependency built-in that
  forwards to the static `Lzma`. Registered as the default.
- **`CompressionCodecs`** (static) — registry keyed by `Id`. Holds the `Default`, and lets a
  provider `Register` / `SetDefault` / `Get` / `TryGet` codecs — by id string or by the typed
  `CompressionMethod`.
- **`CompressionMethod`** (enum) — typed selector for a built-in codec (`Lzma`), an allocation-free
  alternative to the raw id string. Grows one value per built-in codec.
- **`CompressionCodecExtensions`** (static) — codec-agnostic UTF-8 `CompressString` /
  `DecompressString` extension methods over any `ICompressionCodec`.

## Public API

`Lzma` (all static). Public methods throw `ArgumentNullException` on a null argument (library
boundary, not defensive guarding).

**Bytes**
```csharp
byte[] Compress(byte[] data);                       // default 4 MiB match window / dict size
byte[] Compress(byte[] data, int dictionarySize);   // 256 KiB..4 MiB; smaller trades ratio for footprint
byte[] Decompress(byte[] data);                     // fresh array the caller owns
void   DecompressInto(byte[] data, Stream destination);
void   DecompressInto(Stream source, Stream destination);   // streams both ends; runtime hot path
long   ReadUncompressedLength(byte[] data);         // length declared in the header
```

**Streams**
```csharp
void Compress(Stream source, Stream destination);   // drains source to end, writes the .lzma stream
```

**Files** (both directions)
```csharp
void   CompressFile(string src, string dst);
void   CompressFile(string src, string dst, int dictionarySize);
byte[] CompressFile(string src);                    // path -> bytes
void   CompressToFile(byte[] data, string dst);     // bytes -> path
void   DecompressFile(string src, string dst);      // path -> path, streams both ends
byte[] DecompressFile(string src);                  // path -> bytes
void   DecompressToFile(byte[] data, string dst);   // bytes -> path
```

**UTF-8 strings**
```csharp
byte[] CompressString(string text);
byte[] CompressString(string text, int dictionarySize);
string DecompressString(byte[] data);
```

**Constants**
```csharp
const int DefaultDictionarySize = 1 << 22;   // 4 MiB — default and maximum
const int MinDictionarySize     = 1 << 18;   // 256 KiB — minimum accepted
```
`Compress(data, dictionarySize)` throws `ArgumentOutOfRangeException` outside that range.

### Pluggable codecs

`ICompressionCodec` — the swappable surface:
```csharp
string Id { get; }                 // stable id used to register/resolve (e.g. "lzma")
byte[] Compress(byte[] data);      // -> self-describing blob
byte[] Decompress(byte[] data);    // inverts Compress
```

`CompressionCodecs` (static registry, id-keyed, case-insensitive):
```csharp
ICompressionCodec Default { get; }                 // used when no id is requested (LzmaCodec by default)
void Register(ICompressionCodec codec);            // add or replace under codec.Id
bool TryGet(string id, out ICompressionCodec c);
ICompressionCodec Get(string id);                  // throws KeyNotFoundException if unregistered
void SetDefault(string id);                        // promote a registered codec to Default
IEnumerable<string> RegisteredIds { get; }

// Typed overloads — map a CompressionMethod to its codec id and reuse the string path, so the
// typed and string calls resolve the same instance. The map is an explicit switch (not ToString():
// no allocation, rename-safe) and throws ArgumentOutOfRangeException on an unmapped value.
ICompressionCodec Get(CompressionMethod method);
bool TryGet(CompressionMethod method, out ICompressionCodec c);
void SetDefault(CompressionMethod method);
```

`CompressionMethod` — typed codec selector; add one value per blessed codec:
```csharp
enum CompressionMethod { Lzma, Deflate }   // Lzma -> "lzma", Deflate -> "deflate"
```
A codec registered only at runtime (no enum value) stays reachable through the string overloads. A
value whose provider has not registered yet — e.g. `CompressionMethod.Deflate` before the Deflate
provider assembly loads — throws `KeyNotFoundException` on `Get`, which is the intended contract: the
enum lists first-party codecs, but resolving one still requires its provider to be present.

`LzmaCodec` (`Id = "lzma"`) is registered as `Default` from the registry's static constructor, so
`CompressionCodecs.Default`, `CompressionCodecs.Get("lzma")`, and
`CompressionCodecs.Get(CompressionMethod.Lzma)` all resolve the same built-in instance with no setup.

### Codec-agnostic UTF-8 strings

`CompressionCodecExtensions` — lets a compressed-`byte[]` ⇄ UTF-8 `string` flow (e.g. a compressed
text field decompressed to a string) run through *any* `ICompressionCodec`, not only the static
`Lzma` helper:
```csharp
byte[] CompressString(this ICompressionCodec codec, string text);   // UTF-8 encode -> codec.Compress
string DecompressString(this ICompressionCodec codec, byte[] data); // codec.Decompress -> UTF-8 decode
```
Both throw `ArgumentNullException` on a null codec or argument (library boundary).

## Model

- **Stream layout.** 1 byte packed props (`lc,lp,pb`) · 4 bytes little-endian dictionary size · 8
  bytes little-endian uncompressed length · range-coded payload. `ReadUncompressedLength` reads the
  length field at offset 5.
- **Dictionary size is honored on decode.** The output/back-reference window is sized from the
  stream's own header dict field, capped at the uncompressed length (no distance can reach past byte
  0). So this decoder handles any conforming LZMA-alone stream, not just 4 MiB-window ones — and a
  huge declared dict (some encoders write `0xFFFFFFFF`) never over-allocates.
- **Pooled, low-allocation decode.** Decoders are pooled; each reuses its probability model arrays
  and a single sliding output window (which doubles as the back-reference dictionary and grows to
  the largest size seen, reused rather than reallocated for an equal-or-smaller next stream).
  Concurrent multi-blob decompression thus allocates neither the model arrays nor a per-blob output
  buffer; peak memory is bounded to the window, never the whole payload. Concurrency is capped by
  the caller, which bounds how many windows are resident at once.

## Setup / wiring

Pure static library — reference the `PFound.Compression` assembly and call the methods directly. No
scene object, MonoBehaviour host, lifecycle, ScriptableObject, or DI registration. Because the
assembly is `autoReferenced: false`, a consumer assembly must add **`PFound.Compression`** to its
asmdef `references` first.

```csharp
using PFound.Compression;

byte[] packed   = Lzma.Compress(payload);    // build/content-authoring time
byte[] original = Lzma.Decompress(packed);   // runtime, on a downloaded blob

Lzma.DecompressFile(bundlePath, cachePath);  // streamed path -> path on the runtime hot path
```

Prefer the registry when the algorithm should stay swappable:

```csharp
var codec       = CompressionCodecs.Default;         // or CompressionCodecs.Get("lzma")
byte[] packed   = codec.Compress(payload);
byte[] original = codec.Decompress(packed);
```

**Extension point — the provider pattern.** A codec plugs in with no consumer or core changes. A
provider ships in **its own asmdef that references `PFound.Compression`** and self-registers, so its
dependency never leaks into the core module. Having the provider assembly in the project is all that
is required — it registers itself at play via `RuntimeInitializeOnLoadMethod`:

```csharp
public sealed class DeflateCodec : ICompressionCodec { public string Id => "deflate"; /* ... */ }

public static class DeflateCodecInstaller
{
    public static void Install() => CompressionCodecs.Register(new DeflateCodec());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Auto() => Install();   // auto at play; call Install() explicitly for edit-mode/tests
}
```

Consumers then simply *choose* a registered codec — nothing else changes:

```csharp
var codec     = CompressionCodecs.Get(CompressionMethod.Deflate);  // or Get("deflate")
byte[] packed = codec.Compress(payload);
CompressionCodecs.SetDefault(CompressionMethod.Deflate);           // optional: make it the default
```

**Built-in example — `PFound.Compression.Deflate`.** A real, zero-external-dependency provider built
on the BCL `System.IO.Compression.DeflateStream` (RFC 1951), living in `Providers/Deflate/`. It
demonstrates the whole flow end-to-end: separate referencing asmdef → `RuntimeInitializeOnLoadMethod`
self-registration → resolvable by id `"deflate"` or `CompressionMethod.Deflate` (same instance) →
byte-exact round trip → selectable as `Default`. The built-in `LzmaCodec` stays the zero-dependency
core default; Deflate is opt-in by having the provider assembly present.

**Third-party-backed providers.** A provider that carries a *third-party* dependency — e.g. one
wrapping a reference LZMA SDK, or an LZ4/Zstd native library — follows the same pattern but
additionally **define-gates its asmdef** (`defineConstraints`, e.g. `"PFOUND_ZSTD"`) so the codec
only compiles where that dependency is available, and the dependency never leaks into consumers that
do not opt in. The BCL-only Deflate provider needs no such gate.

## File Structure

```
Compression/
  README.md
  MODULE.md
  Runtime/
    Lzma.cs                         # the codec: public API + private encoder/decoder/window/match finder
    ICompressionCodec.cs            # swappable byte[] round-trip interface
    LzmaCodec.cs                    # sealed ICompressionCodec (Id "lzma") forwarding to static Lzma
    CompressionCodecs.cs            # static registry: Default/Register/TryGet/Get/SetDefault/RegisteredIds (+ enum overloads)
    CompressionMethod.cs            # typed codec selector enum (Lzma, Deflate)
    CompressionCodecExtensions.cs   # codec-agnostic UTF-8 CompressString/DecompressString extensions
    PFound.Compression.asmdef       # engine-free, autoReferenced:false
  Providers/
    Deflate/
      DeflateCodec.cs               # ICompressionCodec (Id "deflate") over BCL DeflateStream
      DeflateCodecInstaller.cs      # Install() + RuntimeInitializeOnLoadMethod self-registration
      PFound.Compression.Deflate.asmdef  # references PFound.Compression, autoReferenced:true
  Tests/
    LzmaTests.cs                    # round-trip / edge-case suite
    TestKit.cs
    Program.cs                      # standalone csc/mono runner entry
    PFound.Compression.Tests.asmdef
```

## Downstream Dependents

- **`PFound.ContentDelivery`** (`Core` runtime + tests, `Editor`) — compresses bundles at author
  time and decompresses them on download.
- **`PFound.LocalizationService`** (`Unity`, `Editor`) — compressed localization payloads.

## Limitations / Known Gaps

- **Implements the LZMA1 algorithm; not bit-identical to other LZMA encoders.** The encoder emits
  literals and simple matches only (it never chooses rep matches), so streams are smaller-than-LZ4
  but larger than a maximal LZMA encode. The decoder still understands rep matches for format
  completeness.
- **Fixed literal/position context bits** `lc=3, lp=0, pb=2`. Only the dictionary / match-window
  size is selectable (256 KiB..4 MiB, default 4 MiB) — and it is honored on decode from the header.
- **The encoder buffers the whole payload** to build its match model (`Compress(Stream, Stream)` and
  the byte/file/string entry points all read the full input into an array); only *decode* streams at
  bounded peak memory.
- **Main-thread-agnostic but caller-capped.** Decode is safe to run concurrently across blobs, but
  the pool holds one resident window per in-flight decode — the caller must bound concurrency.
