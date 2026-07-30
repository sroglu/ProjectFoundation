# Papercut Theme — Specification

> **Note — sample theme, incomplete.** Papercut was developed inside Playnest
> (Spec Kit features `001-papercut-theme` + `002-papercut-shell-migration`) but
> the consuming project pivoted to a UGUI/Canvas UI stack in 2026-05-27,
> orphaning the theme. The artifacts were folded back into this UISystem
> submodule as a second active theme alongside Candy so the work isn't lost —
> palette + treatment + per-screen USS + InactiveModifier + Showcase window
> are all present and load-clean, but no live UXML consumer ships with
> UISystem itself. Treat this as a reference / sample implementation. The
> follow-up backlog in `PAPERCUT-FOLLOWUPS.md` lists the polish work that was
> outstanding at the time of the pivot.

Status: **implemented v1.0 (2026-05-15)** via Spec Kit (`specs/001-papercut-theme/` in the source Playnest project).
Reference image: original `uiRef.png` in the source project's `Assets/UIArt/`.
Companion docs: `PAPERCUT-TODO.md` (scaffold-era plan), `PAPERCUT-FOLLOWUPS.md` (live polish backlog at pivot time).

## Final shape (post-implementation)

What this prose doc described was carried forward into Spec-Kit. The
canonical artifacts now live under `specs/001-papercut-theme/`:

- `spec.md` — 27 FRs + 8 SCs + 3 user stories + 4 Clarifications session bullets
- `plan.md` — Technical Context + Constitution Check + Project Structure
- `research.md` — 5 design decisions resolved (R1-R5) + risk register
- `data-model.md` — 5 entity definitions + 8 invariants
- `contracts/papercut-screen.md` — UI contract a Papercut UXML must satisfy
- `quickstart.md` — 5-step Candy→Papercut screen migration recipe
- `tasks.md` — 39 implementation tasks, all checked off

Key resolved decisions (from research.md):

| Question | Decision |
|---|---|
| Rendering technique | Pure USS via asymmetric `border-*-width` + child `.pc-highlight` VE. C# only for `.pc-inactive` class toggling (UI Toolkit has no `:inactive` pseudo). |
| Highlight curve format | 32×32 PNG produced by `Tools/Papercut/Generate Highlight Curve` menu (`PapercutAssetGenerator`). |
| Switching mechanism | Per-screen `<Style src=…>` reference in UXML. No global theme registry. |
| Shape language | Rounded rectangles (8 px) for container components; pills for switch/segmented/slider; circles for radio + knobs + handles. |
| Palette hex | Sampled from `Assets/UIArt/uiRef.png` per coordinates in research.md §R5; pinned by `PapercutHexSamplingTests`. |

## Why a separate theme

The current Candy theme (`Assets/Shell/UI/USS/candy-palette.uss` + framework
`candypop.uss`) renders surfaces with **4-sided bevels** and inherits M3's
shadow-based elevation. The reference asks for a different visual language:
**flat sticker / paper-cut**, not glossy plastic, not 3D, not gradient,
with elevation encoded as one bottom-right "edge band" plus one top-left
"highlight curve."

Rather than mutating Candy in place — which would regress every existing
shell screen — Papercut lives as a parallel theme. Both inherit M3
taxonomy (Elevated > Filled > Tonal > Outlined > Text); only the surface
treatment differs. Screens opt in by referencing Papercut stylesheets.

Name rationale: spec calls the visual metaphor "flat sticker / paper-cut
illustration. Toca Boca / Sago Mini." `sticker-board.uss` is already
taken by the Sticker Board feature, so we use the other half of the
metaphor: **Papercut**.

## Treatment system

Two decorations plus the fill color. Nothing else.

### 1. Edge band (bottom-right)
- Stripe along the bottom-right edge of the component.
- Color = fill darkened (component-local, not a global token).
- Height = 6-10% of component height — **thickness encodes emphasis**.
- Disappears on Pressed; muted on Disabled; quiet on Inactive.

### 2. Highlight curve (top-left)
- Small white curve graphic at "10 o'clock" position.
- Asset format TBD (PNG sprite vs USS-drawn arc — see TODO Phase 2).
- Signals "this surface is raised, touchable."
- Disappears on Pressed; muted on Disabled.

No 4-sided bevel. No outer shadow. No gradient fill. No gloss.

### UI Toolkit caveat
USS has no `::before` / `::after`. The edge band + highlight curve almost
certainly cannot be drawn in pure USS — they likely need real
`VisualElement` children inserted by a C# manipulator (working name
`PapercutSurface`). Resolve before writing component classes; see TODO
Phase 3.

## Emphasis hierarchy

M3 button hierarchy stays intact; mapped to band thickness:

