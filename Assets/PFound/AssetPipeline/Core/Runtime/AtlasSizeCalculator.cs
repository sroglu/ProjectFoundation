using System;

namespace PFound.AssetPipeline.Core
{
    /// <summary>
    /// Picks a power-of-two atlas page size from the members that must fit on it: the smallest power of two whose
    /// area covers the summed member area AND whose side is at least the largest single member dimension, clamped to
    /// a hard maximum. Pure integer math — no engine types — so the sizing policy is unit-testable without Unity;
    /// the editor atlas builder feeds it the members' pixel rects.
    /// </summary>
    public static class AtlasSizeCalculator
    {
        /// <summary>The largest atlas page this policy will emit; a set that needs more is clamped (and the caller warns).</summary>
        public const int MaxAtlasSize = 4096;

        /// <summary>The smallest page the search starts from.</summary>
        public const int MinAtlasSize = 32;

        /// <summary>
        /// Smallest power-of-two side (>= <see cref="MinAtlasSize"/>) whose square covers <paramref name="totalMemberArea"/>
        /// and is at least <paramref name="largestMemberDimension"/>, clamped to <paramref name="clamp"/>. Returns
        /// whether the ideal size had to be clamped via <paramref name="clamped"/>.
        /// </summary>
        public static int ComputeMaxTextureSize(
            double totalMemberArea, int largestMemberDimension, out bool clamped, int clamp = MaxAtlasSize)
        {
            if (totalMemberArea < 0) throw new ArgumentOutOfRangeException(nameof(totalMemberArea));
            if (largestMemberDimension < 0) throw new ArgumentOutOfRangeException(nameof(largestMemberDimension));
            if (clamp < MinAtlasSize) throw new ArgumentOutOfRangeException(nameof(clamp));

            int size = MinAtlasSize;

            // Grow (uncapped) until the page area covers the summed member area.
            while ((double)size * size < totalMemberArea)
                size *= 2;

            // Grow until the page side can hold the single largest member dimension.
            while (size < largestMemberDimension)
                size *= 2;

            // Clamp: anything above the hard cap is pinned down (the caller warns; the set won't fit ideally).
            if (size > clamp) { size = clamp; clamped = true; }
            else clamped = false;

            return size;
        }

        /// <summary>Convenience overload without the clamp-out flag.</summary>
        public static int ComputeMaxTextureSize(double totalMemberArea, int largestMemberDimension, int clamp = MaxAtlasSize)
            => ComputeMaxTextureSize(totalMemberArea, largestMemberDimension, out _, clamp);
    }
}
