# Security and performance update notes

This source package advances GuideVault to `1.2.0` with multi-user hardening, performance fixes, expanded IGDB enrichment, and the first Game Dossier workflow.

## First start after updating

Guidevault now protects browser and administrative API routes with server-validated, HTTP-only session cookies. On the first visit, create the primary administrator login. Existing locally saved profile details are used only to prefill the form; browser storage no longer contains passwords.

Administrators can invite multiple users. Each invitation contains a random, one-use activation token that expires after seven days. Accepting it creates an independent PBKDF2-SHA256 credential and account session. Only token hashes, password hashes, salts, and session hashes are persisted; raw passwords, invitation tokens, and session cookies are never written to configuration files.

Sessions remain valid across server restarts until their seven-day expiry or logout. Password changes revoke the user's other sessions. Existing single-administrator authentication files are migrated automatically to the multi-user schema.

Library assignments, age-rating restrictions, unknown-rating rules, and permissions are enforced on the server. Restricted item identifiers return `404`, while disallowed operations return `403`. Browser library caches, favorites, reader bookmarks, and personal reader settings are also scoped by authenticated user to prevent cross-account leakage on a shared browser. Server-wide reader profiles remain administrator-managed.

The legacy LaunchBox browser-login bridge still accepts the existing username/email and password request shape, but it verifies those credentials against the configured server login before creating a short-lived, one-use browser link. Update the connector's saved credentials if the server password changes.

## Notable runtime changes

- IGDB detail resolution now includes summaries, storylines, themes, modes, perspectives, aliases, ratings, media, websites, and related-game families.
- Persistent Game Dossiers group matched GuideVault documents with series, related-game, expansion/DLC, and remake/remaster/port collections.
- Library item lookups use an in-memory ID index rather than repeatedly scanning the full collection.
- Persisted cache startup no longer performs a synchronous file-system stat for every item; normal scans reconcile stale entries.
- Library chunks are copied by index and merged in the browser with a persistent `Map`.
- Archive-entry caching is bounded to prevent unbounded process growth.
- Forced full garbage collections were removed from cache cleanup actions.
- JSON settings and index files are replaced atomically after a successful temporary write.
- Static responses support Brotli/Gzip compression and safe cache lifetimes.
- Background polling starts only after authentication, pauses when its integration is disabled, and avoids work while the page is hidden where applicable.

## Source packaging

Run `package-source.ps1` in PowerShell to create a ZIP without generated `bin`, `obj`, runtime `data`, editor, backup, release, or Git directories.

The source attachment did not contain the `wwwroot/assets` files referenced by the HTML. Those original image, icon, and background assets must be restored from the project repository before a complete production publish.
