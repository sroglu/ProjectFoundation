using PFound.Compression;
using UnityEngine;

namespace PFound.Compression.Deflate
{
    /// <summary>
    /// Self-registration entry point for <see cref="DeflateCodec"/>. Having this provider assembly in
    /// the project is enough: the <see cref="RuntimeInitializeOnLoadMethod"/> hook registers the codec
    /// automatically at play (before the first scene loads). <see cref="Install"/> can also be called
    /// explicitly — e.g. from an edit-mode test, where the runtime-initialize hook does not fire.
    /// Registration is idempotent: <see cref="CompressionCodecs.Register"/> keys by id, so repeated
    /// installs just replace the entry under "deflate".
    /// </summary>
    public static class DeflateCodecInstaller
    {
        /// <summary>Registers a <see cref="DeflateCodec"/> in <see cref="CompressionCodecs"/> (id "deflate").</summary>
        public static void Install() => CompressionCodecs.Register(new DeflateCodec());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Auto() => Install();
    }
}
