using System.Collections.Generic;

namespace Kunai
{
    /// <summary>
    /// Outcome of a single <see cref="KuiReflectionScanner.Scan{TAttr}"/> call.
    /// Per-assembly load failures land in <see cref="Errors"/> instead of
    /// aborting the scan — third-party packages with broken dependencies are
    /// common and shouldn't take Kunai down with them.
    /// </summary>
    internal struct KuiReflectionResult
    {
        public int TypesScanned;
        public int MembersMatched;
        public List<KuiReflectionError> Errors;
    }

    internal struct KuiReflectionError
    {
        public string AssemblyName;
        public string Message;
    }
}
