namespace PFound.Backend.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Async handler for a request/reply operation.</summary>
    public interface IOperationHandler<TRequest, TReply>
    {
        Task<TReply> HandleAsync(int peerId, TRequest request, CancellationToken ct);
    }
}
