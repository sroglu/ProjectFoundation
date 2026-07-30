# Papercut UI — Polish & Review Backlog

Live polish list for future iterations. Captured 2026-05-16 after the
shell + Sumo Bumo migration shipped. This file is distinct from
`docs/PAPERCUT-THEME-TODO.md` (closed historical scaffold) and from
`specs/0**-papercut-*` (closed Spec-Kit features). Treat this as the
live TODO; promote a chunk to a new Spec-Kit feature when scope grows.

The top section is the **structured visual review** the next pass
should drive against. Each component has a checklist of dimensions to
evaluate at native resolution against `Assets/UIArt/uiRef.png`.

---

## Priority 0 — Comprehensive component-by-component review

### Review dimensions (apply per component)

For every component below, the reviewer checks:

1. **Shape** — corner radius matches the desired language (rounded
   rect vs pill vs circle)
2. **Size** — width / height / padding feel proportional
3. **Fill colour** — palette-correct + readable text on top
4. **Outline** — uniform thin contour visible, colour right
5. **Edge band** — bottom-right thickening present, correct
   thickness tier, contrasts the fill
6. **Highlight curve** — top-left position, size proportional, alpha
   strong enough to read
7. **Pressed state** — fill darken visible, band + highlight hidden,
   outline persists
8. **Disabled state** — opacity ~0.5, decorations muted, text legible
9. **Inactive state** (where applicable) — cream + gray, distinct
   from disabled
10. **Typography** — font size, weight, alignment, vertical centring
11. **Per-context consistency** — same component looks the same on
    Home / Settings / Modal etc.

---

### 1. Button (`.pc-button`)

- [ ] Shape: 8 px rect — final or want different? (smaller? larger?)
- [ ] Variants visual distinction:
  - [ ] Elevated (12 px edge band) vs Filled (8 px) vs Tonal (4 px) —
        readable hierarchy?
  - [ ] Outlined (transparent fill, dark text) — distinct from Filled?
  - [ ] Text (no surface) — does it still feel clickable?
- [ ] Padding `10px 32px` consistent across all button instances?
- [ ] Font size 22 px bold — too big on Settings? too small on Home?
- [ ] Per-context: Home settings-btn (icon-only square) vs Pause Resume
      (Filled large) vs Settings Reset (custom red tonal) vs ParentGate
      answer (Tonal compact) — do they all read as the same component
      family?
- [ ] Pressed → fill darken `--pc-primary-pressed` strong enough?
- [ ] Disabled opacity 0.5 — too faint? text still readable?
- [ ] 88×88 pt min — applied via `.pc-tap-target` opt-in; should it
      be default on `.pc-button`?

### 2. Card (`.pc-card`)

- [ ] Shape: 8 px rect — feel "card-like" enough?
- [ ] Fill: Surface coral `(211, 57, 59)` — discussed: more red than
      coral; keep or retune?
- [ ] Padding `14px 32px` enough for content?
- [ ] Edge band 8 px standard — consistent with Button?
- [ ] Used as Home game tile (with per-game palette override) — does
      override break the Surface association?
- [ ] Used as modal card (Pause, ParentGate, AgeBand) — large card
      look right?
- [ ] Card title typography (20 px bold) — readable on saturated bg?

### 3. Chip (`.pc-chip`)

- [ ] Shape: 8 px rect (changed from pill in `b3f86f5`) — final?
- [ ] Size: padding `6px 18px`, font 16 px — feels chip-y?
- [ ] Selected vs Inactive distinction clear at a glance?
- [ ] Icon child (`.pc-chip__icon`) — 16×16 px right size?
- [ ] Type taxonomy: Assist / Filter / Input / Suggestion — only
      icon differs per spec. Have we tested all 4 types yet?
- [ ] Per-context: Home profile chip (Arda + icon + name) vs
      BadgeGallery filter tabs (text-only) vs Showcase reference —
      consistent feel?

### 4. Checkbox (`.pc-checkbox`)

- [ ] Box size 36 × 36 px — too small? right balance vs label?
- [ ] Corner radius 8 px (square-ish) — feel "checkbox-y"?
- [ ] Information blue `(96, 111, 162)` — distinct enough from
      Primary pink?
- [ ] Checkmark glyph (`✓`) — colour, size, vertical centring
- [ ] Label spacing `margin-left: 12px` — comfortable?
- [ ] Highlight curve override (14 × 14 px) — visible on the small
      box?

### 5. Radio (`.pc-radio`)

- [ ] Circle size 36 × 36 px — same call as Checkbox
- [ ] Toggle yellow `(244, 174, 8)` — distinct from Information blue
      AND from Primary pink?
- [ ] Inner dot 14 × 14 px white — proportional?
- [ ] Label spacing — symmetrical with Checkbox?

### 6. Switch (`.pc-switch`)

