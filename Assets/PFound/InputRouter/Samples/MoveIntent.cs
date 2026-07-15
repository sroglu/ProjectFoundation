namespace PFound.InputRouter.Samples
{
    /// <summary>
    /// The shared sample intent both example adapters produce: a 2D movement vector. Kept as a
    /// plain struct with primitive fields so it stays engine-free and allocation-free — the two
    /// backends differ only in how they read the axes, never in the intent they emit.
    /// </summary>
    public struct MoveIntent : IIntent
    {
        public float Horizontal;
        public float Vertical;

        public MoveIntent(float horizontal, float vertical)
        {
            Horizontal = horizontal;
            Vertical = vertical;
        }
    }
}
