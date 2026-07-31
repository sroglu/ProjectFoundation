using System.Collections.Generic;
using PFound.LocalizationService;
using PFound.NetworkLayer;
using PFound.ServerOperation.Core;

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
        /// Publish the ambient <see cref="ServerOperationHost"/> so a flow can be constructed with only the game's
        /// own parameters (<c>new SpendCoinsOperationFlow(amount, wallet)</c>) — its base ctor resolves the
        /// context (transport + outcome seams + run policy) from here. The transport factory reads the ambient
        /// peer at call time, so this may run before or after <see cref="UseAsAmbientClient"/>. Call once at boot.
        /// The <paramref name="gate"/> defaults to a single-flight, no-spinner run policy shared across flows.
        /// </summary>
        public static void UseAsAmbientServerOperationHost(
            ServerOperationGate gate = null,
            IServerOperationAnalytics analytics = null)
        {
            var localization = CreateSampleLocalization();
            var failurePresenter = new LocalizedToastFailurePresenter<ServerOperationResult, OpResult>(
                new LocalizationUserMessages(localization),
                new DebugToastPresenter());

            var host = new ServerOperationHost(
                new GameServerOperationTransportFactory(),
                new GameServerOperationResultChannels(failurePresenter));
            host.Gate = gate ?? ServerOperationGate.DedupOnly();
            if (analytics != null)
                host.Analytics = analytics;
            ServerOperationHost.Current = host;
        }

        /// <summary>
        /// The sample's English user-message table, keyed by the <see cref="OpResult"/> convention
        /// (<c>"op.result.&lt;Name&gt;"</c>) plus the generic <c>op.result.Unknown</c> fallback, so a failed
        /// operation surfaces a real localized toast end-to-end. A shipping game swaps this in-memory seed for its
        /// content-file-backed <see cref="LocalizationService"/> — the failure pipeline needs no other change.
        /// </summary>
        static LocalizationService CreateSampleLocalization()
        {
            var english = new LanguageKey("en");
            var source = new InMemoryLocalizationSource().Add(english, new Dictionary<string, string>
            {
                ["op.result.AmountNotPositive"] = "Amount must be positive.",
                ["op.result.InsufficientBalance"] = "Not enough coins.",
                ["op.result.AllianceIdMissing"] = "Pick an alliance first.",
                ["op.result.AlreadyInAlliance"] = "You're already in an alliance.",
                ["op.result.ServerFaulted"] = "Something went wrong. Try again.",
                ["op.result.ServerRefused"] = "The server refused that action.",
                ["op.result.RequestExpired"] = "That took too long. Try again.",
                ["op.result.Unroutable"] = "That action isn't available right now.",
                ["op.result.Unknown"] = "Something went wrong.",
            });
            return new LocalizationService(source, english);
        }

        /// <summary>All boot steps in order: enrol the operations, make the peer ambient, publish the flow host.</summary>
        public static void Configure(ClientPeer clientPeer, MessageCatalog catalog)
        {
            RegisterOperations(catalog);
            UseAsAmbientClient(clientPeer);
            UseAsAmbientServerOperationHost();
        }
    }
}
