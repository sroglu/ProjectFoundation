namespace PFound.ServerOperation.Core
{
    /// <summary>
    /// A ready-made <see cref="IServerOperationResult"/> so a game (or a test) need not hand-roll one for
    /// the common case: a success flag, a status/result code, and an optional diagnostic message. Games are
    /// free to supply their own richer result type instead — the lifecycle only depends on the interface.
    /// </summary>
    public sealed class ServerOperationResult : IServerOperationResult
    {
        public bool IsSuccess { get; }
        public int ResultCode { get; }
        public string ErrorMessage { get; }

        ServerOperationResult(bool isSuccess, int resultCode, string errorMessage)
        {
            IsSuccess = isSuccess;
            ResultCode = resultCode;
            ErrorMessage = errorMessage;
        }

        public static ServerOperationResult Success(int resultCode = 0)
            => new ServerOperationResult(true, resultCode, null);

        public static ServerOperationResult Failure(int resultCode, string errorMessage = null)
            => new ServerOperationResult(false, resultCode, errorMessage);
    }
}
