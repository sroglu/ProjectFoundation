namespace PFound.ServerOperation.Core
{
    using System;

    /// <summary>
    /// The reusable failure destination that turns a failed operation's result code into a localized, user-facing
    /// string and shows it through a toast seam. It reads the code as a member of the game's result enum
    /// <typeparamref name="TCode"/> and resolves a localization key BY CONVENTION —
    /// <c>keyPrefix + enumMemberName</c> (default <c>"op.result."</c>) — so authoring a new user message is just
    /// adding a localization row; no mapping table, no switch. Resolution order: the conventional key, then the
    /// shared <see cref="_fallbackKey"/>, then the result's own diagnostic <see cref="IServerOperationResult.ErrorMessage"/>.
    /// </summary>
    public sealed class LocalizedToastFailurePresenter<TResult, TCode> : IServerOperationFailurePresenter<TResult>
        where TResult : IServerOperationResult
        where TCode : struct, System.Enum
    {
        readonly IUserMessageSource _messages;
        readonly IToastPresenter _toast;
        readonly string _keyPrefix;
        readonly string _fallbackKey;

        public LocalizedToastFailurePresenter(
            IUserMessageSource messages,
            IToastPresenter toast,
            string keyPrefix = "op.result.",
            string fallbackKey = "op.result.Unknown")
        {
            _messages = messages;
            _toast = toast;
            _keyPrefix = keyPrefix;
            _fallbackKey = fallbackKey;
        }

        public void PresentFailure(TResult result)
        {
            string key = KeyForCode(result.ResultCode);
            string message = ResolveMessage(key, result.ErrorMessage);
            _toast.Show(message);
        }

        /// <summary>
        /// The conventional key for a result code: <c>keyPrefix + Enum.GetName(code)</c> when the code is a defined
        /// member of <typeparamref name="TCode"/> AND that member is not the <c>"Invalid"</c> sentinel; otherwise
        /// the generic fallback key. An out-of-range or sentinel code always lands on the generic message.
        /// </summary>
        string KeyForCode(int resultCode)
        {
            if (!System.Enum.IsDefined(typeof(TCode), resultCode))
                return _fallbackKey;
            string name = System.Enum.GetName(typeof(TCode), resultCode);
            if (name == "Invalid")
                return _fallbackKey;
            return _keyPrefix + name;
        }

        /// <summary>
        /// The conventional key first, the shared fallback key next, and the raw diagnostic last — the first that
        /// resolves to real text wins, so a missing localization row never leaves the user with a blank toast.
        /// </summary>
        string ResolveMessage(string key, string diagnostic)
        {
            if (_messages.TryGet(key, out string message))
                return message;
            if (key != _fallbackKey && _messages.TryGet(_fallbackKey, out message))
                return message;
            return diagnostic;
        }
    }
}
