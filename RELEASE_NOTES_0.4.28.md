# GuideVault LaunchBox Connector 0.4.28

This release reimagines the GuideVault LaunchBox Connector as a visual companion application rather than a log-driven synchronization utility.

## Highlights

- Added a visual Dashboard with synchronization, coverage, missing-game, document, and review statistics.
- Consolidated manuals, strategy guides, and magazines into one Content Sync workspace.
- Added an in-app Platform Games review workflow with LaunchBox and GuideVault cover previews.
- Added Match, Review Suggestion, Change Match, Unlink, and optional bulk suggestion unlinking.
- Added a side-by-side Match Studio with ranked candidates, confidence details, platform information, and manual search.
- Improved matching performance, title normalization, result caching, loading feedback, and candidate ranking.
- Improved username and email authentication compatibility.
- Improved Connection Health diagnostics, reader launching, DPI behavior, and deployment validation.
- Removed misleading LaunchBox artwork fallbacks when genuine GuideVault document artwork is unavailable.

## Privacy

The distribution package excludes connector settings, authentication sessions, WebView2 profiles, debug symbols, local installation paths, and source-control metadata.

## Installation

1. Close LaunchBox and BigBox.
2. Extract the release ZIP into the folder containing LaunchBox.exe.
3. Allow existing plugin files to be overwritten.
4. Restart LaunchBox.
5. Open Tools > GuideVault Connector.

Expected installer confirmation:

VERIFIED INSTALL: GuideVault LaunchBox Connector 0.4.28
