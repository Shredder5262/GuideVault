# Content Intelligence (v5.1) — review-only preview

The existing Scan Magazine workspace now exposes a separate Content Intelligence panel for administrators. It consumes a complete current v8.0-phase1 document index and the synced LaunchBox catalog; it does not create an index, change metadata, or save game relationships. Run Scan Magazine to build the index first. Applying metadata suggestions is not required to run Content Intelligence.

- Analyze Content: indexed text and catalog matching; no model inference.
- Recover missing headings: optional targeted Tesseract pass on PDF or ZIP-based CBZ, with the same overall budget. It is off by default; other archive formats use indexed OCR only.
- Load Last Review: loads the last successfully published report for the exact item ID and current source fingerprint. A changed source needs a fresh review.
- Stop: cancels the browser request and requests server-side termination. A previous successful review is retained on failure/cancellation. Individual run artifacts remain for diagnostics.
- Game matches, Sections and Unresolved evidence are separate tabs. Unresolved rows group identical candidate ID sets and keep every page and evidence record. Suppressed cover branding and unsupported single-word alphabetic aliases are retained in `suppressed_evidence`, outside the normal review list. Earlier saved reports can still be viewed; analyze again to apply v5.1. Page links use the reader's zero-based archive page API or the PDF viewer's one-based page fragment. Article ranges and platform inference are suggestions, not confirmed coverage.

## Image and persistence

The image includes `vision/content_intelligence.py` at `/opt/guidevault-vision/content_intelligence.py`, using the existing `/opt/guidevault-vision/bin/python`. Build-time checks verify Python syntax, CLI routing, Tesseract English data, Poppler availability, Pillow, PyTorch, Torchvision and model class imports. They do not download weights or run inference. The existing Florence and Qwen worker remains in place, and its cache lives under the persistent `GUIDEVAULT_DATA/vision-models` directory. The Content Intelligence preview does not load either model. Do not commit models or personal library data.

The API uses a single selected magazine, the existing library I/O gate and an engine budget of 118 seconds, with a 120-second request cancellation deadline. Cleanup may take a few additional seconds if a process must be killed. Index creation is separate and is not subject to this analysis budget. The source path, catalog path, index directory and report root are chosen by the server, not submitted by the browser. Reports are scoped under `GUIDEVAULT_DATA/content-intelligence-results/managed/<document hash>/<source fingerprint>/`; previous experimental reports are not imported or deleted. Only a successful, complete-index report is published as `magazine-review.json`. Visual coverage can still be partial, as indicated by the report. This release does not automatically prune historical runs.

API (admin only): `GET /api/items/{id}/content-intelligence`; `POST /api/items/{id}/content-intelligence?visual=false` (or `true`). The POST returns `{report, seconds}`; GET returns `{report}` (null if no current report). No apply endpoint is included.

## Deployment validation

Build with the existing SixLabors build secret (never put its value in source). For example, with `SIXLABORS_LICENSE_B64` already provided via a secure local environment, run:

```powershell
docker build --secret id=sixlabors_license_b64,env=SIXLABORS_LICENSE_B64 -t guidevault:ci-preview .
```

Test the preview image before pushing main or a release tag: the existing workflows publish GHCR images automatically. Keep the original image and `/data` volume; do not run two instances against the same writable volume concurrently. The app's public version remains 1.3.2 for this patch.

Smoke check after deployment:

```powershell
docker exec guidevault /opt/guidevault-vision/bin/python /opt/guidevault-vision/content_intelligence.py --help
```

Then select an already-indexed Nintendo Power issue, open Scan Magazine, and use Analyze Content. The known issue #1 produced five tentative article game leads and 18 navigation leads in the full-catalog v4.8 benchmark. Replaying those reports with v4.9 retains the five and adds a tentative Kid Icarus gameplay-heading lead. For the known Sega Force #2 benchmark, enable targeted OCR; the previous full-catalog result was 17 platform-supported and 41 inferred game leads. Counts may vary if the source/index/catalog changes. Verify page links, admin restrictions, cancellation, saved-review reload, and stale-source isolation before enabling any write-back.

Local v4.9 patch validation: cached report replay preserved all 58 Sega Force game IDs (17 platform-supported and 41 inferred) and all five Nintendo Power article game IDs; one extra Nintendo Power gameplay-heading lead, Kid Icarus, was recovered. Four weak alias observations in Sega Force are retained as diagnostics. A regression fixture using the reported Sega Challenge cover heading verifies the masthead guard. No original Sega Challenge index was available for a complete scan. A split-title guard prevents promotion of the incomplete SUPER / MARIO BROS. heading from the Nintendo Power report. The underlying game catalog is never altered. Browser visual validation and deployed full-catalog scan remain required.

## v5.0 contextual review

Explicit review/preview headings, prominent full title headings and repeated body mentions can create tentative game review leads when exactly one catalog identity is compatible with the chronology and available platform evidence. A checklist or cover reference cannot create a game lead by itself; it may supplement an existing uniquely compatible game identity. Review evidence records retain all catalog alternatives and separately expose viable candidates. Preferred reader pages favor substantive coverage. Existing TOC and visual-profile logic remains unchanged. Page classifications are conservative and do not independently confirm article coverage; classified checklist references are not automatically published or individually classified as upcoming releases.

## v5.1 region review

