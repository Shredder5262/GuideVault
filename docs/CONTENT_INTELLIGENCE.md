# Content Intelligence (v4.8) — review-only preview

The existing Scan Magazine workspace now exposes a separate Content Intelligence panel for administrators. It consumes a complete current v8.0-phase1 document index and the synced LaunchBox catalog; it does not create an index, change metadata, or save game relationships. Run Scan Magazine to build the index first. Applying metadata suggestions is not required to run Content Intelligence.

- Analyze Content: indexed text and catalog matching; no model inference.
- Recover missing headings: optional targeted Tesseract pass on PDF or ZIP-based CBZ, with the same overall budget. It is off by default; other archive formats use indexed OCR only.
- Load Last Review: loads the last successfully published report for the exact item ID and current source fingerprint. A changed source needs a fresh review.
- Stop: cancels the browser request and requests server-side termination. A previous successful review is retained on failure/cancellation. Individual run artifacts remain for diagnostics.
- Game matches, Sections and Unresolved evidence are separate views. Page links use the reader's zero-based archive page API or the PDF viewer's one-based page fragment. Article ranges and platform inference are suggestions, not confirmed coverage.

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

Then select an already-indexed Nintendo Power issue, open Scan Magazine, and use Analyze Content. The known issue #1 produced five tentative article game leads and 18 navigation leads in the full-catalog benchmark. For the known Sega Force #2 benchmark, enable targeted OCR; the previous full-catalog result was 17 platform-supported and 41 inferred game leads. Counts may vary if the source/index/catalog changes. Verify page links, admin restrictions, cancellation, saved-review reload, and stale-source isolation before enabling any write-back.

Local patch validation: Python syntax/CLI and JavaScript syntax checked; both uploaded cached benchmark reports reproduced exactly for games, unresolved evidence and navigation. Fresh explicit-directory invocation tested with a reconstructed 114-page index and a 350-entry partial catalog. The actual .NET/Docker build, original magazine page rendering, web UI, and full-catalog fresh run still require validation in the deployment environment.
