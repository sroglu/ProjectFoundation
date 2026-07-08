using PFound.GuidedOnboardingFlow.Core;
using PFound.Signaling;
using RouterService = PFound.ScreenRouter.ScreenRouter;

namespace PFound.GuidedOnboardingFlow
{
    /// <summary>
    /// The bag of resolved services a step needs at build time. The engine-free core never sees this; it
    /// is the seam through which the Unity layer hands concrete UI/router/signal collaborators to the
    /// authoring assets and step factories.
    /// </summary>
    public sealed class TutorialRuntimeServices
    {
        // Attached after construction to break the cycle: blueprints are built from these services, and
        // the manager is built from those blueprints, so the manager can only be wired in afterward.
        public ITutorialManager Manager { get; private set; }
        public IInputBlocker InputBlocker { get; }
        public ITutorialHand Hand { get; }
        public IHighlightMask Highlight { get; }
        public IDialogPanel Dialog { get; }
        public ITutorialAnchorRegistry Anchors { get; }
        public ITutorialLog Log { get; }
        public RouterService Router { get; }
        public SignalTracker Signals { get; }

        public TutorialRuntimeServices(
            IInputBlocker inputBlocker,
            ITutorialHand hand,
            IHighlightMask highlight,
            IDialogPanel dialog,
            ITutorialAnchorRegistry anchors,
            ITutorialLog log,
            RouterService router = null,
            SignalTracker signals = null)
        {
            InputBlocker = inputBlocker;
            Hand = hand;
            Highlight = highlight;
            Dialog = dialog;
            Anchors = anchors;
            Log = log;
            Router = router;
            Signals = signals;
        }

        /// <summary>Wire the manager in once it has been built from these services' blueprints.</summary>
        public void Attach(ITutorialManager manager) => Manager = manager;

        /// <summary>Steps read this to honour the manager's auto-advance / auto-click mode.</summary>
        public bool AutoAdvance => Manager != null && Manager.AutoAdvance;

        /// <summary>Per-step delay a step waits before self-completing under auto-advance.</summary>
        public float AutoAdvanceDelaySeconds => Manager != null ? Manager.AutoAdvanceDelaySeconds : 0f;
    }
}
