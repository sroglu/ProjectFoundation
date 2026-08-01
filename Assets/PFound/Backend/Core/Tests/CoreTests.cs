using System;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.Backend.Core
{
    internal static class CoreTests
    {
        public static void Run()
        {
            SessionStoreBindAndRetrieve();
            SessionStoreUnbind();
            SessionStoreBindThrowsOnDuplicate();
            SessionStoreTryGetReturnsFalseWhenNotBound();
            SessionStoreMultiplePeers();
            SessionStoreUnbindRemovesSession();
            OperationHandlerContract();
            NotifyHandlerContract();
            PlayerSessionStruct();
            AuthResultStructSuccess();
            AuthResultStructFailure();
            AuthResultErrorCode();
        }

        static void SessionStoreBindAndRetrieve()
        {
            var store = new InMemorySessionStore();
            var session = new PlayerSession { PlayerId = 123L, AuthToken = "token123" };

            store.Bind(1, session);

            TestKit.Check(store.TryGet(1, out var retrieved), "session found after bind");
            TestKit.Check(retrieved.PlayerId == 123L, "PlayerId matches");
            TestKit.Check(retrieved.AuthToken == "token123", "AuthToken matches");
        }

        static void SessionStoreUnbind()
        {
            var store = new InMemorySessionStore();
            var session = new PlayerSession { PlayerId = 789L, AuthToken = "token789" };

            store.Bind(3, session);
            store.Unbind(3);

            TestKit.Check(!store.TryGet(3, out _), "TryGet returns false after unbind");
        }

        static void SessionStoreBindThrowsOnDuplicate()
        {
            var store = new InMemorySessionStore();
            var session1 = new PlayerSession { PlayerId = 111L, AuthToken = "token111" };
            var session2 = new PlayerSession { PlayerId = 222L, AuthToken = "token222" };

            store.Bind(4, session1);
            TestKit.Throws<InvalidOperationException>(() => store.Bind(4, session2), "bind throws on duplicate peer");
        }

        static void SessionStoreTryGetReturnsFalseWhenNotBound()
        {
            var store = new InMemorySessionStore();
            TestKit.Check(!store.TryGet(999, out _), "TryGet returns false for unbound peer");
        }

        static void SessionStoreMultiplePeers()
        {
            var store = new InMemorySessionStore();
            var session1 = new PlayerSession { PlayerId = 100L, AuthToken = "token1" };
            var session2 = new PlayerSession { PlayerId = 200L, AuthToken = "token2" };

            store.Bind(1, session1);
            store.Bind(2, session2);

            TestKit.Check(store.TryGet(1, out var s1) && s1.PlayerId == 100L, "first peer bound correctly");
            TestKit.Check(store.TryGet(2, out var s2) && s2.PlayerId == 200L, "second peer bound correctly");
        }

        static void SessionStoreUnbindRemovesSession()
        {
            var store = new InMemorySessionStore();
            var session = new PlayerSession { PlayerId = 333L, AuthToken = "token333" };

            store.Bind(5, session);
            TestKit.Check(store.TryGet(5, out _), "session exists before unbind");

            store.Unbind(5);
            TestKit.Check(!store.TryGet(5, out _), "session removed after unbind");
        }

        static void OperationHandlerContract()
        {
            var handler = new StubOperationHandler();
            IOperationHandler<string, string> iface = handler;
            TestKit.Check(iface != null, "OperationHandler interface implemented");
        }

        static void NotifyHandlerContract()
        {
            var handler = new StubNotifyHandler();
            INotifyHandler<string> iface = handler;
            TestKit.Check(iface != null, "NotifyHandler interface implemented");
        }

        static void PlayerSessionStruct()
        {
            var session = new PlayerSession
            {
                PlayerId = 999L,
                AuthToken = "testtoken"
            };

            TestKit.Check(session.PlayerId == 999L, "PlayerSession PlayerId property");
            TestKit.Check(session.AuthToken == "testtoken", "PlayerSession AuthToken property");
        }

        static void AuthResultStructSuccess()
        {
            var result = new AuthResult
            {
                Success = true,
                PlayerId = 555L,
                ErrorCode = ""
            };

            TestKit.Check(result.Success, "AuthResult Success true");
            TestKit.Check(result.PlayerId == 555L, "AuthResult PlayerId success case");
        }

        static void AuthResultStructFailure()
        {
            var result = new AuthResult
            {
                Success = false,
                PlayerId = 0L,
                ErrorCode = "InvalidPassword"
            };

            TestKit.Check(!result.Success, "AuthResult Success false");
            TestKit.Check(result.PlayerId == 0L, "AuthResult PlayerId failure case");
        }

        static void AuthResultErrorCode()
        {
            var result = new AuthResult
            {
                Success = false,
                PlayerId = 0L,
                ErrorCode = "UserNotFound"
            };

            TestKit.Check(result.ErrorCode == "UserNotFound", "AuthResult ErrorCode property");
        }

        sealed class StubOperationHandler : IOperationHandler<string, string>
        {
            public Task<string> HandleAsync(int peerId, string request, CancellationToken ct)
            {
                return Task.FromResult("reply");
            }
        }

        sealed class StubNotifyHandler : INotifyHandler<string>
        {
            public void Handle(int peerId, string notify)
            {
            }
        }
    }
}
