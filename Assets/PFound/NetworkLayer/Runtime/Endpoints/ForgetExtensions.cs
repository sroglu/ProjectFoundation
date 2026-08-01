using System;
using System.Threading.Tasks;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Fire a call without awaiting its reply — the request still goes out and the server still answers, the
    /// caller just does not block on (or read) the response. This is the call-site "fire and forget" choice on
    /// an ordinary reply-bearing operation: <c>SomeOp.Execute(args).Forget();</c>. It is NOT the same as a
    /// <see cref="NotifyAttribute"/> op — a notify carries NO reply on the wire at all, while this fires a normal
    /// reply-bearing call and drops the reply. Use <c>Forget</c> when the op has a reply (or a server side effect)
    /// you simply do not want to wait for; use <c>[Notify]</c> when there is genuinely no reply to send.
    /// </summary>
    public static class ForgetExtensions
    {
        /// <summary>
        /// Where a forgotten call's failure goes. A dropped <see cref="Task"/> would otherwise swallow its
        /// exception silently — this routes it somewhere visible. The host sets it once at boot (e.g. to
        /// <c>UnityEngine.Debug.LogException</c>); until then faults surface on <see cref="Console.Error"/>.
        /// </summary>
        public static Action<Exception> OnFault = e => Console.Error.WriteLine(e);

        /// <summary>Send the call and return immediately; a fault is routed to <see cref="OnFault"/>.</summary>
        public static void Forget(this Task call)
        {
            _ = Observe(call);
        }

        static async Task Observe(Task call)
        {
            try
            {
                await call;
            }
            catch (OperationCanceledException)
            {
                // A gameloop/session cancel is an expected stop, not a fault — drop it silently.
            }
            catch (Exception e)
            {
                OnFault(e);
            }
        }
    }
}
