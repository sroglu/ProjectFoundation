using System;

namespace PFound.Utilities.Messaging
{
    /// <summary>
    /// A disposable handle representing one (or, for a paired switch listener, a
    /// bundle of) live subscription(s). Disposing it detaches the listener; disposal
    /// is idempotent.
    /// </summary>
    public sealed class Subscription : IDisposable
    {
        private Action _detach;

        internal Subscription(Action detach)
        {
            _detach = detach;
        }

        /// <summary>True while the underlying listener is still attached.</summary>
        public bool Active
        {
            get { return _detach != null; }
        }

        /// <summary>Detaches the listener. Safe to call more than once.</summary>
        public void Dispose()
        {
            Action detach = _detach;
            _detach = null;
            if (detach != null)
            {
                detach();
            }
        }

        /// <summary>An already-spent handle, handy when a subscribe request is a no-op.</summary>
        internal static Subscription Spent()
        {
            return new Subscription(null);
        }
    }
}