| M3 Tier  | Edge band | Highlight | Notes                          |
| -------- | --------- | --------- | ------------------------------ |
| Elevated | ~10%      | yes       | thickest, "raised"             |
| Filled   | ~6%       | yes       | standard primary action        |
| Tonal    | ~3%       | yes       | quiet variant                  |
| Outlined | none      | none      | only the 1px contour           |
| Text     | none      | none      | no surface treatment at all    |

## Palette (semantic slots)

Concrete hex values TBD in TODO Phase 1 (sample uiRef.png).

| Slot           | Role                | Used by                                            |
| -------------- | ------------------- | -------------------------------------------------- |
| Primary        | hot pink (~#E91E63) | buttons, segmented selected, slider, chip-selected |
| Toggle         | yellow              | radio, switch (kasıtlı ayrım)                      |
| Surface        | coral               | cards                                              |
| Information    | blue                | checkbox                                           |
| Input          | teal                | text fields, list items                            |
| Inactive fill  | cream               | chip-unselected, switch-off                        |
| Inactive line  | gray                | thin outline on inactive surfaces                  |

**Chip taxonomy:** type only changes the icon, never the color.
Assist = ampul · Filter = funnel+caret · Input = X · Suggestion = sparkle.
A Filter chip and a Suggestion chip in the same state look identical
except for the icon child.

## State behavior

| State    | Fill         | Edge band | Highlight | Opacity |
| -------- | ------------ | --------- | --------- | ------- |
| Enabled  | full         | visible   | visible   | 1.0     |
| Pressed  | ~10% darker  | **gone**  | **gone**  | 1.0     |
| Disabled | desaturated  | muted     | muted     | ~0.5    |
| Inactive | cream        | quiet     | quiet     | 1.0     |

- **Pressed = flat.** Component loses both decorations and darkens.
- **Disabled keeps decorations but muted.** Not the same as Inactive.
- **Inactive ≠ Disabled.** Inactive is "off but tappable" (chip
  unselected, switch off); decorations are quiet, outline replaces
  the colored fill, opacity stays 1.0.

## Components in scope (from uiRef.png)

1. Button — Primary
2. Card — Surface
3. Chip (a) Selected — Primary
4. Chip (b) Unselected — Inactive
5. Checkbox — Information
6. Radio — Toggle
7. Switch — Toggle (track + knob; knob is also a surface)
8. Segmented Control — Primary on selected half, Inactive on the other
9. Slider — Primary track + handle; handle is a surface
10. Text Field — Input (fill + label band)
11. List Item — Input

## Sheet caveats — do NOT replicate

- Text Field shows "Plold text" — should be "Placeholder text" (sheet
  typo / render glitch).
- Switch knob highlight curve is missing in some sheet states. The
  treatment rule says the knob is a surface and **always gets the
  highlight** in non-Disabled states.
- Treat the sheet as **behavior reference**, not pixel reference.

## Coexistence with Candy

- Candy uses `--candy-*` tokens and `.candy-*` / `.shell-*` classes.
- Papercut uses `--pc-*` tokens and `.pc-*` classes.
- A single screen should not load both palettes. Switching mechanism
  (per-screen vs global theme switch) — see TODO Phase 5.

## File layout

```
Assets/Shell/UI/USS/Papercut/
  papercut-palette.uss      ← color slot definitions
  papercut-treatment.uss    ← edge-band + highlight-curve mixin classes
  papercut-components.uss   ← per-component classes + states

Assets/UIArt/Papercut/      ← reserved for highlight curve art,
                              edge texture, future component refs

docs/
  PAPERCUT-THEME.md         ← this file
  PAPERCUT-THEME-TODO.md    ← work plan
```

## Source-spec verbatim (kept for fidelity)

> Bu UI sistemi M3 baseline üzerine kurulu — taksonomi, hierarchy, behavior
> aynen M3'ün spec'ine uyar (Elevated > Filled > Tonal > Outlined > Text
> gibi). M3'ün varsayılan visual surface'i bizim projemizde şu şekilde
> override ediliyor:
>
> ELEVATION (M3'te shadow ile): Bizde shadow yok. Yerine her component'in
> alt-sağ kenarında "darker edge band" stripi var — fill renginin koyu
> tonunda, height %6-10 kalınlığında.
>
> HIGHLIGHT (M3'te yok): Her component'in üst-sol köşesinde küçük beyaz
> curve var (10 o'clock position). "Bu yüzey çıkıntılı, dokunulabilir"
> sinyali.
>
> VISUAL STYLE: Flat sticker / paper-cut illustration. NOT 3D rendered,
> NOT glossy plastic, NOT gradient. Reference: Toca Boca / Sago Mini
> flat treatment.