A bounded, read-only region pass uses existing layout OCR to find title-led tips in a tips column, prominent game features beside that column, and game-specific captions or explicit preview statements inside hardware features. It uses original catalog discovery records, including low-priority candidates, without inventing IDs. A manufacturer editorial plus the specific Power Base, SegaScope and Light Phaser references can support a tentative Master System inference for this period; this is not an explicit issue platform or verified coverage. Contents and scoreboards are supporting references, not independent game leads. The new `region-leads.json` retains OCR geometry, candidate provenance, and unresolved full titles. Mixed-page classification remains heuristic. Missing or incompatible catalog identities remain unresolved. No metadata, relationship, index or catalog writes are introduced.


## v5.2 — Human review workflow (v5.1 recognition engine unchanged)

Analyze Content remains read-only. In Game Matches, expand a proposed game, inspect its evidence, choose a substantive digital page and explicitly Confirm, Reject, or Correct Match. Correction searches the synchronized LaunchBox catalog; verify exact platform, edition, title and content before committing. Correction saves the rejection of the old pair and confirmation of the new pair together; a target with an existing decision requires separate review in LaunchBox Links. Reference-only entries (cover, checklist, contents, high-score and possible advertisements) are separated from unresolved substantive evidence. They cannot be approved independently by this workspace. Unresolved-only identity repair is not implemented in this first decision UI.

Decisions use the existing LaunchBox relationship store (`/data/config/launchbox.integration.json`), not a separate database or magazine archive/metadata write-back. A saved decision includes the current source fingerprint, a single human-reviewed digital destination and coverage type on the relationship. Confirmed/rejected relationships survive analysis reruns; the existing rematch preserves confirmed/rejected pairs. Resetting/deleting relationships or replacing the synced game catalog can remove them. Review tokens and pair versions guard against stale submissions. The admin-only decision route checks visible magazine access and blocks during a rematch. Take a consistent backup of the integration JSON before the first write test. A source fingerprint mismatch requires re-review and suppresses the CI relationship in the direct and edition-group literature selection; legacy metadata fallback (if independently enabled) is not changed.

The authenticated literature response now includes optional `preferredDigitalPage` (one-based) and `coverageType` for direct, confirmed, current-source Content Intelligence relationships. The existing `readerUrl` remains unchanged. A DreamPause/client or reader update is still needed to open at the approved page automatically. No client update is included here.

### First deployment smoke test

1. Load the saved Sega Challenge review. Check that Game Matches shows the same seven proposals and References separates checklist-only evidence. No analysis rerun required.
2. Expand OutRun, inspect digital page 6, choose it and Confirm. Reload the saved review and check Saved decisions and LaunchBox Links. Only this game/publication pair should change.
3. Re-run analysis. The match stays confirmed and the selected page remains 6. Check the authenticated literature API / DreamPause for this magazine.
4. Test Reject on an intentionally selected test pair, then re-confirm if desired. Test correction only with a real misidentified game and an unused target; do not correct a known-good association just to exercise a button.
5. Verify that non-admin users cannot call decision/catalog endpoints. Verify no other magazine relationships were modified.

The scanner still uses the existing 120-second request budget. This integration requires .NET compilation, Docker deployment and live decision testing before production approval.

## v5.3 — Unified single-issue Content Intelligence

The magazine detail tab is now Content Intelligence. Analyze Content uses the existing server-owned document-index job, reuses the current index, and resumes missing page records. It then runs the Python game review and extracts publication metadata with the existing publication/editorial evidence routines (cached vision only). The Content Intelligence report supplies featured games, reference-only mentioned titles and editorial platform proposals; existing entries are preserved because discovery is incomplete. Explicit publication/cover evidence and legacy department extraction still support overview fields. The older batch Metadata Manager scanner is retained for compatibility and is not part of the unified detail workflow.

A single 120-second cancellation budget covers indexing and both analysis stages, including optional targeted heading OCR; cleanup may add a few seconds. Large unindexed issues may require repeated runs. Stage progress is based on completed index pages or reported title/crop counts. Estimated remaining time is **stage** time, not a countdown to the deadline; when no throughput is available the stage is indeterminate. Stop retains completed index records. A successful review may exist even when the metadata stage times out; a previous full metadata preview is not applicable to a failed job. Last scan metadata loaded from disk after a restart is diagnostic only; reanalyze to obtain a new guarded apply preview.

Overview Metadata displays proposed changes and evidence. Select fields and apply explicitly; the existing journal, metadata status protections, field locks and stale override checks remain active. Confirmed game matches still use LaunchBox Links; the redundant Saved Decisions tab is removed. The existing combined barcode/UPC/ISSN field is retained; checksum-valid UPC-A cover evidence is now recognized, but conflicting identifiers require manual review. A physical page count is proposed only with an explicit printed total. Digital image count is displayed separately; no automatic physical total is inferred from incomplete folios, spreads or covers. Existing page metadata may still need correction. No automatic metadata write or game approval is performed.

Expanded coverage labels use nearby heading/body geometry and existing local coverage cues. Dedicated feature, spotlight, boss guide, preview, interview, gameplay information and reference labels can be proposed without changing identity selection. Manual coverage labels may be selected for a resolved game's substantive reviewed page. References remain separate; unresolved identity correction is not included. These are heuristics and do not guarantee full coverage classification. Existing confirmed links and approved page destinations remain authoritative.

Deployment checks: replay known Nintendo Power and Sega Force cached reports, then run live Sega Challenge analysis and verify the confirmed OutRun relationship. Test an unindexed issue, stop/resume, page progress, timeout, field locks and explicit metadata application before bulk use. The direct legacy review endpoint remains for compatibility; the new detail UI starts `/document-index/job?catalog=true&preview=true&unified=true` instead.
