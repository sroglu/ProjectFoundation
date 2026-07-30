using UnityEngine;

namespace Kunai
{
    public abstract class KuWindow
    {
        public abstract string Title { get; }

        public Rect WindowRect
        {
            get => State.Rect;
            set
            {
                var s = State;
                s.Rect = value;
                State = s;
            }
        }

        internal KuiWindowState State;

        public virtual void Initialize() { }

        // Called by KuiContext.Dispose for every registered window. Default
        // is a no-op. Override to release native containers or unsubscribe
        // from events. Safe to assume single-call (idempotency is not enforced).
        public virtual void Shutdown() { }

        public abstract void OnRenderUI();

        // Each frame KuiContext snaps the window's left/right edges to the
        // screen, leaving InsetPx on each side. Useful for log views, status
        // bars, or any console-shaped panel that should grow with the
        // viewport. Vertical position + height stay as authored.
        public virtual bool StretchHorizontal => false;
        public virtual float StretchInsetPx => 20f;

        // Whether the master toolbox window lists this one as a togglable
        // entry. Override to false for "always-on" panels (e.g. Console) or
        // for the master window itself (so it can't toggle itself off).
        public virtual bool ShowInMasterToggle => true;
    }
}
