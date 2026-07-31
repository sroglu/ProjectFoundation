using System;
using UnityEngine;
using PFound.LocalizationService;
using PFound.NetworkLayer;
using PFound.ServerOperation;
using PFound.ServerOperation.Core;

namespace GameSpecific.Networking
{
    /// <summary>
    /// The game's transport factory for the ambient <see cref="ServerOperationHost"/>. It binds every flow's
    /// request/response pair to the realtime request/reply layer by wrapping the ambient
    /// <see cref="NetworkClient.Current"/> peer in a <see cref="ClientPeerServerOperationTransport{TRequest,TResponse}"/>.
    /// The Core-side factory signature is constraint-free (it cannot see the wire base types), while the transport
    /// requires <c>TRequest : RequestMessage</c> / <c>TResponse : ReplyMessage</c> (the peer decodes the reply by
    /// its concrete awaited type) — so the constrained transport is built reflectively from the flow's concrete
    /// envelope types, which always satisfy the constraints at runtime. The peer is read at call time (not
    /// captured), mirroring how the generated <c>Execute</c> reads it, so there is no boot-ordering coupling.
    /// </summary>
    public sealed class GameServerOperationTransportFactory : IServerOperationTransportFactory
    {
        public IServerOperationTransport<TRequest, TResponse> CreateTransport<TRequest, TResponse>()
        {
            var transportType = typeof(ClientPeerServerOperationTransport<,>).MakeGenericType(typeof(TRequest), typeof(TResponse));
            return (IServerOperationTransport<TRequest, TResponse>)Activator.CreateInstance(transportType, NetworkClient.Current, -1);
        }
    }

    /// <summary>
    /// The game's outcome seams for the ambient host. Every flow in this game reports through
    /// <see cref="ServerOperationResult"/>, so the failure presenter is a single concrete instance the generic
    /// accessor hands back (the object cast is the standard bridge from a generic ambient accessor to a
    /// fixed-result-type seam — it always holds because <c>TResult</c> is <see cref="ServerOperationResult"/>
    /// at every call site). The success notification is not needed here, so the no-op Core sink is returned.
    /// </summary>
    public sealed class GameServerOperationResultChannels : IServerOperationResultChannels
    {
        readonly IServerOperationFailurePresenter<ServerOperationResult> _failurePresenter;

        public GameServerOperationResultChannels(IServerOperationFailurePresenter<ServerOperationResult> failurePresenter)
            => _failurePresenter = failurePresenter;

        public IServerOperationFailurePresenter<TResult> FailurePresenter<TResult>() where TResult : IServerOperationResult
            => (IServerOperationFailurePresenter<TResult>)(object)_failurePresenter;

        public IServerOperationSuccessSink<TResult> SuccessSink<TResult>() where TResult : IServerOperationResult
            => new SilentSuccessSink<TResult>();
    }

    /// <summary>
    /// One place a failed operation is surfaced to the developer for this sample game. Still a valid choice — it
    /// logs the diagnostic so a rejected pre-check or a server failure is visible in the console — but the default
    /// sample wiring instead resolves the result code to a localized toast (see <see cref="LocalizationUserMessages"/>
    /// + <see cref="DebugToastPresenter"/> and <c>GameNetworkSetup</c>).
    /// </summary>
    public sealed class LoggingFailurePresenter : IServerOperationFailurePresenter<ServerOperationResult>
    {
        public void PresentFailure(ServerOperationResult result)
            => Debug.LogError($"ServerOperation failed (code {result.ResultCode}): {result.ErrorMessage}");
    }

    /// <summary>
    /// The game's <see cref="IUserMessageSource"/> — the framework presenter's window onto this game's
    /// <see cref="LocalizationService"/>. It looks up the conventional <c>"op.result.&lt;Name&gt;"</c> key as raw
    /// (unformatted) text; a hit means the localization row exists, a miss lets the presenter fall back.
    /// </summary>
    public sealed class LocalizationUserMessages : IUserMessageSource
    {
        readonly LocalizationService _localization;

        public LocalizationUserMessages(LocalizationService localization) => _localization = localization;

        public bool TryGet(string key, out string message) => _localization.TryGetUnprocessed(key, out message);
    }

    /// <summary>
    /// A placeholder toast destination that logs the resolved message as a warning so the localized error is
    /// visible while a real on-screen toast is still a future game concern. Swap this for the game's toast/banner
    /// widget when one exists — the failure pipeline needs no other change.
    /// </summary>
    public sealed class DebugToastPresenter : IToastPresenter
    {
        public void Show(string message) => Debug.LogWarning($"[toast] {message}");
    }
}
