using System;
using System.Collections.Generic;

namespace PFound.Utilities.Pause
{
    /// <summary>
    /// Reference-counted pause coordinator. Any number of independent callers can request a
    /// pause by acquiring a <see cref="PauseHandle"/>; the coordinator reports
    /// <see cref="IsPaused"/> for as long as at least one handle is still held. Releasing the
    /// last outstanding handle resumes. The <see cref="PauseChanged"/> event fires only on the
    /// transitions (running -> paused and paused -> running), never on intermediate acquires
    /// or releases that leave the paused state unchanged.
    /// </summary>
    /// <remarks>
    /// Deliberately engine-free (no UnityEngine dependency) so the counting logic can be
    /// exercised under plain csc/mono. Intended for single-threaded use on the main loop.
    /// </remarks>
    public sealed class PauseCoordinator
    {
        private readonly List<PauseHandle> _held = new List<PauseHandle>();

        /// <summary>Raised on paused/running edges only. Argument is the new paused state.</summary>
        public event Action<bool> PauseChanged;

        /// <summary>True while one or more handles are outstanding.</summary>
        public bool IsPaused => _held.Count > 0;

        /// <summary>Number of handles currently held.</summary>
        public int ActiveCount => _held.Count;

        /// <summary>
        /// Requests a pause. The returned handle keeps the coordinator paused until it is
        /// released (via <see cref="PauseHandle.Release"/> or disposal). An optional
        /// <paramref name="reason"/> is carried for diagnostics/inspection.
        /// </summary>
        public PauseHandle Acquire(string reason = null)
        {
            var handle = new PauseHandle(this, reason);
            bool wasRunning = _held.Count == 0;
            _held.Add(handle);
            if (wasRunning)
            {
                PauseChanged?.Invoke(true);
            }
            return handle;
        }

        /// <summary>
        /// Snapshot of the reasons attached to the currently held handles, in acquisition
        /// order. Handles acquired without a reason contribute an empty string.
        /// </summary>
        public IReadOnlyList<string> ActiveReasons
        {
            get
            {
                var reasons = new string[_held.Count];
                for (int i = 0; i < _held.Count; i++)
                {
                    reasons[i] = _held[i].Reason ?? string.Empty;
                }
                return reasons;
            }
        }

        /// <summary>
        /// Drops every outstanding handle at once and resumes. Handles released this way become
        /// inert, so a later Release/Dispose on any of them is a harmless no-op. Does nothing
        /// (and fires no event) when already running.
        /// </summary>
        public void ReleaseAll()
        {
            if (_held.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _held.Count; i++)
            {
                _held[i].MarkReleased();
            }
            _held.Clear();
            PauseChanged?.Invoke(false);
        }

        // Called by a handle when it is released individually. Idempotence is guaranteed by
        // the handle itself, so a handle only reaches here once while it is still tracked.
        internal void Retire(PauseHandle handle)
        {
            if (!_held.Remove(handle))
            {
                return;
            }
            if (_held.Count == 0)
            {
                PauseChanged?.Invoke(false);
            }
        }
    }
}
