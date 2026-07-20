# GuideVault 1.3.0

GuideVault 1.3.0 delivers a cleaner manual-library workflow, safer reading-profile behavior, dossier presentation fixes, and a rebuilt first/last page-turn sequence.

## Highlights

- Groups the active manual system, item count, cover-size control, and sorting control into a unified library header.
- Tightens manual cards, removes the CBZ Pages badge, and shows the favorite indicator only for favorited items.
- Adds a three-dot item menu for opening, favoriting, refreshing one item, and adding content to collections or dossiers.
- Fixes dossier background scrolling while preserving the dossier artwork layer.
- Keeps one-off reader setting changes session-specific instead of automatically creating per-book reading profiles.
- Prevents duplicated or missing final pages in two-page mode and centers an unpaired final page after it turns onto the left.
- Rebuilds the first and last page turns with curved, aspect-preserving front/back textures so the correct interior or final page appears without mirroring or stretching.
- Delays the first interior spread until the cover is visibly turning and suppresses page-stack artifacts during edge-page handoffs.
- Reduces page-turn rendering overhead with bounded texture/canvas sizes, lighter curl meshes, decoded-image caching, and coordinated WebGL-to-DOM handoffs.

## Packaging

Runtime library data, caches, generated build folders, backups, and editor metadata are excluded from the source archive.
