# UISystem — Known Issues / TODO

Tracked items that block or degrade UISystem usage in downstream projects. Update as they're resolved.

---

## USS Parser Errors (Unity 6.3 LTS)

**Surfaced in:** Playnest project bootstrap, Unity 6000.3.12f1, 2026-04-16. Console reports compile-time USS parser errors during initial asset import.

### ~~Issue 1 — `navigation-drawer.uss:110` Recursive pseudo classes~~ ✅ Resolved 2026-05-28 (Phase 5)
Flattened the `:not(.m3-nav-drawer-item--active)` chain into a default-rule + `--active` override pair (no `:not()`). Verified 0 errors in `Unity_GetConsoleLogs`.

```
Assets/PFound/UISystem/Styles/Components/navigation-drawer.uss (line 110): error: Internal import error: Recursive pseudo classes are not supported
```

**What happened:** Unity 6's USS parser (`UnityEngine.UIElements.StyleSheets.CSSSpec.GetSelectorSpecificity`) rejects nested pseudo selectors (e.g., `:is(... :is(...))`, `:not(:hover:focus)` style chains, or selectors that reference themselves through a pseudo). The selector at line 110 contains such a pattern.

**Why it must be fixed:**
- The drawer's `[Stylesheet]` import fails → drawer component renders without its themed states (hover/focus/selected)
- Any downstream project using `M3NavigationDrawer` sees broken visuals in Editor + runtime
- Error pollutes the console on every full asset reimport, masking other real issues

**To investigate:**
1. Open `Styles/Components/navigation-drawer.uss` line 110, identify the nested pseudo selector
2. Flatten the selector (split into sibling rules) — Unity USS does NOT support `:is()`, `:where()`, or recursion
3. Re-test in Unity 6.3 LTS
4. Add a regression test in `Tests/UISystem.Tests` that imports the USS and asserts no parser errors

### ~~Issue 2 — `textfield.uss:45` Unknown property `pointer-events`~~ ✅ Resolved 2026-05-28 (Phase 5)
Removed the no-op `pointer-events: none` declaration. The floating label already sets `pickingMode = PickingMode.Ignore` in `M3TextField.cs:187`. USS now warning-free.

```
Assets/PFound/UISystem/Styles/Components/textfield.uss (line 45): warning: Unknown property 'pointer-events' (did you mean 'border-radius'?)
    pointer-events: none
```

**What happened:** `pointer-events` is a CSS-standard property but Unity USS does not implement it (Unity's pick-detection works through `picking-mode` instead).

**Why it must be fixed:**
- The textfield can't disable pointer interception via the documented CSS approach → input bleeds through to underlying elements when intended to be disabled
- Warning level today, but downstream developers may write USS the same way and get silent breakage

**To investigate / fix:**
1. Replace `pointer-events: none` with `picking-mode: ignore` in the USS rule at line 45
2. Verify Unity USS reference (Edit > Preferences > UI Toolkit) for the canonical property name
3. Document in `GUIDELINES.md` → "USS authoring rules" that `picking-mode` is Unity's equivalent and `pointer-events` must NOT be used

---

## Workflow when resolving

1. Fix inside this submodule, push commit
2. In downstream project (e.g., Playnest), bump submodule ref → commit → push
3. Run `Unity_GetConsoleLogs` in Unity to confirm 0 errors / 0 warnings
4. Strike through the resolved item above (don't delete — keeps the audit trail)
