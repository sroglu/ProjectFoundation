namespace PFound.LoopScheduler
{
    /// <summary>
    /// The frame phases the scheduler runs each tick. Declaration order follows the real frame
    /// timeline, and every phase is injected at a distinct PlayerLoop moment (not one up-front
    /// pass): the early phases run inside the engine's early-update slot, the input phases inside
    /// the pre-update slot, the update phases inside the update slot, the late/camera/binding
    /// phases inside the pre-late-update slot, <see cref="BeforeRender"/> on the engine's
    /// pre-render hook, and the render/end phases inside the post-late-update slot. Callbacks
    /// within a single phase run in registration order.
    /// </summary>
    public enum LoopPhase
    {
        // --- engine early-update slot ---
        /// <summary>First logic phase of the frame; runs in the engine early-update slot.</summary>
        EarlyUpdate,
        /// <summary>Scene-level work, timed early in the frame.</summary>
        Scene,
        /// <summary>Networking / transport pump, timed early in the frame.</summary>
        Network,

        // --- engine pre-update slot (after input devices are polled) ---
        /// <summary>Input handling, after the engine has refreshed input devices.</summary>
        Input,
        /// <summary>Selection / pointer resolution, right after <see cref="Input"/>.</summary>
        Selection,

        // --- engine update slot (after MonoBehaviour.Update) ---
        /// <summary>Main per-frame update, after the engine's Update.</summary>
        Update,
        /// <summary>Follow-up update work, right after <see cref="Update"/>.</summary>
        PostUpdate,

        // --- engine pre-late-update slot (after MonoBehaviour.LateUpdate) ---
        /// <summary>Late per-frame update, after the engine's LateUpdate.</summary>
        LateUpdate,
        /// <summary>Camera positioning, after gameplay has settled for the frame.</summary>
        Camera,
        /// <summary>Data-binding / view sync, after the camera is placed.</summary>
        Bindings,
        /// <summary>Last chance to mutate before the pre-render hook.</summary>
        PreRender,

        // --- engine pre-render hook ---
        /// <summary>Driven by the engine's pre-render hook, immediately before rendering.</summary>
        BeforeRender,

        // --- engine post-late-update slot (after rendering has been submitted) ---
        /// <summary>Render-adjacent work, in the post-late-update slot.</summary>
        Render,
        /// <summary>Follow-up render work, right after <see cref="Render"/>.</summary>
        PostRender,
        /// <summary>Final phase of the frame.</summary>
        EndOfFrame
    }
}
