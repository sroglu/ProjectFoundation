# UISystem Component Development Guidelines

Lessons learned during the M3 Button implementation (WP-4 + WP-5).
Apply these to every new component: TextField, Card, Chip, Dialog, etc.

---

## 1. USS `border-radius: 9999px` Creates Ovals, Not Pills

**Problem:** Unity UI Toolkit resolves `border-radius: 9999px` as an *ellipse* — it clamps
x-radius to `width/2` and y-radius to `height/2` independently. For a 40×86px button that
means x=43px and y=20px. When x+x=width and y+y=height, every quarter-arc meets at the
center → full ellipse, no flat sections.

**Rule:** Always use explicit `height/2` pixel values for pill-shaped elements.

```css
/* WRONG — renders as ellipse */
border-radius: 9999px;

/* CORRECT — circular corner arcs → true pill */
border-radius: 20px; /* = 40px height / 2 */
```

For dynamic sizes, set `style.border*Radius = height / 2f` from C# in addition to the USS
class, because Unity applies USS first and inline style overrides.

```csharp
// In ApplySize() or wherever height changes:
float r = isSquare ? squareRadius : height / 2f;
_root.style.borderTopLeftRadius    = r;
_root.style.borderTopRightRadius   = r;
_root.style.borderBottomLeftRadius = r;
_root.style.borderBottomRightRadius = r;
```

Also note: `overflow: hidden` combined with `border-radius` clips *everything* drawn inside
the element — including Painter2D output from `generateVisualContent`. Do not remove the clip;
it is required for RippleElement clipping.

---

## 2. ~~SDFRectElement: Use `layout` Bounds, Not `contentRect`~~ *(obsolete — spec 009 Phase 4)*

The original Painter2D `OnGenerateVisualContent` path was replaced by the GPU SDF shader in
`M3Surface` / `SdfShape`. The shader operates on UV space derived from the element's
padding-box layout automatically; there is no consumer-side draw rect choice anymore. Left
as a historical note — see commit history if you maintain a fork still on the legacy
Painter2D primitive.

---

## 3. Optical Centering vs. Geometric Centering

**Problem:** Font ascenders (cap-height + above) are taller than descenders. Unity positions
text using the full font metrics bounding box. The geometric center of this box is above the
visual center of typical button label characters (uppercase letters, no descenders). Result:
text appears to float upward inside the button — most visible in small sizes (XS, SM).

**Rule:** Do not rely on CSS `align-items: center` alone for text centering. Reset default
Label padding and apply a small optical compensation.

```css
/* button.uss — clear Unity's default Label padding */
.m3-button__label {
    padding: 0;
    -unity-text-align: middle-center;
}
```

```csharp
// M3Button.cs — optical offset for Roboto Medium
// Shifts cap-height to the visual center of the pill.
_label.style.paddingTop = 1f;
```

The compensation value is font-specific (~1px for 14px Roboto Medium). If the font changes,
re-tune this value visually.

---

## 4. Unity Label Has Default Padding

**Problem:** Unity's built-in stylesheet adds `padding: 1px 2px` to all Label elements. If
you don't explicitly reset this, labels inside tightly constrained containers (small buttons,
chips) will be visually off-center even with correct container layout.

**Rule:** Always add `padding: 0` to any label USS class used inside a custom component.

```css
.m3-my-component__label {
    margin: 0;
    padding: 0;
    -unity-text-align: middle-center;
}
```

---

## 5. Singleton Pattern: Domain Reload Resilience

**Problem:** Unity Editor domain reloads (triggered by script changes, `AssetDatabase.Refresh`,
etc.) reset all C# statics to their default values. MonoBehaviours in `DontDestroyOnLoad`
persist across the reload but their `Awake()` / `OnEnable()` are NOT re-called. Static
singleton references become null while the actual component still exists.

**Rule:** Use a lazy getter that searches the scene if the cached reference is null.

```csharp
private static MyManager _instance;

public static MyManager Instance
{
    get
    {
        if (_instance == null)
            _instance = FindObjectOfType<MyManager>();
        return _instance;
    }
}
```

This makes `Instance` safe to call at any time without depending on lifecycle order.

---

## 6. Demo Controllers: Register Panels Defensively

**Problem:** `ThemeManager._managedPanels` is a runtime list that only receives panels through
`RegisterPanel`. Easy to forget when creating new demo scenes. Without registration, `ToggleLightDark()`
succeeds silently but nothing changes visually.

**Rule:** Every demo `MonoBehaviour` that uses theming must call `RegisterPanel` in `Start()`.

```csharp
private void Start()
{
    var doc = GetComponent<UIDocument>();
    ThemeManager.RegisterPanel(doc); // defensive — idempotent

    // ... rest of setup
}
```

`RegisterPanel` is idempotent (checks for duplicates), so calling it on an already-registered
panel is safe.

---

## 7. Absolute-Positioned Elements Intercept Click Events

**Problem:** A `position: absolute` element with `pickingMode: Position` (the default) sits
above the normal flow in hit-testing, even if it is visually only partially overlapping.
Clicking an area covered by the absolute element triggers its event handler, not the element
underneath.

**Rule:** For overlay/reference panels, either:

- Position them so they do not overlap interactive elements (prefer `top` over `bottom` for
  panels at the edge of a layout that has interactive content near the bottom), OR
- Set `pickingMode: Ignore` on the panel container and only make specific child elements
  (e.g., an icon) interactive.

```css
/* Avoid bottom overlap with page-bottom interactive content */
.m3-reference-panel {
    position: absolute;
    top: 16px;   /* not bottom */
    right: 16px;
}
```

---

## 8. M3 Spec Visual Comparison — Required Before Closing Any Component

Per team feedback: visual bugs (oval buttons, misaligned text) were only discovered after
implementation was considered "done". The spec comparison check must happen before reporting
complete.

**Checklist for every new component:**

1. Take a Play-mode screenshot via `ScreenCapture.CaptureScreenshot`
2. Open the corresponding m3.material.io page (e.g., `/components/buttons/overview`)
3. Compare: shape, corner radius, sizing, color, state overlays, typography
4. For sub-pixel issues: use Python PIL/PNG byte parsing to measure actual pixel boundaries
   and compare against expected spec values
5. Document any intentional deviations in the component's USS file header

```python
# Quick pixel-width measurement at a given row
from PIL import Image
img = Image.open("screenshot.png")
pixels = list(img.getdata())
row = [pixels[y * img.width + x] for x in range(img.width)]
# Find leftmost and rightmost non-background pixel
```

---

## Quick Reference

| Issue | Root Cause | Fix |
|---|---|---|
| Oval buttons | `border-radius: 9999px` = ellipse | Use `height/2` explicitly |
| Off-center Painter2D | `contentRect` subtracts padding | Use `new Rect(0,0,w,h)` |
| Text floats upward | Font ascender > descender | `paddingTop: 1f` optical offset |
| Text still off | Unity default Label padding | `padding: 0` in USS |
| `Instance` null after reload | Domain reload resets statics | Lazy `FindObjectOfType` getter |
| Theme switch silent | Panel not in `_managedPanels` | `RegisterPanel(doc)` in Start |
| Click intercepted by overlay | Absolute element hit-test | Move panel or `pickingMode: Ignore` |
