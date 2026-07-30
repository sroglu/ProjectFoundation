# PFound.UISystem — Doc Map

An index for the UISystem doc set. Start at the README, jump here to find the right document.

## Document Tree

| Doc | Purpose |
|-----|---------|
| [README.md](README.md) | Thin landing page: what the module is, quick reference, requirements, doc pointers. |
| [MODULE.md](MODULE.md) | Architecture deep doc: assemblies, dependencies, key types, public API, ThemeManager setup/wiring, file structure, limitations. |
| [COMPONENT-GUIDE.md](COMPONENT-GUIDE.md) | Authoritative rules for creating/modifying M3 components: USS-only theming, exception registry, `M3ComponentBase` pattern, icons, animation, typography, batching baseline. |
| [GUIDELINES.md](GUIDELINES.md) | Hard-won implementation lessons and UI Toolkit gotchas (pill radii, label padding, hit-testing, domain-reload, panel registration). |
| [SCOPE.md](SCOPE.md) | Scope, work packages, technical decisions, shape-primitive selection guide, roadmap, out-of-scope list. |
| [TODO.md](TODO.md) | Known issues / bug tracker (USS parser errors, resolution log). |
| [PAPERCUT.md](PAPERCUT.md) | Papercut sample theme specification (flat sticker / paper-cut visual language). |
| [PAPERCUT-TODO.md](PAPERCUT-TODO.md) | Papercut scaffold-era work plan (closed; historical). |
| [PAPERCUT-FOLLOWUPS.md](PAPERCUT-FOLLOWUPS.md) | Papercut live polish/review backlog (per-component review dimensions). |

## When to Read What

| I want to… | Read |
|------------|------|
| Get started fast (create a theme, bootstrap, add a component) | [README.md](README.md) |
| Understand the architecture (SDF/M3Surface layers, theme system, assemblies) | [MODULE.md](MODULE.md) |
| Wire up `ThemeManager` (automatic Resources vs manual `Initialize`, runtime panels) | [MODULE.md](MODULE.md) → Setup / wiring |
| Look up the public API surface | [MODULE.md](MODULE.md) → Public API |
| Build or modify an M3 component (the mandatory rules) | [COMPONENT-GUIDE.md](COMPONENT-GUIDE.md) |
| Know which shape primitive to use (`SdfShape` vs `M3Surface` vs `GpuSdfElement`…) | [SCOPE.md](SCOPE.md) → Shape Primitives |
| Avoid a known UI Toolkit pitfall while implementing | [GUIDELINES.md](GUIDELINES.md) |
| Understand scope, work packages, and roadmap | [SCOPE.md](SCOPE.md) |
| Check a known bug or its resolution status | [TODO.md](TODO.md) |
| Work on the Papercut sample theme | [PAPERCUT.md](PAPERCUT.md) + [PAPERCUT-FOLLOWUPS.md](PAPERCUT-FOLLOWUPS.md) |
