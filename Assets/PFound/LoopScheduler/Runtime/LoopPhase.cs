namespace PFound.LoopScheduler
{
    /// <summary>
    /// Ordered frame phases the scheduler runs each tick (declaration order = execution order).
    /// Coarse pipeline from input through render to end-of-frame; callbacks within a phase run in
    /// registration order. <see cref="BeforeRender"/> is driven by the engine's pre-render hook;
    /// the rest run in one early pipeline pass per frame.
    /// </summary>
    public enum LoopPhase
    {
        Input,
        Selection,
        EarlyUpdate,
        Update,
        LateUpdate,
        PostUpdate,
        Camera,
        Bindings,
        PreRender,
        BeforeRender,
        Render,
        PostRender,
        Scene,
        Network,
        EndOfFrame
    }
}
