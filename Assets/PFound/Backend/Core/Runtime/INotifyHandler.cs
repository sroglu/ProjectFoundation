namespace PFound.Backend.Core
{
    /// <summary>Sync handler for a one-way notification.</summary>
    public interface INotifyHandler<in TNotify>
    {
        void Handle(int peerId, TNotify notify);
    }
}
