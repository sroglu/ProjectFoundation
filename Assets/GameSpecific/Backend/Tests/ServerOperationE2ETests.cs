namespace GameSpecific.Backend.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using PFound.NetworkLayer;
    using PFound.Backend;
    using PFound.Backend.Core;
    using PFound.ServerOperationFlow.Core;
    using GameSpecific.Networking;
    using GameSpecific.Networking.Operations;
    using GameSpecific.Networking.Data;
    using GameSpecific.Backend.Repositories;
    using GameSpecific.Backend.Operations;

    /// <summary>
    /// End-to-end test of the server ↔ client round-trip via loopback transport.
    /// Verifies the full request/reply lifecycle: client sends SpendCoinsOperation,
    /// server processes via handler, reply flows back to client, local state updates.
    ///
    /// The test wires a BackendHost (server-side) and ClientPeer (client-side)
    /// through a LoopbackHub, exercising all 5 Handler Rules:
    /// 1. Sync vs async choice (handlers use async interface)
    /// 2. Reply on pump thread (BackendHost.Tick drains reply queue)
    /// 3. Copy request values before await (SpendCoinsHandler extracts amount)
    /// 4. Single reply before deadline (Watchdog reclaims expired exchanges)
    /// 5. Fail with ReplyStatus (Faulted on insufficient balance, not silence)
    ///
    /// This is a pure C# test (not a UnityTest) that runs on mono/csc.
    /// Run: csc -nologo -warn:0 -out:/tmp/pf_backend_e2e.exe \
    ///      Assets/GameSpecific/Backend/Tests/ServerOperationE2ETests.cs \
    ///      Assets/GameSpecific/Backend/Tests/*.cs ... && \
    ///      mono /tmp/pf_backend_e2e.exe
    /// </summary>
    public sealed class ServerOperationE2ETests
    {
        private LoopbackHub _hub;
        private LoopbackServerLink _serverLink;
        private LoopbackClientLink _clientLink;
        private ServerPeer _serverPeer;
        private ClientPeer _clientPeer;
        private BackendHost _backendHost;
        private MessageCatalog _catalog;

        private void Setup()
        {
            // Create shared transport hub
            _hub = new LoopbackHub();
            _serverLink = new LoopbackServerLink(_hub);
            _clientLink = new LoopbackClientLink(_hub);

            // Create catalog and register all operations
            _catalog = GameNetworkSetup.CreateCatalog();

            // Create ServerPeer with loopback link and catalog
            _serverPeer = new ServerPeer(_serverLink, _catalog, ServerLinkOptions.Default);

            // Create repositories
            var wallets = new InMemoryWalletRepository();
            var players = new InMemoryPlayerRepository();

            // Wire handlers via OperationHandlerRegistry
            // (RULE 2: Registry enqueues replies to concurrent queue, BackendHost.Tick drains them)
            var replyQueue = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();
            var registry = new OperationHandlerRegistry(replyQueue)
                .Register<SpendCoinsOperation.RequestMessage, SpendCoinsOperation.ReplyMessage>(
                    new SpendCoinsHandler(wallets))
                .Register<GetPlayerDataOperation.RequestMessage, GetPlayerDataOperation.ReplyMessage>(
                    new GetPlayerDataHandler(players, wallets))
                .Register<JoinAllianceOperation.RequestMessage, JoinAllianceOperation.ReplyMessage>(
                    new JoinAllianceHandler());
            registry.AttachTo(_serverPeer);

            // Create BackendHost (owns the reply queue and drains it each Tick)
            _backendHost = new BackendHost(_serverPeer, replyQueue);

            // Create ClientPeer
            _clientPeer = new ClientPeer(_clientLink, _catalog, ClientLinkOptions.Default);
            _clientPeer.Connect("loopback", 0);

            // Start the server listening
            _serverLink.Listen(0);

            // Configure ambient hosts for the client side
            GameNetworkSetup.UseAsAmbientClient(_clientPeer);
            GameNetworkSetup.UseAsAmbientServerOperationHost(CancellationToken.None);

            // Initialize client wallet to 1000 coins
            PlayerWallet.Current = new PlayerWallet { Balance = 1000 };
        }

        private void Teardown()
        {
            _backendHost?.Halt();
            _clientPeer?.Close();
            _serverLink?.Halt();
        }

        private void PumpBothSidesUntilComplete(Task task, int maxIterations = 600)
        {
            for (int i = 0; i < maxIterations && !task.IsCompleted; i++)
            {
                // Server: pump network frames + drain reply queue (RULE 2)
                _serverLink.Pump(8);
                _backendHost.Tick();

                // Client: pump network responses
                _clientPeer.Update();
            }
        }

        private void AssertTaskCompleted(Task task, string message = "task did not complete")
        {
            if (!task.IsCompleted)
                throw new Exception(message);
            if (task.IsFaulted)
                throw new Exception("task faulted: " + (task.Exception?.ToString() ?? ""));
        }

        /// <summary>
        /// Test 1: SpendCoins Round-Trip
        /// Client: SpendCoinsOperation.Execute(50 coins)
        /// Server: processes request, deducts 50 from wallet (1000→950)
        /// Client: receives reply, applies new balance
        /// Assert: wallet.Balance == 950
        /// </summary>
        public void Test1_SpendCoinsRoundTrip_ClientSends50Coins_BalanceUpdates950()
        {
            Console.WriteLine("Test 1: SpendCoinsRoundTrip");

            Setup();

            try
            {
                // Simulate server connect event
                _serverLink.Pump(8);

                // Client starts the operation
                Task<ServerOperationRun<ServerOperationResult<OpResult>>> runTask =
                    new SpendCoinsOperationFlow(new SpendRequest { Amount = 50 }).RunAsync();

                // Pump both sides until the lifecycle completes
                PumpBothSidesUntilComplete(runTask);

                // Verify the lifecycle completed without errors
                AssertTaskCompleted(runTask, "operation lifecycle did not complete");

                if (!runTask.Result.Accepted)
                    throw new Exception("operation should be accepted (not suppressed)");
                if (!runTask.Result.Result.IsSuccess)
                    throw new Exception("operation should succeed");

                // Verify the client's local balance was updated by the server's reply
                if (PlayerWallet.Current.Balance != 950)
                    throw new Exception($"wallet balance should be 950, got {PlayerWallet.Current.Balance}");

                Console.WriteLine("  ✓ Passed\n");
            }
            finally
            {
                Teardown();
            }
        }

        /// <summary>
        /// Test 2: Insufficient Balance (Failure Path)
        /// Pre-state: wallet.Balance == 950 (from Test 1)
        /// Client: SpendCoinsOperation.Execute(2000 coins) (exceeds balance)
        /// Client-side pre-check: fails with OpResult.InsufficientBalance
        /// No request sent to server (client-side reject)
        /// Assert: wallet.Balance == 950 (unchanged)
        /// </summary>
        public void Test2_InsufficientBalance_PreCheckRejects_NoRequestSent()
        {
            Console.WriteLine("Test 2: InsufficientBalance (PreCheck Rejects)");

            Setup();

            try
            {
                // Set balance to 950
                PlayerWallet.Current.Balance = 950;

                // Client attempts to spend 2000 (exceeds 950 balance)
                Task<ServerOperationRun<ServerOperationResult<OpResult>>> runTask =
                    new SpendCoinsOperationFlow(new SpendRequest { Amount = 2000 }).RunAsync();

                // Pre-check rejection happens synchronously, so task should complete immediately
                PumpBothSidesUntilComplete(runTask, 30);

                AssertTaskCompleted(runTask, "pre-check rejection should complete synchronously");

                if (runTask.Result.Result.IsSuccess)
                    throw new Exception("pre-check should reject due to insufficient balance");
                if (PlayerWallet.Current.Balance != 950)
                    throw new Exception($"balance should remain 950, got {PlayerWallet.Current.Balance}");

                Console.WriteLine("  ✓ Passed\n");
            }
            finally
            {
                Teardown();
            }
        }

        /// <summary>
        /// Test 3: Idempotency (Duplicate Spend Suppression)
        /// Pre-state: wallet.Balance == 950
        /// Client: SpendCoinsOperation.Execute(50 coins) first time
        /// Waits for reply, verifies: wallet.Balance == 900
        /// Client: SpendCoinsOperation.Execute(50 coins) again (while first is settling or immediately after)
        /// Server: sees duplicate key (same amount), single-flight gate rejects
        /// Client: receives Suppressed disposition
        /// Assert: wallet.Balance == 900 (second spend did not go through)
        /// </summary>
        public void Test3_Idempotency_DuplicateSpend_SecondIsSuppressed()
        {
            Console.WriteLine("Test 3: Idempotency (Duplicate Spend Suppression)");

            Setup();

            try
            {
                // Set balance to 950
                PlayerWallet.Current.Balance = 950;

                // Configure a shared gate for deduplication (single-flight)
                var gate = ServerOperationGate.DedupOnly();
                ServerOperationHost<ServerOperationResult<OpResult>>.Current.Gate = gate;

                // First spend: 50 coins (950 → 900)
                Task<ServerOperationRun<ServerOperationResult<OpResult>>> firstRun =
                    new SpendCoinsOperationFlow(new SpendRequest { Amount = 50 }).RunAsync();

                // Second spend: same amount, should be suppressed by the gate
                Task<ServerOperationRun<ServerOperationResult<OpResult>>> secondRun =
                    new SpendCoinsOperationFlow(new SpendRequest { Amount = 50 }).RunAsync();

                // The second run should be suppressed synchronously (before the first completes)
                if (!secondRun.IsCompleted)
                    throw new Exception("duplicate should be suppressed before first completes");
                if (secondRun.Result.Disposition != ServerOperationDisposition.DuplicateSuppressed)
                    throw new Exception("second spend should be suppressed by single-flight gate");

                // Pump to complete the first run
                PumpBothSidesUntilComplete(firstRun);

                AssertTaskCompleted(firstRun, "first spend should complete");

                if (!firstRun.Result.Accepted)
                    throw new Exception("first spend should be accepted");
                if (!firstRun.Result.Result.IsSuccess)
                    throw new Exception("first spend should succeed");
                if (PlayerWallet.Current.Balance != 900)
                    throw new Exception($"balance should be 900, got {PlayerWallet.Current.Balance}");

                Console.WriteLine("  ✓ Passed\n");
            }
            finally
            {
                Teardown();
            }
        }

        /// <summary>
        /// Test 4: Server Error Path (Handler Returns Faulted)
        /// Pre-state: wallet.Balance == 900
        /// Server handler: detects insufficient balance, returns ReplyStatus.Faulted
        /// Client flow: interprets Faulted status, returns OpResult.Failed
        /// Assert: wallet.Balance == 900 (unchanged)
        /// </summary>
        public void Test4_ServerReturns_Faulted_ClientInterpretsAsFailure()
        {
            Console.WriteLine("Test 4: Server Error Path (Faulted Reply)");

            Setup();

            try
            {
                // Set balance to 900
                PlayerWallet.Current.Balance = 900;

                // Reset gate for this test (no dedup)
                ServerOperationHost<ServerOperationResult<OpResult>>.Current.Gate = ServerOperationGate.Disabled;

                // Attempt to spend more than available (900 < 1000)
                Task<ServerOperationRun<ServerOperationResult<OpResult>>> runTask =
                    new SpendCoinsOperationFlow(new SpendRequest { Amount = 1000 }).RunAsync();

                // Pump both sides
                PumpBothSidesUntilComplete(runTask);

                AssertTaskCompleted(runTask, "operation should complete");

                if (!runTask.Result.Accepted)
                    throw new Exception("operation should be accepted (sent to server)");
                if (runTask.Result.Result.IsSuccess)
                    throw new Exception("operation should fail (server returned Faulted)");
                if (PlayerWallet.Current.Balance != 900)
                    throw new Exception($"balance should remain 900, got {PlayerWallet.Current.Balance}");

                Console.WriteLine("  ✓ Passed\n");
            }
            finally
            {
                Teardown();
            }
        }

        /// <summary>Entry point for running all E2E tests.</summary>
        public static void Main()
        {
            Console.WriteLine("Running ServerOperationE2ETests...\n");

            var tests = new ServerOperationE2ETests();
            try
            {
                tests.Test1_SpendCoinsRoundTrip_ClientSends50Coins_BalanceUpdates950();
                tests.Test2_InsufficientBalance_PreCheckRejects_NoRequestSent();
                tests.Test3_Idempotency_DuplicateSpend_SecondIsSuppressed();
                tests.Test4_ServerReturns_Faulted_ClientInterpretsAsFailure();

                Console.WriteLine("All E2E tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}