- [ ] Shape: 10 px rect (changed from pill in `822e935`) — final?
- [ ] Track size 90 × 40 px — feels right for a toggle?
- [ ] Knob 34 × 34 px circle with own outline + edge band — busy?
- [ ] On (knob right) vs Off (knob left via `.pc-inactive`) — do we
      see the slide animation in real use or is it a snap?
- [ ] Off-state cream fill (Inactive Fill) + gray outline — reads as
      "off"?

### 7. Segmented Control (`.pc-segmented`)

- [ ] Track 10 px rect, segments 6 px rect — final shapes?
- [ ] Selected segment: Primary fill + 5 px edge band
- [ ] Unselected segment: cream text-on-cream — readable?
- [ ] Per-context: Settings Lang (EN/TR), Settings Analytics
      (Off/On), Showcase (Seg 1/Seg 2) — consistent?
- [ ] Presenter wiring: who toggles `.pc-selected` between segments
      on tap? Currently only initial state set in UXML — need
      `SettingsPresenter` to update on click.

### 8. Slider (`.pc-slider`)

- [ ] Track 6 px rect — too thin to grab? compare to uiRef
- [ ] Track height 16 px in CSS but with 6 px radius makes it look
      bar-like; verify
- [ ] Handle 36 × 36 px circle with edge band — looks right?
- [ ] Settings volume sliders (3 in a row) — vertical rhythm OK?
- [ ] Settings Screen Time slider (range 0-120) — handle position
      at 0 visible? at 120 visible?
- [ ] Active drag visual feedback — UI Toolkit Slider has its own
      pseudo states; verify Papercut respects them.

### 9. Text Field (`.pc-textfield`)

- [ ] Shape: 10 px rect (kept rectangle since pre-shape-refactor)
- [ ] Fill: off-white cream `rgb(252, 244, 232)` — feels like an
      input?
- [ ] Outline: teal `--pc-input` — distinct from Information blue?
- [ ] Label "LABEL" small caps 11 px above field — readable?
- [ ] Placeholder text colour vs input text colour — clear hierarchy?
- [ ] Focus state — UI Toolkit `:focus` pseudo; do we override?

### 10. List Item (`.pc-listitem`)

- [ ] Shape: 8 px rect (changed from pill in `822e935`) — final?
- [ ] Teal `--pc-input` fill — same colour as TextField outline,
      intentional?
- [ ] Padding `10px 24px` enough?
- [ ] Chevron `›` at right — size/colour right?
- [ ] When used as Home collection button vs hypothetical list item
      in another screen — consistent?

### 11. Game tile (`.pc-home-tile`, screen-specific)

- [ ] Per-game palette: Tossy pink / Camping green / Tiny Tales
      purple / Sumo Bumo orange — feel like a coherent set or
      random?
- [ ] Tile size 46% width × 160 px min-height — too small? too big?
- [ ] Icon 56 px + label 20 px — readable from a distance?
- [ ] Locked state opacity 0.55 + lock badge top-right — readable
      that it's locked?

### 12. Modals (Pause, ParentGate, AgeBandPickerModal)

- [ ] Scrim alpha 0.5 — too dark? too light?
- [ ] Card sizing consistent across the three modals?
- [ ] Card alignment in scrim (`align-items: center; justify-content:
      center`) — looks centred on tablet portrait?
- [ ] Stacked action buttons vs side-by-side action buttons — when
      to use which?

### 13. Top app bar (`.pc-top-bar`)

- [ ] 72 px tall — too tall? right balance with title typography?
- [ ] Back button 48 × 48 — distinct from main Button visually?
      should it be smaller?
- [ ] Title 26 px bold — readable on cream sheet bg?
- [ ] Per-context: BadgeGallery / PhotoAlbum / StickerBoard /
      Settings — all use same `.pc-top-bar`; final or per-screen
      variants needed?

### 14. Empty state (`.pc-empty-state`)

- [ ] Icon 64 px + title + subtitle — used on PhotoAlbum,
      BadgeGallery, StickerBoard — consistent emotional tone?
- [ ] Colour palette of icon (`--pc-primary-edge`) — appropriate or
      should it be neutral?
- [ ] Spacing rhythm — feels final?

---

## Priority A — Visual decisions that may need stakeholder input

