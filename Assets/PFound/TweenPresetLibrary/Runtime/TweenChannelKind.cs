namespace PFound.TweenPresetLibrary
{
    /// <summary>Which transform/visual aspect a <see cref="TweenChannel{T}"/> drives.</summary>
    public enum TweenChannelKind : byte
    {
        /// <summary>No channel / unset sentinel.</summary>
        Empty = 0,
        Fade = 1,
        Scale = 2,
        Move = 3,
        Rotate = 4,
    }
}
