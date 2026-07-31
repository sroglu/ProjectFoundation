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

    /// <summary>Transport factory that hands back one shared <see cref="ProbeTransport"/> for the probe types.</summary>
    internal sealed class ProbeTransportFactory : IServerOperationTransportFactory
    {
        public readonly ProbeTransport Transport = new ProbeTransport();

        public IServerOperationTransport<TRequest, TResponse> CreateTransport<TRequest, TResponse>()
            => (IServerOperationTransport<TRequest, TResponse>)(object)Transport;
    }

    /// <summary>Outcome channels that hand back one shared presenter + sink for <see cref="ServerOperationResult"/>.</summary>
    internal sealed class ProbeResultChannels : IServerOperationResultChannels
    {
        public readonly ProbeFailurePresenter Presenter = new ProbeFailurePresenter();
        public readonly ProbeSuccessSink Sink = new ProbeSuccessSink();

        public IServerOperationFailurePresenter<TResult> FailurePresenter<TResult>() where TResult : IServerOperationResult
            => (IServerOperationFailurePresenter<TResult>)(object)Presenter;

        public IServerOperationSuccessSink<TResult> SuccessSink<TResult>() where TResult : IServerOperationResult
            => (IServerOperationSuccessSink<TResult>)(object)Sink;
    }

    /// <summary>A sample game result enum for the localized-toast presenter tests — mirrors an Invalid=0 sentinel.</summary>
    internal enum ProbeOpResult
    {
        Invalid = 0,
        AmountNotPositive = 1,
        InsufficientBalance = 2,
    }

    /// <summary>Dictionary-backed <see cref="IUserMessageSource"/> so a test can seed exactly which keys resolve.</summary>
    internal sealed class ProbeUserMessageSource : IUserMessageSource
    {
        readonly Dictionary<string, string> _messages = new Dictionary<string, string>();

        public ProbeUserMessageSource Add(string key, string message) { _messages[key] = message; return this; }

        public bool TryGet(string key, out string message) => _messages.TryGetValue(key, out message);
    }

    /// <summary>Captures the last message shown so a test can assert what the presenter resolved.</summary>
    internal sealed class ProbeToastPresenter : IToastPresenter
    {
        public int Count;
        public string Last;

        public void Show(string message) { Count++; Last = message; }
    }

    /// <summary>A probe flow constructed WITHOUT an explicit context — it resolves one from the ambient host.</summary>
    internal sealed class AmbientRecordingOperation : ServerOperationFlow<ProbeRequest, ProbeResponse, ServerOperationResult>
    {
        public readonly List<string> Log = new List<string>();

        protected override ServerOperationResult PreCheck() { Log.Add("precheck"); return ServerOperationResult.Success(); }
        protected override ProbeRequest BuildRequest() { Log.Add("build"); return new ProbeRequest(); }
        protected override ServerOperationResult Interpret(ProbeResponse response) { Log.Add("interpret"); return ServerOperationResult.Success(); }
        protected override void ApplySuccess(ServerOperationResult result) => Log.Add("apply");
    }

    /// <summary>
    /// A concrete operation whose behaviour is steered by delegates and whose every lifecycle step is logged,
    /// so the suite can assert ordering, short-circuiting, and cancellation without one subclass per case.
    /// </summary>
    internal class RecordingOperation : ServerOperationFlow<ProbeRequest, ProbeResponse, ServerOperationResult>
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
