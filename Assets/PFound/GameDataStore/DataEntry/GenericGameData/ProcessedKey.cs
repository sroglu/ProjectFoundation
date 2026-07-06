using System;
using Sirenix.OdinInspector;

namespace PFound.GameDataStore.Entries
{
    [Serializable]
    [InlineProperty]
    public struct ProcessedKey : IEquatable<ProcessedKey>
    {
        [HideLabel]
        public string Key;

        #region Initialization

        public ProcessedKey(string key)
        {
            Key = key;
        }

        public static implicit operator ProcessedKey(string key)
        {
            return new ProcessedKey(key);
        }

        #endregion

        #region Validity

        public bool IsNull => string.IsNullOrEmpty(Key);
        public bool IsNotNull => !string.IsNullOrEmpty(Key);

        #endregion

        #region Equality

        public bool Equals(ProcessedKey other)
        {
            return string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public bool Equals(string other)
        {
            return string.Equals(Key, other, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ProcessedKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Key != null ? Key.GetHashCode() : 0);
        }

        #endregion

        #region ToString

        public override string ToString()
        {
            return Key;
        }

        #endregion

        #region Tools

        public static readonly ProcessedKey[] EmptyProcessedKeyArray = Array.Empty<ProcessedKey>();

        #endregion
    }
}