namespace GameSpecific.Backend.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using PFound.NetworkLayer;
    using PFound.Backend.Core;
    using GameSpecific.Networking.Operations;
    using GameSpecific.Networking.Data;
    using GameSpecific.Backend.Repositories;
    using GameSpecific.Backend.Operations;

    public static class BackendHandlerTests
    {
        public static void Main()
        {
            RunAllTests();
        }

        static void RunAllTests()
        {
            Console.WriteLine("Running Backend Handler Tests...\n");

            TestSpendCoinsHandlerSuccess();
            TestSpendCoinsHandlerInsufficientBalance();
            TestGetPlayerDataHandler();
            TestJoinAllianceHandlerSuccess();
            TestJoinAllianceHandlerInvalidAlliance();

            Console.WriteLine("\nAll tests passed!");
        }

        static void TestSpendCoinsHandlerSuccess()
        {
            Console.WriteLine("Test: SpendCoinsHandler - Success");

            var wallets = new InMemoryWalletRepository();
            var sessions = new InMemorySessionStore();
            sessions.Bind(1, new PlayerSession { PlayerId = 1 });
            var handler = new SpendCoinsHandler(wallets, sessions);

            var request = new SpendCoinsOperation.RequestMessage
            {
                Content = new SpendRequest { Amount = 100 }
            };

            var task = handler.HandleAsync(1, request, CancellationToken.None);
            var reply = task.Result;

            if (reply.Status != ReplyStatus.Ok)
                throw new AssertException("Expected ReplyStatus.Ok");
            if (reply.Content.NewBalance != 900)
                throw new AssertException($"Expected 900, got {reply.Content.NewBalance}");

            Console.WriteLine("  ✓ Passed\n");
        }

        static void TestSpendCoinsHandlerInsufficientBalance()
        {
            Console.WriteLine("Test: SpendCoinsHandler - Insufficient Balance");

            var wallets = new InMemoryWalletRepository();
            var sessions = new InMemorySessionStore();
            sessions.Bind(1, new PlayerSession { PlayerId = 1 });
            var handler = new SpendCoinsHandler(wallets, sessions);

            var request = new SpendCoinsOperation.RequestMessage
            {
                Content = new SpendRequest { Amount = 2000 }
            };

            var task = handler.HandleAsync(1, request, CancellationToken.None);
            var reply = task.Result;

            if (reply.Status != ReplyStatus.Faulted)
                throw new AssertException($"Expected ReplyStatus.Faulted, got {reply.Status}");

            Console.WriteLine("  ✓ Passed\n");
        }

        static void TestGetPlayerDataHandler()
        {
            Console.WriteLine("Test: GetPlayerDataHandler");

            var players = new InMemoryPlayerRepository();
            var wallets = new InMemoryWalletRepository();
            var sessions = new InMemorySessionStore();
            sessions.Bind(1, new PlayerSession { PlayerId = 1 });
            var handler = new GetPlayerDataHandler(players, wallets, sessions);

            var request = new GetPlayerDataOperation.RequestMessage
            {
                Content = new PlayerId { Value = 1 }
            };

            var task = handler.HandleAsync(1, request, CancellationToken.None);
            var reply = task.Result;

            if (reply.Status != ReplyStatus.Ok)
                throw new AssertException("Expected ReplyStatus.Ok");
            if (reply.Content.PlayerId.Value != 1)
                throw new AssertException($"Expected PlayerId 1, got {reply.Content.PlayerId.Value}");
            if (reply.Content.Name != "Player One")
                throw new AssertException($"Expected name 'Player One', got '{reply.Content.Name}'");
            if (reply.Content.Level != 5)
                throw new AssertException($"Expected level 5, got {reply.Content.Level}");

            Console.WriteLine("  ✓ Passed\n");
        }

        static void TestJoinAllianceHandlerSuccess()
        {
            Console.WriteLine("Test: JoinAllianceHandler - Success");

            var sessions = new InMemorySessionStore();
            sessions.Bind(1, new PlayerSession { PlayerId = 1 });
            var handler = new JoinAllianceHandler(sessions);

            var request = new JoinAllianceOperation.RequestMessage
            {
                Content = new JoinAllianceRequest
                {
                    AllianceId = new AllianceId { Value = 123 },
                    PlayerId = new PlayerId { Value = 1 }
                }
            };

            var task = handler.HandleAsync(1, request, CancellationToken.None);
            var reply = task.Result;

            if (reply.Status != ReplyStatus.Ok)
                throw new AssertException("Expected ReplyStatus.Ok");
            if (reply.Content.Id.Value != 123)
                throw new AssertException($"Expected AllianceId 123, got {reply.Content.Id.Value}");

            Console.WriteLine("  ✓ Passed\n");
        }

        static void TestJoinAllianceHandlerInvalidAlliance()
        {
            Console.WriteLine("Test: JoinAllianceHandler - Invalid Alliance ID");

            var sessions = new InMemorySessionStore();
            sessions.Bind(1, new PlayerSession { PlayerId = 1 });
            var handler = new JoinAllianceHandler(sessions);

            var request = new JoinAllianceOperation.RequestMessage
            {
                Content = new JoinAllianceRequest
                {
                    AllianceId = new AllianceId { Value = 0 },
                    PlayerId = new PlayerId { Value = 1 }
                }
            };

            var task = handler.HandleAsync(1, request, CancellationToken.None);
            var reply = task.Result;

            if (reply.Status != ReplyStatus.Faulted)
                throw new AssertException($"Expected ReplyStatus.Faulted, got {reply.Status}");

            Console.WriteLine("  ✓ Passed\n");
        }
    }

    class AssertException : Exception
    {
        public AssertException(string message) : base(message) { }
    }
}
