# Guidevault indexing performance

Library scans should feel predictable and should not block normal navigation longer than necessary.

## Goals

- Scans should report clear progress.
- Removing an item from a library should update the UI quickly.
- Refreshing a library should detect new, changed, and removed items reliably.
- Generated metadata and cache work should not make the library appear empty during load.

## Recommended behavior

- Use queued tasks for scan, refresh, cleanup, and removal work.
- Surface task state through the task monitor.
- Keep existing library items visible while a scan is running.
- Mark stale entries for cleanup rather than rebuilding the visible list from scratch.
- Cache cover and preview data so grids do not show blank covers until hover.

## Notes

Large libraries should be treated as long-running background operations. UI state should remain responsive while the backend updates index data.
