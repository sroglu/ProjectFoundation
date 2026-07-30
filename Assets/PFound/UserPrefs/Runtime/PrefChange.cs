namespace PFound.UserPrefs
{
    public enum PrefChangeKind
    {
        Set = 0,
        Cleared = 1,
        MigratedIn = 2,
    }

    public readonly struct PrefChange<T>
    {
        public readonly string Key;
        public readonly T OldValue;
        public readonly T NewValue;
        public readonly PrefChangeKind Kind;

        public PrefChange(string key, T oldValue, T newValue, PrefChangeKind kind)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
            Kind = kind;
        }
    }
}
