using System;

namespace PFound.Utilities.Pause
{
    /// <summary>
    /// A single outstanding pause request handed out by <see cref="PauseCoordinator.Acquire"/>.
    /// Keeping the handle alive keeps the coordinator paused; releasing it (explicitly or by
    /// letting a <c>using</c> block dispose it) withdraws this one reason. Release is
    /// idempotent, so disposing twice or disposing after a coordinator-wide
    /// <see cref="PauseCoordinator.ReleaseAll"/> does nothing on the second call.
    /// </summary>
    public sealed class PauseHandle : IDisposable
    {
        private PauseCoordinator _owner;

        internal PauseHandle(PauseCoordinator owner, string reason)
        {
            _owner = owner;
            Reason = reason;
        }

        /// <summary>Optional descriptive reason supplied at acquisition time; may be null.</summary>
        public string Reason { get; }

        /// <summary>True until this handle has been released.</summary>
        public bool IsHeld => _owner != null;

        /// <summary>Withdraws this pause reason. Safe to call more than once.</summary>
        public void Release()
        {
            var owner = _owner;
            if (owner == null)
            {
                return;
            }
            _owner = null;
            owner.Retire(this);
        }

        /// <summary>Alias for <see cref="Release"/> so the handle works with <c>using</c>.</summary>
        public void Dispose() => Release();

        // Invoked by the coordinator when it clears everything in one shot; detaches without
        // calling back into the owner (which is already resetting its own state).
        internal void MarkReleased() => _owner = null;
    }
}
