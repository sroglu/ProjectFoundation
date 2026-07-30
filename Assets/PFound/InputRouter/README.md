# PFound.InputRouter

A backend-agnostic input **intent** router. Caller-supplied `IIntentSource<T>` adapters turn raw
device input into typed intent structs; the router derives a four-phase lifecycle (`Idle` / `Started`
/ `Held` / `Ended`) per intent and dispatches each phase to its subscribers in stable registration
order, once per `Tick()`. The core is engine-free; a backend is touched only inside your source.

## Quick reference

```csharp
var driver = gameObject.AddComponent<IntentRouterDriver>();   // MonoBehaviour host, pumps Tick() in Update
IntentRouter router = driver.Router;

router.Register<MoveIntent>(new InputSystemMoveSource(moveAction), IntentGroup.Gameplay);
IDisposable sub = router.Subscribe<MoveIntent>(
    IntentPhase.Held, ctx => _body.Move(ctx.Payload.Horizontal, ctx.Payload.Vertical));
// sub.Dispose() to unbind.

// Or host it yourself: new IntentRouter(PointerOverUiGate.FromEventSystem()); call Tick() each update.

driver.Disable();   // freeze BOTH intent dispatch and UI clicks (toggles the active EventSystem
driver.Enable();    // input module); the state is re-applied on every active-scene change.
```

The `Samples/` assembly ships a dual pair per intent — `Legacy*Source` (`ENABLE_LEGACY_INPUT_MANAGER`)
and `InputSystem*Source` (`ENABLE_INPUT_SYSTEM`) — so the router stays backend-agnostic.

The UI gate (`PointerOverUiGate.FromEventSystem()`) is touch-aware: it checks the mouse pointer OR
the primary touch (id 0), so UI blocks gameplay intents on both desktop and touch devices.

## Dependencies

Runtime → `PFound.InputRouter.Core` (engine-free). Samples → `Unity.InputSystem`.

## Docs

Deep reference: [MODULE.md](MODULE.md) — API, phases/groups, hosting, dual-backend Samples, limitations.
