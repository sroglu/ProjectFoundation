# Messaging
Instance-based synchronous event bus: an ordered, re-entrancy-safe `EventChannel` plus a stateful two-position `StateSwitch`, with engine-aware seams auto-installed under Unity.

**Key classes:** `EventChannel`, `EventChannel<TPayload>`, `StateSwitch`, `Subscription`, `SubscriptionLifetime`, `MessagingEnvironment`, `UnityMessagingHost`
**Assembly:** `PFound.Utilities.Messaging`
**Tier:** engine
**Depends on:** none

## Current API
- `EventChannel` — parameterless immediate bus. `Subscribe(callback, order, lifetime, guard)`
  returns a disposable `Subscription`; `SubscribeOnce`, `Unsubscribe`, `UnsubscribeAll(owner)`,
  `UnsubscribeCurrent`, `Clear`, `RemoveStaleListeners`, `Raise`, `RaiseProtected`,
  `ListenerCount`. Listeners run inline in ascending `order` (ties keep subscription order).
- `EventChannel<TPayload>` — same shape carrying a typed payload.
- `StateSwitch` — remembers on/off and fires paired enable/disable callbacks on edges only;
  a late `Subscribe` while already on immediately runs the enable side (state-sync fast-track).
  `TurnOn` / `TurnOff` / `Set(bool)` / `Toggle`, `IsOn`, `UnsubscribeAll`, `Clear`.
- `SubscriptionLifetime` — `Persistent` or `FireOnce`.

## Wiring seam
The dispatch core is engine-free. `MessagingEnvironment` exposes two swappable hooks —
`LivenessOf` (guard liveness) and `ReportFault` (protected-raise exception sink). Under Unity,
`UnityMessagingHost.Install()` runs automatically via
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` and replaces them with a fake-null-aware
`UnityEngine.Object` probe and `Debug.LogException`. No manual setup is required; consumers just
`new EventChannel()` / `new StateSwitch()` where they need one.

## Limitations
- Synchronous/inline dispatch only — no deferral or queueing (that is PFound.Signaling's job).
- `Raise` aborts on a throwing listener; use `RaiseProtected` to isolate faults and keep going.
- Intended for single-threaded main-loop use; not thread-safe.
