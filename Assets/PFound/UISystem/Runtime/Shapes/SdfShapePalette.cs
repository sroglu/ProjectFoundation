using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.UISystem.Shapes
{
    /// <summary>
    /// Color palette resolver for the spec 009 A+ per-instance state delivery scheme.
    /// Each <see cref="SdfShape"/> / <c>M3Surface</c> element stores a palette index in
    /// its <c>Vertex.tint</c> Color32 (byte 0 = fill index). The fragment shader reads
    /// the actual color from the <c>_ColorPalette[16]</c> uniform on the shared material.
    /// </summary>
    /// <remarks>
    /// Lifecycle:
    /// <list type="bullet">
    ///   <item>Slot 0 is reserved for the design-system default fill (initialized lazily).</item>
    ///   <item>Slots 1-15 are dynamically assigned in insertion order on first <see cref="Resolve"/> call.</item>
    ///   <item>Overflow (> 16 unique colors) throws <see cref="UISystemPaletteOverflowException"/>
    ///         — does NOT silently quantize to the nearest existing slot, since visual
    ///         surprises are worse than a developer-fixable error.</item>
    ///   <item>HDR colors are supported in palette slots (float4 storage preserves channels > 1.0).
    ///         See spec 009 data-model.md "HDR color support" note for output-side caveats.</item>
    /// </list>
    /// Thread-safety: main-thread only (matches UI Toolkit / UIR conventions).
    /// </remarks>
    public static class SdfShapePalette
    {
        /// <summary>Shader uniform array size — must match <c>_ColorPalette[16]</c> in UIShape.shader.</summary>
        public const int MaxSlots = 16;

        /// <summary>Reserved slot index for the design-system default fill color.</summary>
        public const int DefaultFillSlot = 0;

        private static readonly Color[] _slots = new Color[MaxSlots];
        private static readonly Dictionary<Color, int> _lookup = new(MaxSlots);
        private static int _nextSlot = 1; // slot 0 reserved
        private static bool _dirty = true;

        // Backing field for the shader uniform upload. Allocated once, reused every flush.
        private static readonly Vector4[] _uploadBuffer = new Vector4[MaxSlots];

        static SdfShapePalette()
        {
            // Slot 0 default — white. Real default comes from ThemeMaterialRegistry on first
            // flush, but a sane initial value avoids garbage on the first frame.
            _slots[DefaultFillSlot] = Color.white;
            _lookup[Color.white] = DefaultFillSlot;
        }

        /// <summary>
        /// Resolve a <see cref="Color"/> to a stable palette index in [0, MaxSlots-1].
        /// Returns the existing slot if the color was previously resolved; otherwise
        /// allocates the next free slot. Throws on overflow.
        /// </summary>
        /// <exception cref="UISystemPaletteOverflowException">
        /// Thrown when more than <see cref="MaxSlots"/> unique colors are requested across
        /// all active SdfShape / M3Surface instances. Resolve overflow by expanding the
        /// shader's _ColorPalette array size (also bump MaxSlots) or simplifying the color set.
        /// </exception>
        public static int Resolve(Color color)
        {
            if (_lookup.TryGetValue(color, out var idx)) return idx;
            if (_nextSlot >= MaxSlots)
            {
                throw new UISystemPaletteOverflowException(
                    $"SdfShapePalette overflow: requested {MaxSlots + 1}th unique color {color} but " +
                    $"shader _ColorPalette has only {MaxSlots} slots. " +
                    "Expand the array size in UIShape.shader and SdfShapePalette.MaxSlots, " +
                    "or reduce the unique color count in your UI.");
            }
            idx = _nextSlot++;
            _slots[idx] = color;
            _lookup[color] = idx;
            _dirty = true;
            return idx;
        }

        /// <summary>
        /// Atomically replace the entire palette. Use for theme swap — all elements
        /// re-render with the new color set on next frame without per-element updates.
        /// Slots beyond <paramref name="newPalette"/>'s length keep their previous values
        /// (but their <see cref="Resolve"/> lookup is rebuilt).
        /// </summary>
        public static void SetPalette(Color[] newPalette)
        {
            if (newPalette == null) throw new ArgumentNullException(nameof(newPalette));
            if (newPalette.Length > MaxSlots)
            {
                throw new UISystemPaletteOverflowException(
                    $"SetPalette called with {newPalette.Length} colors but MaxSlots is {MaxSlots}.");
            }
            _lookup.Clear();
            for (int i = 0; i < newPalette.Length; i++)
            {
                _slots[i] = newPalette[i];
                _lookup[newPalette[i]] = i;
            }
            _nextSlot = newPalette.Length;
            _dirty = true;
        }

        /// <summary>
        /// Push the palette to all active category materials' <c>_ColorPalette</c> uniform
        /// array. Idempotent — coalesced via dirty flag, only uploads when needed. Called
        /// by <see cref="SdfShape.OnGenerateVisualContent"/> before emitting vertices each
        /// frame, OR explicitly on theme swap.
        /// </summary>
        public static void FlushToActiveMaterials()
        {
            if (!_dirty) return;
            // Material.SetVectorArray bypasses the sRGB→linear conversion that Material.SetColor
            // performs automatically in a Linear color-space project. Without pre-linearizing,
            // sRGB values written into the shader uniform get gamma-applied a second time on the
            // sRGB render target, producing washed-out fills (e.g. M3 primary #6750A4 rendering
            // as ~#AA98D2). Pre-linearize here so the GPU's output sRGB encoding lands on the
            // original sRGB value the design system specified.
            bool linearSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < MaxSlots; i++)
            {
                var c = _slots[i];
                var v = linearSpace ? c.linear : c;
                _uploadBuffer[i] = new Vector4(v.r, v.g, v.b, c.a);
            }
            foreach (var mat in SdfShapeMaterials.ActiveMaterials)
            {
                if (mat != null) mat.SetVectorArray("_ColorPalette", _uploadBuffer);
            }
            _dirty = false;
        }

        /// <summary>Force-mark palette as dirty so the next flush re-uploads even if no Resolve happened.</summary>
        public static void MarkDirty() => _dirty = true;

        /// <summary>
        /// Reset the palette to its initial state — slot 0 = white, all other slots cleared.
        /// Used by tests + on theme teardown.
        /// </summary>
        public static void ClearAll()
        {
            Array.Clear(_slots, 0, MaxSlots);
            _lookup.Clear();
            _slots[DefaultFillSlot] = Color.white;
            _lookup[Color.white] = DefaultFillSlot;
            _nextSlot = 1;
            _dirty = true;
        }

        /// <summary>Diagnostic: how many palette slots are currently occupied.</summary>
        public static int OccupiedSlotCount => _nextSlot;

        /// <summary>Diagnostic: read the color stored at a given slot.</summary>
        public static Color GetSlotColor(int idx)
        {
            if (idx < 0 || idx >= MaxSlots) throw new ArgumentOutOfRangeException(nameof(idx));
            return _slots[idx];
        }
    }

    /// <summary>
    /// Thrown when <see cref="SdfShapePalette"/> capacity (16 slots) is exceeded.
    /// Per spec 009 SC-002, the system does NOT silently degrade — it forces the
    /// developer to either expand the shader's palette size or simplify their color set.
    /// </summary>
    public sealed class UISystemPaletteOverflowException : InvalidOperationException
    {
        public UISystemPaletteOverflowException(string message) : base(message) { }
    }
}
