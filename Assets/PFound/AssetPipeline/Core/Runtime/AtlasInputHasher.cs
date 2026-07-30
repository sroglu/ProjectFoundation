using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PFound.AssetPipeline.Core
{
    /// <summary>
    /// Computes a stable content hash for the INPUT SET of a sprite atlas: a digest over each member's identity
    /// (asset path + its source content hash), sorted so membership — not enumeration order — decides the result.
    /// Adding/removing a sprite, or changing a member's pixels (its content hash), changes the digest; reordering
    /// does not. This is the change-detection key that lets a build skip an unchanged atlas, consistent with the
    /// content-addressed bundle model. Engine-free (SHA-256), so it is unit-testable without Unity.
    /// </summary>
    public static class AtlasInputHasher
    {
        /// <summary>Digest of the member identity strings (e.g. "path|contentHash"). Order-independent; 32 hex chars.</summary>
        public static string Compute(IEnumerable<string> memberIdentities)
        {
            if (memberIdentities == null) throw new ArgumentNullException(nameof(memberIdentities));

            var members = new List<string>(memberIdentities);
            members.Sort(StringComparer.Ordinal);

            byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", members));
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(32);
                for (int i = 0; i < 16; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
