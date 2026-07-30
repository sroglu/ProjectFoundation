using System;
using PFound.Commerce.Core;

namespace PFound.Commerce
{
    /// <summary>The engine-side clock — wall-clock UTC seconds for ledger timestamps.</summary>
    public sealed class UnityCommerceClock : ICommerceClock
    {
        public long NowUnixSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
