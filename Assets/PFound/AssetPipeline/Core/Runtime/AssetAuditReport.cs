using System.Collections.Generic;
using System.Text;

namespace PFound.AssetPipeline.Core
{
    /// <summary>
    /// The result of an audit pass: the policy it ran against, how many assets it scanned, and every
    /// <see cref="PolicyViolation"/> found — and nothing mutated. Engine-free, so the audit's shape is testable
    /// without Unity; the editor exporter serializes it to JSON. An empty <see cref="Violations"/> means clean.
    /// </summary>
    public sealed class AssetAuditReport
    {
        public string PolicyDescription;
        public int AssetsScanned;
        public IReadOnlyList<PolicyViolation> Violations;

        public AssetAuditReport(string policyDescription, int assetsScanned, IReadOnlyList<PolicyViolation> violations)
        {
            PolicyDescription = policyDescription;
            AssetsScanned = assetsScanned;
            Violations = violations;
        }

        public int ViolationCount => Violations.Count;
        public bool IsClean => Violations.Count == 0;

        /// <summary>How many violations carry <paramref name="code"/> (for per-rule roll-ups).</summary>
        public int CountOf(ViolationCode code)
        {
            int n = 0;
            for (int i = 0; i < Violations.Count; i++) if (Violations[i].Code == code) n++;
            return n;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Audited ").Append(AssetsScanned).Append(" asset(s) against [").Append(PolicyDescription).Append("]: ");
            if (IsClean) { sb.Append("clean."); return sb.ToString(); }
            sb.Append(ViolationCount).Append(" violation(s)");
            foreach (var v in Violations) sb.Append("\n  • ").Append(v);
            return sb.ToString();
        }
    }
}
