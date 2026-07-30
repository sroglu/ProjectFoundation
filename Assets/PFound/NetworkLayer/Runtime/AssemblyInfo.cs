using System.Runtime.CompilerServices;

// White-box test access: the EditAndPlayModes test assembly drives server-internal
// telemetry hooks (e.g. ServerDiagnostics.RecordServed/Tick/RecordDeadlineMiss,
// ServerMetrics counters) directly. These stay `internal` because they are pumped by
// ServerPeer, not part of the public API surface. The standalone mono build compiles
// everything into a single assembly, so this friend declaration is a no-op there.
[assembly: InternalsVisibleTo("PFound.NetworkLayer.Tests.EditAndPlayModes")]
