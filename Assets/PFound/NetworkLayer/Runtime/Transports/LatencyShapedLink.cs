using System;
using System.Collections.Generic;

namespace PFound.NetworkLayer
{
    public enum LatencyShape
    {
        /// <summary>Pass frames straight through with no added delay.</summary>
        Instant,
        /// <summary>Hold every inbound frame for a constant span.</summary>
        Constant,
        /// <summary>Hold each inbound frame for a uniformly random span in [Low, High].</summary>
        Scattered,
    }

    public readonly struct LatencyProfile
    {
        public readonly LatencyShape Shape;
        public readonly int LowMs;
        public readonly int HighMs;

        public LatencyProfile(LatencyShape shape, int lowMs, int highMs)
        {
            Shape = shape;
            LowMs = lowMs;
            HighMs = highMs;
        }

        public static LatencyProfile None => new LatencyProfile(LatencyShape.Instant, 0, 0);
        public static LatencyProfile Constant(int ms) => new LatencyProfile(LatencyShape.Constant, ms, ms);
        public static LatencyProfile Scattered(int lowMs, int highMs) => new LatencyProfile(LatencyShape.Scattered, lowMs, highMs);
    }

    /// <summary>
    /// A client-link decorator that defers inbound delivery to emulate network
    /// latency/jitter for tests. It wraps any real <see cref="IClientLink"/>,
    /// intercepts frames as the inner link pumps them, and holds each until its
    /// clock-driven release time — so a test can advance a <see cref="ManualClock"/>
    /// and assert that a reply is not observed until the emulated delay elapses.
    /// Purely a test-support seam; production traffic uses the bare link.
    /// </summary>
    public sealed class LatencyShapedLink : IClientLink
    {
        readonly IClientLink _inner;
        readonly IClock _clock;
        readonly Random _rng;
        readonly Queue<Held> _held = new Queue<Held>();

        public LatencyProfile Profile;

        struct Held
        {
            public byte[] Frame;
            public long ReleaseAtMs;
        }

        public event Action Opened;
        public event Action Closed;
        public event Action<ArraySegment<byte>> Received;

        public bool IsOpen => _inner.IsOpen;

        public LatencyShapedLink(IClientLink inner, IClock clock, LatencyProfile profile, int rngSeed = 20719)
        {
            _inner = inner;
            _clock = clock;
            Profile = profile;
            _rng = new Random(rngSeed);

            _inner.Opened += () => Opened?.Invoke();
            _inner.Closed += () => Closed?.Invoke();
            _inner.Received += Absorb;
        }

        public void Open(string host, int port) => _inner.Open(host, port);
        public void Close() => _inner.Close();
        public bool Deliver(ArraySegment<byte> frame) => _inner.Deliver(frame);

        public void Pump(int budget)
        {
            _inner.Pump(budget);      // pulls transport frames into _held via Absorb
            ReleaseDue(budget);       // emits the ones whose delay has elapsed
        }

        void Absorb(ArraySegment<byte> frame)
        {
            var clone = new byte[frame.Count];
            Buffer.BlockCopy(frame.Array, frame.Offset, clone, 0, frame.Count);
            _held.Enqueue(new Held { Frame = clone, ReleaseAtMs = _clock.NowMs + PickDelay() });
        }

        void ReleaseDue(int budget)
        {
            long now = _clock.NowMs;
            for (int i = 0; i < budget && _held.Count > 0 && _held.Peek().ReleaseAtMs <= now; i++)
                Received?.Invoke(new ArraySegment<byte>(_held.Dequeue().Frame));
        }

        int PickDelay()
        {
            switch (Profile.Shape)
            {
                case LatencyShape.Constant: return Profile.LowMs;
                case LatencyShape.Scattered: return _rng.Next(Profile.LowMs, Profile.HighMs + 1);
                default: return 0;
            }
        }
    }
}
