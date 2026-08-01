using System.Threading;
using PFound.NetworkLayer;
using PFound.ServerOperationFlow.Core;

namespace GameSpecific.Networking
{
    /// <summary>
    /// How the game wires its network operations exactly once at boot. Two steps:
    /// <list type="number">
    /// <item><description>Enrol every <c>[RemoteProcedure]</c> / <c>[Notify]</c> partial in this assembly into
    /// the catalog — the generator-emitted <c>GeneratedMessages.RegisterAll</c> aggregator does them all, so
    /// no operation can be forgotten.</description></item>
    /// <item><description>Publish the connected peer as the ambient <c>NetworkClient.Current</c> so the
    /// generated <c>&lt;Op&gt;.Execute(args)</c> / <c>&lt;Notify&gt;.Notify(args)</c> entry points have a
    /// client to send through — set it once, here, and never thread a peer through call sites.</description></item>
    /// </list>
    /// Typical boot: build the catalog, <c>RegisterOperations(catalog)</c>, construct the
    /// <c>ClientPeer(link, catalog, options)</c>, then <c>UseAsAmbientClient(peer)</c> (or the combined
    /// <see cref="Configure"/>).
    /// </summary>
    public static class GameNetworkSetup
    {
        /// <summary>
        /// The production body codec for this game's wire DTOs: MessagePack's AOT source-generated formatters,
        /// composed from this assembly's <see cref="GameNetworkResolver"/> (pushed explicitly — no reflection
        /// discovery) ahead of the builtin primitive/collection resolver. Build the catalog with this codec.
        /// </summary>
        public static MessagePackBodyCodec CreateCodec() => new MessagePackBodyCodec(GameNetworkResolver.Instance);

        /// <summary>A ready catalog: the source-generated codec plus every operation enrolled.</summary>
        public static MessageCatalog CreateCatalog()
        {
            var catalog = new MessageCatalog(CreateCodec());
            RegisterOperations(catalog);
            return catalog;
        }

        /// <summary>Enrol every operation declared in this assembly into the catalog the peer uses.</summary>
        public static void RegisterOperations(MessageCatalog catalog) => GeneratedMessages.RegisterAll(catalog);

        /// <summary>
        /// Publish the connected peer as the ambient client the generated <c>Execute</c>/<c>Send</c> entry
        /// points send through. Call once at startup, after the peer is built.
        /// </summary>
        public static void UseAsAmbientClient(ClientPeer clientPeer) => NetworkClient.Current = clientPeer;

        /// <summary>
        /// Publish the ambient host so a flow resolves its context with only the game's own params. Call once at boot.
        /// <paramref name="sessionCancellation"/> is the gameloop-cancelled token that aborts flows on session/scene end.
        /// </summary>
        public static void UseAsAmbientServerOperationHost(
            CancellationToken sessionCancellation = default,
            ServerOperationGate gate = null,
            IServerOperationAnalytics analytics = null)
        {
            var host = new ServerOperationHost<ServerOperationResult<OpResult>>(
                new GameServerOperationTransportFactory(),
                new CodeToastFailurePresenter<OpResult>(new DebugToastPresenter()));
            host.Gate = gate ?? ServerOperationGate.DedupOnly();
            host.Cancellation = sessionCancellation;
            if (analytics != null)
                host.Analytics = analytics;
            ServerOperationHost<ServerOperationResult<OpResult>>.Current = host;
        }

        /// <summary>All boot steps: enrol operations, make the peer ambient, publish the flow host. Pass a gameloop-cancelled token to abort flows on teardown.</summary>
        public static void Configure(ClientPeer clientPeer, MessageCatalog catalog, CancellationToken sessionCancellation = default)
        {
            RegisterOperations(catalog);
            UseAsAmbientClient(clientPeer);
            UseAsAmbientServerOperationHost(sessionCancellation);
        }
    }
}
