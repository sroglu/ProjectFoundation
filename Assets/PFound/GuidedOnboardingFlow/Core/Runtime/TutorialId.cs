using System;

namespace PFound.GuidedOnboardingFlow.Core
{
    /// <summary>
    /// A compact value-type handle for a tutorial definition. Backed by a stable integer so lookups,
    /// persistence keys and equality stay allocation-free — raw display strings are kept only for
    /// authoring/diagnostics and never participate in identity.
    /// </summary>
    [Serializable]
    public struct TutorialId : IEquatable<TutorialId>
    {
        // Serialized field name kept simple so Unity's serializer and the editor drawer agree on it.
        public int handle;

        public TutorialId(int handle)
        {
            this.handle = handle;
        }

        /// <summary>Zero is reserved to mean "no tutorial" so a default(TutorialId) is detectable.</summary>
        public bool IsNone => handle == 0;

        public static TutorialId None => new TutorialId(0);

        public bool Equals(TutorialId other) => handle == other.handle;

        public override bool Equals(object obj) => obj is TutorialId other && Equals(other);

        public override int GetHashCode() => handle;

        public override string ToString() => "Tutorial#" + handle;

        public static bool operator ==(TutorialId a, TutorialId b) => a.handle == b.handle;

        public static bool operator !=(TutorialId a, TutorialId b) => a.handle != b.handle;
    }
}
