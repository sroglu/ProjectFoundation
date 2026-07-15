# StringTools
Engine-free plain-string helpers and a brace-based tag-processing scanner, with an engine-facing naming assembly split off so the core stays mono/csc testable.

**Key classes:** `StringTools` (partial), `ITagSink`, `ITagProcessor`, `TagProcessResult`, `NamingTools`
**Assembly:** `PFound.Utilities.StringTools` (Runtime), `PFound.Utilities.StringTools.Engine` (Engine)
**Tier:** core (Runtime) / engine (Engine)
**Depends on:** `ZString`