- [ ] **Surface palette colour** — currently `rgb(211, 57, 59)` (sampled
      from `uiRef.png`'s Card row). Spec called this "coral" but the
      sampled value reads as dark red/pink. Either accept the sampled
      value as canonical or re-sample to a warmer coral and update
      `--pc-surface` + `--pc-surface-edge` + `--pc-surface-pressed`.
- [ ] **Cold-launch walk-through (SC-001 visual check)** — manually
      navigate ProfileSelect → AgeBandPickerModal → Home → Settings →
      ParentGate → every mini-game first screen at native resolution.
- [ ] **Parent panel comparison test (SC-001 measurable)** — five+
      raters, blind side-by-side of one pre-migration screenshot vs
      post-migration. ≥ 4/5 should pick Papercut as "more deliberately
      designed for a young child."
- [ ] **AgeBandPickerModal inline overrides** — 3-4 and 4-5 tiles use
      inline `style="background-color: rgb(60,175,95); …"` for green
      + blue. Replace with proper palette tokens.
- [ ] **Settings → Reset Progress destructive button** — uses a manual
      inline red `rgb(150, 40, 40)` colour. Either define a `--pc-danger`
      palette slot or accept the inline.

---

## Priority B — Cross-component consistency

- [ ] **Typography hierarchy** — h1 (title) / h2 (section) / body /
      label / caption — is the spread of sizes consistent across the
      app? Right now each screen sets its own font-size; consider
      centralising into `papercut-typography.uss` if drift appears.
- [ ] **Colour contrast WCAG** — white text on `--pc-primary` is OK,
      on `--pc-toggle` (yellow) might be borderline. Spot-check at
      WCAG AA against AAA thresholds.
- [ ] **Spacing rhythm** — margins between sections, card-to-card,
      button-to-button. Currently per-screen; collect into spacing
      tokens (`--pc-space-sm/md/lg`)?
- [ ] **Iconography** — `m3-icon` font from UISystem typography. Is
      the icon weight + style consistent with Papercut's flat
      sticker look? May need a Papercut-specific icon font in the
      future.
- [ ] **Modal/overlay scrim alpha** — `0.5` everywhere. Should
      destructive modals (parent gate) be darker than informational?

---

## Priority C — Architecture / tech debt

- [ ] **BEM legacy-shim rules in `papercut-shell-common.uss`** — four
      item templates kept their original BEM class names so the C#
      presenters that toggle modifier classes via `EnableInClassList`
      keep working. Naming convention is mixed (`pc-*` for new + BEM
      for legacy). Options:
      - (a) Rename presenter constants, drop BEM rules, add `.pc-*`
        equivalents — cleaner long-term;
      - (b) Document the mixed convention and live with it.
- [ ] **Per-screen layout in `papercut-shell-common.uss`** — that file
      hosts both shared layout primitives (`.pc-top-bar`, `.pc-grid`,
      `.pc-empty-state`) and screen-specific layout (`.profile-select__*`,
      `.badge-gallery__*`). Consider splitting screen-specific rules
      back into per-screen Papercut files.
- [ ] **003-papercut-sumobumo as a "real" Spec-Kit feature** — the
      Sumo Bumo alignment landed as a direct commit on main without
      ceremony (commit `9ff7bf3`). If we want consistency, retro-fit
      a `specs/003-papercut-sumobumo/` skeleton.

---

## Priority D — Tests & verification

- [ ] **Re-run 24-test EditMode suite** after the 002 + 003-sumo work
      lands. Last clean run was at 001 merge point (`2ca0c86`); Unity
      Editor was in a Play-mode/test-runner race during the post-002
      attempt. Memory note "Unity Play Mode + compile lock" is the
      recipe.
- [ ] **Extend `PapercutCoexistenceTests` to every migrated screen**
      — current test only covers `Home.uxml`. Add cases for the 7
      other migrated screens.
- [ ] **T030 / T033 (Candy regression diff) pivot** — original
      blocker was no EditMode panel-to-Texture2D API. But Candy USS
      files are now deleted, so the diff baseline can't exist. Mark
      these as resolved by deletion + update spec.
- [ ] **PapercutHexSamplingTests after palette retune** — if Surface
      coral hex changes, re-pin the test.
- [ ] **Per-component visual snapshot tests** — when implementing
      Priority 0 review changes, capture before/after screenshots
      into `specs/003-*/baselines/` so the next iteration has a
      visual diff anchor.

---

## Priority E — Things still missing UXML / future work

- [ ] **Tossy Toss** has no UXML player UI — gameplay UI is currently
      GameObject-based. When porting to UI Toolkit, ensure Papercut
      from day 1.
- [ ] **Camping Adventure** ditto — Ranger Pip dialog + Backpack
      picker will need Papercut treatment when authored.
- [ ] **Tiny Tales (v1.1)** — when the storybook UI is built,
      Papercut should already be the active theme.

---

## Notes / known caveats

- `Assets/PFound/UISystem/Resources/UISystem/candypop.uss`
  still exists in the submodule (UISystem still ships it for other
  consumers). Playnest no longer references it — verified by grep.
  Don't delete from the submodule.
- `Assets/Game/`, `Assets/Game.meta`, `Assets/MiniGames/SumoBumo/Assets/`
  are pre-existing untracked WIP from the user, not from Papercut
  work. Leave alone.
- Pushed to `origin/main` at `e692d02` on 2026-05-16. This file
  becomes the entry point for the next polish iteration.
