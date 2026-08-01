using PFound.ServerOperationFlow.Core;

namespace GameSpecific.Networking
{
    /// <summary>Game flow base that fixes the result type to <see cref="ServerOperationResult{OpResult}"/> so concrete flows never repeat it.</summary>
    public abstract class GameServerOperationFlow<TRequest, TResponse>
        : ServerOperationFlow<TRequest, TResponse, ServerOperationResult<OpResult>>
    {
    }
}
