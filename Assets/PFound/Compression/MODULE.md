# Compression

## Purpose

Engine-free compression codecs for PFound. Today it ships one clean-room `Lzma` codec — a static
library that compresses at build/content-authoring time and decompresses on the runtime hot path
(e.g. a downloaded content blob). `Decompress` inverts `Compress` from this same codec; that
round-trip property is the only guarantee the module makes.

## Assemblies

| Assembly | Path | Notes |
|---|---|---|
| `PFound.Compression` | `Runtime/PFound.Compression.asmdef` | `noEngineReferences: true`, `autoReferenced: false` — add it to a consumer's asmdef `references` explicitly |
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

## File Structure

```
Compression/
  README.md
  MODULE.md
  Runtime/
    Lzma.cs                         # the codec: public API + private encoder/decoder/window/match finder
    PFound.Compression.asmdef       # engine-free, autoReferenced:false
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

- **Clean-room LZMA1 from the published algorithm — NOT a port of the 7-Zip SDK**, and not
  bit-identical to it. The encoder emits literals and simple matches only (it never chooses rep
  matches), so streams are smaller-than-LZ4 but larger than a full 7-Zip encode. The decoder still
  understands rep matches for format completeness.
- **Fixed literal/position context bits** `lc=3, lp=0, pb=2`. Only the dictionary / match-window
  size is selectable (256 KiB..4 MiB, default 4 MiB) — and it is honored on decode from the header.
- **The encoder buffers the whole payload** to build its match model (`Compress(Stream, Stream)` and
  the byte/file/string entry points all read the full input into an array); only *decode* streams at
  bounded peak memory.
- **Main-thread-agnostic but caller-capped.** Decode is safe to run concurrently across blobs, but
  the pool holds one resident window per in-flight decode — the caller must bound concurrency.
