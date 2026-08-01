namespace GameSpecific.Backend
{
    using System.Collections.Concurrent;
    using PFound.Backend;
    using PFound.Backend.Core;
    using PFound.NetworkLayer;
    using GameSpecific.Networking;
    using GameSpecific.Networking.Operations;
    using GameSpecific.Backend.Repositories;
    using GameSpecific.Backend.Auth;
    using GameSpecific.Backend.Operations;

    public static class GameBackendSetup
    {
        public static BackendHost CreateHost(int port)
        {
            // 1. Register all opcodes + reply types in catalog
            var catalog = GameNetworkSetup.CreateCatalog();

            // 2. Create ServerLink and ServerPeer with catalog
            var serverLink = new TelepathyServerLink(ServerLinkOptions.Default);
            var serverPeer = new ServerPeer(serverLink, catalog, ServerLinkOptions.Default);

            // 3. Create repos
            var wallets = new InMemoryWalletRepository();
            var players = new InMemoryPlayerRepository();

            // 4. Create session store for peer ↔ player binding
            var sessions = new InMemorySessionStore();

            // 5. Create authenticator
            var authenticator = new GameAuthenticator();

            // 6. Create & wire handlers
            var replyQueue = new ConcurrentQueue<System.Action>();
            var registry = new OperationHandlerRegistry(replyQueue)
                .Register<LoginOperation.RequestMessage, LoginOperation.ReplyMessage>(
                    new LoginHandler(authenticator, sessions))
                .Register<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>(
                    new SpendCoinsHandler(wallets, sessions))
                .Register<GetPlayerDataOperation.RequestMessage, GetPlayerDataOperation.ReplyMessage>(
                    new GetPlayerDataHandler(players, wallets, sessions))
                .Register<JoinAllianceOperation.RequestMessage, JoinAllianceOperation.ReplyMessage>(
                    new JoinAllianceHandler(sessions));
            registry.AttachTo(serverPeer);

            // 7. Create host
            var host = new BackendHost(serverPeer, replyQueue);
            return host;
        }
    }
}
