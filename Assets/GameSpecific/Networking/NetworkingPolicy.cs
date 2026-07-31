// Networking policy for this game's wire assembly (GameSpecific.Networking).
//
// FREE mode (default, line below commented): both `<Op>.CallAsync(args)` and the ServerOperation lifecycle
// are allowed — the developer picks per call.
//
// MANDATORY mode: uncomment the single line below to require every server call to go through a
// ServerOperation project-wide. Any raw `<Op>.CallAsync(...)` then raises PFNET0010 (compile error), so the
// shortcut can't be reached for by mistake. [Notify] `Send(...)` stays allowed (one-way, no lifecycle).
//
// NOTE: after a change to the analyzer DLL, restart Unity ONCE so it loads the analyzer; thereafter toggling
// this line takes effect on the next recompile with no further restart.

//[assembly: PFound.NetworkLayer.RequireServerOperation]
