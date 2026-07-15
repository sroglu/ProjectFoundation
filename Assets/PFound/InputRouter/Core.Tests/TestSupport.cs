using System.Collections.Generic;

namespace PFound.InputRouter.Tests
{
    /// <summary>A gameplay-flavoured sample intent carrying a scalar strength.</summary>
    internal struct FireIntent : IIntent
    {
        public float Strength;
    }

    /// <summary>A second gameplay intent, used to prove per-type isolation and ordering.</summary>
    internal struct JumpIntent : IIntent
    {
        public int Charges;
    }

    /// <summary>A system-group intent (e.g. pause) exempt from the gameplay gate.</summary>
    internal struct PauseIntent : IIntent
    {
    }

    /// <summary>
    /// A hand-driven source: the test sets <see cref="Active"/> and <see cref="Payload"/> and
    /// counts how many times the router polled it. Stands in for a real input backend.
    /// </summary>
    internal sealed class FakeSource<TIntent> : IIntentSource<TIntent> where TIntent : struct, IIntent
    {
        public bool Active;
        public TIntent Payload;
        public int Polls;

        public IntentReading<TIntent> Read()
        {
            Polls++;
            return Active
                ? IntentReading<TIntent>.Active(Payload)
                : IntentReading<TIntent>.Inactive;
        }
    }

    /// <summary>Records the phases/payloads a subscription observed, in order.</summary>
    internal sealed class PhaseLog<TIntent> where TIntent : struct, IIntent
    {
        public readonly List<IntentPhase> Phases = new List<IntentPhase>();
        public readonly List<TIntent> Payloads = new List<TIntent>();

        public void Record(IntentContext<TIntent> context)
        {
            Phases.Add(context.Phase);
            Payloads.Add(context.Payload);
        }

        public int Count
        {
            get { return Phases.Count; }
        }

        public IntentPhase Last
        {
            get { return Phases[Phases.Count - 1]; }
        }
    }
}
