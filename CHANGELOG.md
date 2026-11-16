# Changelog

## 0.4.28

### Reimagined connector interface

- Rebuilt the connector around a visual Dashboard, unified Content Sync workspace, Match Studio, Settings, Log, and About pages.
- Added responsive dashboard coverage bars for synchronized systems, imported games, matched games, missing games, manuals, strategy guides, magazines, and the review queue.
- Added persistent synchronization progress with a doughnut indicator, collapsible job details, status-aware activity output, and improved high-DPI sizing.
- Moved Connection Health into a collapsible Settings section and improved server, authentication, LaunchBox, transport, and synchronized-data diagnostics.

### Unified Content Sync and platform review

- Consolidated manual, strategy-guide, and magazine synchronization into one medium-driven workspace.
- Added automatic platform refresh, per-medium selections, Select All, Uncheck All, and selected-system synchronization.
- Added an in-app Platform Games view with game covers, GuideVault document covers, hover previews, search, status filters, and Back navigation.
- Added per-game Match, Review suggestion, Change match, and Unlink actions.
- Distinguished confirmed links from unconfirmed suggested matches.
- Added optional bulk suggestion unlinking with multi-selection, themed confirmation, modal progress, partial-failure reporting, and automatic platform refresh.

### Match Studio

- Added side-by-side LaunchBox game and GuideVault document comparison.
- Added ranked candidate cards with platform/system, content type, year, publisher, match status, confidence, and matching reasons.
- Added explicit Search behavior, live LaunchBox-selection handling, manual GuideVault search, Confirm, Reject, Undo, Preview, and direct matching.
- Added visible loading feedback while candidates are retrieved.
- Improved candidate ranking, title normalization, platform/year conflict handling, caching, and asynchronous supplemental searching.
- Removed misleading LaunchBox game-art substitution when an actual GuideVault document cover is unavailable.

### Authentication, reader, and reliability

- Added compatible username or email sign-in request handling.
- Improved encrypted credential/session reuse and authentication recovery.
- Improved reader-helper discovery and browser fallback behavior.
- Renamed the embedded reader behavior to Borderless fullscreen (Embedded WebView2).
- Added startup guards, compile-check projects, package verification, deployment version checks, duplicate-DLL checks, and numerous layout and DPI corrections.

## 0.4.27

- Added strict source/built/installed assembly-version verification to the Windows build and install scripts.
- Added SHA-256 comparison after installation and reject duplicate connector DLLs anywhere under `LaunchBox\Plugins`.
- Added a clear nested-source-tree error for ZIPs extracted inside an older connector project.
- Added the loaded connector DLL path and assembly version to the startup log so stale deployments are immediately visible.
- Retained the requested sync scope when older GuideVault responses omit a job ID, and accepted server-provided `matchTypes` when available.

## 0.4.26

- Made live sync output scope-aware: manual-only, strategy-guide-only, and magazine-only jobs now show only the requested content counters.
- Reworked progress lines around scope, checked/total games, imported games, matched items, ambiguous results, and unmatched games.
- Added the currently checked LaunchBox game title/platform by correlating the server checkpoint with the deterministic submitted game order.
- Added forward-compatible current-game fields so newer GuideVault servers can provide an exact title/platform directly.
- Applied the same scope-aware reporting to the Status job summary and added smoke coverage for scoped/all-content output.

## 0.4.25

- Replaced the stock Windows insecure-HTTP confirmation with a dark GuideVault-themed security dialog.
- Made the safer **Go Back** action the default while retaining an explicit **Continue Anyway** option.
- Clarified the distinction between DPAPI encryption on the local PC and unencrypted HTTP transport to the GuideVault server.

## 0.4.24

- Stored the administrator username/email and password inside the existing DPAPI-protected authentication file, scoped to the current Windows user and configured GuideVault server.
- Added automatic API re-authentication and one retry when the server session expires.
- Added short-lived, single-use browser-login links so embedded reader and default-browser launches inherit the connector sign-in without exposing the password to the reader process.
- Renamed Sign Out to Sign Out & Forget and made it remove both the encrypted credentials and connector session.

## 0.4.23

- Added a GuideVault administrator sign-in/sign-out section for protected integration endpoints. Passwords remain memory-only; the opaque server session is encrypted for the current Windows user.
- Added authenticated cookie handling for status, coverage, badge, relationship, job, and cancellation requests, with a clear expired-session message on HTTP 401.
- Removed stored username/password fields and DOM login injection. Existing WebView2 sessions remain persistent, and legacy credential fields are scrubbed automatically.
- Restricted reader navigation, web messages, and server-returned open URLs to HTTP(S) and the configured GuideVault origin; external links open in the system browser.
- Redacted sensitive launcher URL/query data and sanitized API errors instead of logging arbitrary server response bodies.
- Replaced blocking right-click network lookups with a disk-backed background match cache and refresh it after sync completion.
- Corrected manual sync scoping and type-specific missing/coverage calculations for manuals, strategy guides, and magazines.
- Added cancellation-aware requests, bounded per-operation timeouts, atomic settings writes, and corrupt-settings recovery.
- Removed obsolete menu classes and the unused selected-game tab.
- Centralized the connector version and made the reader helper a self-contained `win-x64` publish.
- Added clean source/release packaging, SHA-256 manifests, package validation, license/notices, and core smoke tests.
- Preserved existing settings during local install while removing obsolete credential/debug properties.

## 0.4.22

Release hardening for the first packaged GuideVault LaunchBox Connector build.

- Added clearer plugin/server/last-sync status visibility.
- Added scoped sync controls for manuals, strategy guides, and magazines.
- Added selected/all sync support for strategy guides and magazines.
- Added match review popups for strategy guide and magazine relationships.
- Improved match review counts, low-confidence visibility, refresh time, and empty states.
- Reduced background polling and UI wait-cursor churn.
- Disabled sync controls while a sync/match job is active.
- Removed stale/obsolete selected-game guide actions from coverage tabs.
- Uses the external GuideVaultReaderLauncher helper for embedded WebView2 behavior.

Requires GuideVault server 0.9.258 or newer. Server 0.9.260 or newer is recommended.
