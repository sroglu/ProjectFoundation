using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PFound.ServerOperation.Core.Tests
{
    // ---- sample request / response the tests drive (a game would author typed wire messages) ----

    internal sealed class ProbeRequest
    {
        public int Value;
    }

    internal sealed class ProbeResponse
    {
        public int Value;
    }

    /// <summary>
    /// Stub transport. Counts sends so a test can assert "sent once / never sent", and can optionally hold a
    /// call open (<see cref="Pending"/>) to model an in-flight round-trip for the single-flight tests.
    /// </summary>
    internal sealed class ProbeTransport : IServerOperationTransport<ProbeRequest, ProbeResponse>
    {
        public int SendCount;
        public ProbeResponse Response = new ProbeResponse();
        public TaskCompletionSource<ProbeResponse> Pending;
        public CancellationToken LastCancellation;

        public Task<ProbeResponse> SendAsync(ProbeRequest request, CancellationToken cancellation)
        {
            SendCount++;
            LastCancellation = cancellation;
            if (Pending != null)
                return Pending.Task;
            return Task.FromResult(Response);
        }
    }

    internal sealed class ProbeFailurePresenter : IServerOperationFailurePresenter<ServerOperationResult>
    {
        public int Count;
        public ServerOperationResult Last;

        public void PresentFailure(ServerOperationResult result)
        {
            Count++;
            Last = result;
        }
    }

    internal sealed class ProbeSuccessSink : IServerOperationSuccessSink<ServerOperationResult>
    {
        public int Count;
        public ServerOperationResult Last;

        public void OnSuccess(ServerOperationResult result)
        {
            Count++;
            Last = result;
        }
    }

    internal sealed class ProbeAnalytics : IServerOperationAnalytics
    {
        public int Count;
        public string LastOperation;
        public bool LastSuccess;

        public void RecordOutcome(string operationName, IServerOperationResult result)
        {
            Count++;
            LastOperation = operationName;
            LastSuccess = result.IsSuccess;
        }
    }

    internal sealed class ProbeLoadingIndicator : IServerOperationLoadingIndicator
    {
        public int ShowCount;
        public int HideCount;

        public void Show() => ShowCount++;
        public void Hide() => HideCount++;

        public void Reset()
        {
            ShowCount = 0;
            HideCount = 0;
        }
    }

    /// <summary>
    /// A concrete operation whose behaviour is steered by delegates and whose every lifecycle step is logged,
    /// so the suite can assert ordering, short-circuiting, and cancellation without one subclass per case.
    /// </summary>
    internal class RecordingOperation : ServerOperation<ProbeRequest, ProbeResponse, ServerOperationResult>
    {
        public readonly List<string> Log;
        public string Tag = "op";

        public Func<ServerOperationResult> PreCheckResult = () => ServerOperationResult.Success();
        public Func<ProbeResponse, ServerOperationResult> InterpretResult = _ => ServerOperationResult.Success();
        public Func<ServerOperationResult, CancellationToken, Task> PostEffects = (_, __) => Task.CompletedTask;
        public string OverrideKey;

        public RecordingOperation(
            ServerOperationContext<ProbeRequest, ProbeResponse, ServerOperationResult> context,
            List<string> log) : base(context)
        {
            Log = log;
        }

        protected override string DuplicateKey => OverrideKey ?? base.DuplicateKey;

        protected override ServerOperationResult PreCheck()
        {
            Log.Add(Tag + ".precheck");
            return PreCheckResult();
        }

        protected override ProbeRequest BuildRequest()
        {
            Log.Add(Tag + ".build");
            return new ProbeRequest();
        }

        protected override ServerOperationResult Interpret(ProbeResponse response)
        {
            Log.Add(Tag + ".interpret");
            return InterpretResult(response);
        }

        protected override void ApplySuccess(ServerOperationResult result)
        {
            Log.Add(Tag + ".apply");
        }

        protected override Task RunPostEffectsAsync(ServerOperationResult result, CancellationToken cancellation)
        {
            Log.Add(Tag + ".posteffects");
            return PostEffects(result, cancellation);
        }
    }
}
