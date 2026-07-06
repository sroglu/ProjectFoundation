# Papercut Theme — Work Plan (CLOSED 2026-05-15)

This pre-Spec-Kit work plan is superseded by the live task list at
`specs/001-papercut-theme/tasks.md` (33/39 complete; 6 polish tasks
closed alongside this doc). Kept for historical reference only —
the phases below were absorbed and reorganised by the Spec Kit
workflow.

Companion to `docs/PAPERCUT-THEME.md`. Close phases in order;
each phase ends with a verifiable artifact (file, asset, or screen).

## Phase 0 — Scaffolding (DONE 2026-05-11)

- [x] Folder `Assets/Shell/UI/USS/Papercut/` created
- [x] `papercut-palette.uss` skeleton (`:root` placeholder, slot
      contract documented)
- [x] `papercut-treatment.uss` skeleton (class contracts in comments)
- [x] `papercut-components.uss` skeleton (component list in comments)
- [x] Folder `Assets/UIArt/Papercut/` reserved for art
- [x] `docs/PAPERCUT-THEME.md` (spec)
- [x] `docs/PAPERCUT-THEME-TODO.md` (this file)

## Phase 1 — Palette concretization

- [ ] Sample `Assets/UIArt/uiRef.png` at each slot:
      Primary fill, Toggle fill, Surface fill, Info fill, Input fill,
      Inactive cream, Inactive outline gray
- [ ] Record hex values inline in this file (audit trail)
- [ ] Fill `papercut-palette.uss` `:root`:
      `--pc-primary`, `--pc-toggle`, `--pc-surface`, `--pc-info`,
      `--pc-input`, `--pc-inactive-fill`, `--pc-inactive-line`
- [ ] Add darkened-fill tokens for edge bands — one per slot:
      `--pc-primary-edge`, `--pc-toggle-edge`, `--pc-surface-edge`,
      `--pc-info-edge`, `--pc-input-edge`. Concrete darken curve TBD
      (eyeballed vs HSL -L%); record decision here.
- [ ] Decide text colors. Likely shared with Candy → alias
      `--pc-text-heading`, `--pc-text-body`, `--pc-text-on-strong` to
      the existing `--candy-text-*` tokens to avoid drift.

## Phase 2 — Highlight curve asset

- [ ] Decide format: **PNG sprite** (alpha-only, 32×32, white curve at
      10 o'clock) vs **USS-drawn arc** (border-radius hack on a tiny
      child VisualElement).
- [ ] If PNG: produce `Assets/UIArt/Papercut/highlight-curve.png` +
      `highlight-curve.png.meta` with sprite import settings (no
      mipmaps, point filter, alpha-only).
- [ ] Verify it composites correctly on saturated fills (pink, blue,
      teal) **and** cream fills (inactive); white-on-cream contrast may
      need a soft drop. Capture comparison via `Unity_Camera_Capture`.
- [ ] Decide: same curve for all sizes (scaled), or three sizes
      (small chip / mid button / large card)?

## Phase 3 — Treatment system

- [ ] Resolve UI Toolkit caveat first: edge band + highlight need
      real `VisualElement` children since USS lacks pseudo-elements.
      Likely shape: a `PapercutSurface` manipulator that adds
      `pc-edge-band` + `pc-highlight` children on Attach. Confirm vs
      override-`Init` pattern from existing M3 controls.
- [ ] Implement `.pc-surface` base class
- [ ] Implement `.pc-edge-band--elevated` (~10% height)
- [ ] Implement `.pc-edge-band--filled`   (~6%  height)
- [ ] Implement `.pc-edge-band--tonal`    (~3%  height)
- [ ] Implement `.pc-highlight` (top-left curve)
- [ ] `:active` rule: hide band + highlight, darken fill ~10%
- [ ] `:disabled` rule: opacity 0.5, band/highlight muted
- [ ] `.pc-surface--inactive` modifier: cream fill + thin gray
      outline; band/highlight quiet but not removed
- [ ] Smoke-test on a single Button manually placed in the
      UISystem Showcase scene before fanning out to other components.

## Phase 4 — Component classes

Per component: write `papercut-components.uss` class + verify against
uiRef.png in all three states (Enabled / Pressed / Disabled).

- [ ] `.pc-button` (Elevated / Filled / Tonal / Outlined / Text)
- [ ] `.pc-card`
- [ ] `.pc-chip` + `.pc-chip--selected` / `.pc-chip--unselected`
      (icon child slot, type-agnostic)
- [ ] `.pc-checkbox` (Info)
- [ ] `.pc-radio` (Toggle)
- [ ] `.pc-switch` — track + knob; **knob also gets highlight curve**
      (sheet bug correction)
- [ ] `.pc-segmented` (selected = filled, other = inactive)
- [ ] `.pc-slider` — track + handle; handle is a surface
- [ ] `.pc-textfield` — Input tier; placeholder string is
      "Placeholder text" not "Plold text" (sheet bug correction)
- [ ] `.pc-listitem` (Input tier)

## Phase 5 — Showcase + opt-in mechanism

- [ ] Build `Assets/Shell/UI/Showcase/PapercutShowcase.uxml` mirroring
      uiRef.png layout (10 components × 3 states grid)
- [ ] Capture via `Unity_Camera_Capture` and diff against uiRef.png;
      list intentional deviations explicitly
- [ ] Decide opt-in mechanism:
      - **Per-screen** — UXML adds `<Style src=".../papercut-*.uss"/>`
      - **Global** — extend UISystem ThemeManager (if it exists), add
        a "Papercut" theme entry alongside Candypop
- [ ] Document the chosen path; update `CLAUDE.md` Decision Log

## Phase 6 — Migration (optional, post-validation)

- [ ] Pick one screen (Home is the highest-traffic candidate) and
      port it end-to-end as a soak test
- [ ] Compare side-by-side against the Candy version (screenshot
      diff)
- [ ] Decide: deprecate Candy, ship both as user-selectable, or keep
      Papercut as a research branch only

## Open questions to resolve

- [ ] Edge band + highlight via C# `VisualElement` children, USS
      hack, or custom shader? (Phase 3 blocker)
- [ ] Coral surface — exact hex from uiRef? (Phase 1)
- [ ] Yellow track for switch — same yellow as radio fill? (sheet
      suggests yes — confirm)
- [ ] Inactive outline gray — single token or per-component override?
- [ ] Highlight curve — PNG (asset cost) vs USS arc (rendering cost)?
- [ ] M3 outline / text variants — do we need them at all in the
      shell? Scope reduction option.
