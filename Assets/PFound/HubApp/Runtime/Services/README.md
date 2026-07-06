# HubApp.Services

Cross-cutting services for a kid-safe hub app: Profile, Badge, Sticker, PhotoAlbum, Audio, ParentGate, IAP, Analytics.

Each service typically registers with `ServiceRegistry` at bootstrap, subscribes to relevant `Signaling` signals, and persists state via the scoped `SaveService`.

**Scope (planned):** see parent `MODULE.md`.
