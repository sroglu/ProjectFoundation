using System;

namespace Kunai
{
    /// <summary>
    /// Marks a static field or property as a tweakable in the Kunai Inspector.
    /// Supported member types: <c>bool</c>, <c>int</c>, <c>float</c>,
    /// <c>double</c>, <c>string</c>, and any <c>enum</c>. Other types are
    /// skipped at registration time with a warning. Instance members are
    /// out of scope for v1.
    ///
    /// <para><c>Label</c> overrides the displayed label; <c>null</c> means
    /// "use member name".</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property,
                    AllowMultiple = false, Inherited = true)]
    public sealed class KuiOptionAttribute : Attribute
    {
        public string Label { get; }
        public KuiOptionAttribute(string label = null) { Label = label; }
    }

    /// <summary>
    /// Pairs with <see cref="KuiOptionAttribute"/> on numeric members.
    /// Selects the slider renderer over the stepper. Out-of-range values
    /// written by code are NOT clamped — clamp only on user interaction.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property,
                    AllowMultiple = false)]
    public sealed class KuiRangeAttribute : Attribute
    {
        public double Min { get; }
        public double Max { get; }
        public KuiRangeAttribute(double min, double max)
        {
            if (max < min)
                throw new ArgumentException($"KuiRange: max ({max}) must be ≥ min ({min}).");
            Min = min;
            Max = max;
        }
    }
}
