namespace Kunai
{
    public enum KuiLogLevel : byte
    {
        // Verbose intentionally has the highest numeric value so existing
        // switch statements with explicit Info/Warning/Error/Exception cases
        // and a default fallback degrade gracefully (Verbose lands in default
        // which renders as the Info path).
        Info      = 0,
        Warning   = 1,
        Error     = 2,
        Exception = 3,
        Verbose   = 4,
    }

    public struct KuiLogEntry
    {
        public float TimeSinceStartup;
        public KuiLogLevel Level;
        public string Message;
        public string StackTrace;
        // Auto-detected from a leading "[Foo]" prefix on the original
        // condition string (see KuiCategoryParser), or set explicitly via
        // KuiLogger.Info(msg, category). null = uncategorised.
        public string Category;
    }
}
