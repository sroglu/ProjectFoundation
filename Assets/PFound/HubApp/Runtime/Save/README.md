# HubApp.Save

Profile-namespaced JSON save persistence. Atomic write via `Utilities/FileSystemTools`, `.bak` fallback, schema version + migrations.

**Scope (planned):** `SaveSchema.cs`, `ISaveService` + `SaveService.cs`, `IMigration` + `MigrationPipeline.cs`. See parent `MODULE.md` for design.
