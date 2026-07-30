using PFound.GuidedOnboardingFlow.Core;
using UnityEngine;

namespace PFound.GuidedOnboardingFlow.Tests
{
    /// <summary>Interface fakes so the interactive steps can be exercised without a live uGUI scene.</summary>
    internal sealed class FakeInputBlocker : IInputBlocker
    {
        public bool Enabled;
        public RectTransform Hole;
        public bool PendingInteraction;

        public void Enable() => Enabled = true;
        public void Disable() => Enabled = false;
        public void SetHole(RectTransform target) => Hole = target;

        public bool ConsumeInteraction()
        {
            if (!PendingInteraction) return false;
            PendingInteraction = false;
            return true;
        }
    }

    internal sealed class FakeHand : ITutorialHand
    {
        public RectTransform Pointed;
        public string Hint;
        public Vector2 Offset;
        public bool Hidden;

        public void PointAt(RectTransform target) => PointAt(target, null, Vector2.zero);

        public void PointAt(RectTransform target, string hint, Vector2 offset)
        {
            Pointed = target;
            Hint = hint;
            Offset = offset;
            Hidden = false;
        }

        public void Hide() { Hidden = true; Pointed = null; }
    }

    internal sealed class FakeHighlight : IHighlightMask
    {
        public RectTransform Target;
        public string MaskSpriteName;
        public Vector2 Padding;
        public bool Cleared;

        public void Highlight(RectTransform target) => Highlight(target, null, Vector2.zero);

        public void Highlight(RectTransform target, string maskSpriteName, Vector2 padding)
        {
            Target = target;
            MaskSpriteName = maskSpriteName;
            Padding = padding;
            Cleared = false;
        }

        public void Clear() { Cleared = true; Target = null; }
    }

    internal sealed class FakeDialog : IDialogPanel
    {
        public string Message;
        public float TypingSpeedCps;
        public DialogDismissMode DismissMode;
        public float? AutoDismissSeconds;
        public bool Visible;
        public bool PendingContinue;

        public void Show(string message) => Show(message, 0f, DialogDismissMode.Tap, null);

        public void Show(string message, float typingSpeedCps, DialogDismissMode dismissMode, float? autoDismissSeconds)
        {
            Message = message;
            TypingSpeedCps = typingSpeedCps;
            DismissMode = dismissMode;
            AutoDismissSeconds = autoDismissSeconds;
            Visible = true;
        }

        public void Hide() => Visible = false;

        public bool ConsumeContinue()
        {
            if (!PendingContinue) return false;
            PendingContinue = false;
            return true;
        }
    }

    internal sealed class FakeManager : ITutorialManager
    {
        public TutorialRunner Active => null;
        public bool IsRunning => false;
        public bool AutoAdvance { get; set; }
        public float AutoAdvanceDelaySeconds { get; private set; }

        public event System.Action<TutorialId> TutorialStarted { add { } remove { } }
        public event System.Action<TutorialId, TutorialOutcome> TutorialEnded { add { } remove { } }
        public event System.Action<ITutorialStep> StepStarted { add { } remove { } }
        public event System.Action<TutorialId, int> StepChanged { add { } remove { } }

        public bool TryStart(TutorialId id) => false;
        public void ForceStart(TutorialId id) { }
        public void Skip() { }
        public void SetRunSpecific(System.Collections.Generic.IEnumerable<TutorialId> ids) { }

        public void SetAutoAdvance(bool enabled, float perStepDelaySeconds)
        {
            AutoAdvance = enabled;
            AutoAdvanceDelaySeconds = perStepDelaySeconds;
        }

        public void Tick(float deltaSeconds) { }
    }

    internal static class Fakes
    {
        public static TutorialRuntimeServices Services(
            out FakeInputBlocker blocker,
            out FakeHand hand,
            out FakeHighlight highlight,
            out FakeDialog dialog,
            out TutorialAnchorRegistry anchors,
            out FakeManager manager)
        {
            blocker = new FakeInputBlocker();
            hand = new FakeHand();
            highlight = new FakeHighlight();
            dialog = new FakeDialog();
            anchors = new TutorialAnchorRegistry();
            manager = new FakeManager();

            var services = new TutorialRuntimeServices(blocker, hand, highlight, dialog, anchors, new NullLog());
            services.Attach(manager);
            return services;
        }
    }

    internal sealed class NullLog : ITutorialLog
    {
        public void Write(LogSeverity severity, string message) { }
    }
}
