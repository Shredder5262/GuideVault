# DreamPause — GuideVault Manual & Embedded Reader Polish v1.8

**Source patch test candidate**, based on the exact delivered DreamPause GuideVault Literature Client v1.7 module files. It modifies only two C# files, never shell artwork, server code, LaunchBox XML, or actual documents.

## Changes

- Manual cover preview: 854 × 342 canvas at x318, y250 (replacing 834 × 401 at x306, y266); centered aspect-preserving image fully above the lower chrome edge. Preserves actual `/cover-thumb` caching and lazy WebView2 startup.
- Manual preview: removes the sentence "Preview uses no live reader; full reader starts only when opened". Replaces provider-forward `GUIDEVAULT` heading with `BOOKLET`. Keeps Retry and Open in GuideVault buttons **only in GuideVault primary Manual reader mode** (BuiltIn and External retain their own existing screens/actions). The large **Open Full Screen** button remains available for a found manual.
- Embedded GuideVault reader: top bar displays the **game title only**. Removes DreamPause zoom (+/−), percentage, and Fit/Reset toolbar controls (does not interfere with the web reader's own reading options).
- Bottom toolbar: **BACK** at left (no redundant Close button); **PREV PAGE** and **NEXT PAGE** centered regardless of other controls; **AUTO: OFF/ON** and 3 / 5 / 10 / 15 SEC options at right.
- Auto turns via GuideVault's native `#nextPage` button and pauses automatically when the native reader is unavailable, the page count reaches the end, a new navigation occurs, or the window closes. It starts **off** each time and never runs on the lightweight Manual preview.

## Install

1. **Close LaunchBox and Big Box**. Unzip this release without separating `Payload/`, `manifest.json`, or the installer.
2. Inside the extracted package, preview against the current DreamPause source:
   ```powershell
   .\Install-DreamPause-GuideVault-Reader-Polish-v1.8.ps1
   ```
3. Apply the source replacement if preflight passes:
   ```powershell
   .\Install-DreamPause-GuideVault-Reader-Polish-v1.8.ps1 -Apply
   ```
4. From `C:\Users\Andrew\Documents\VSCode\ShredderPauseNext`, build AND deploy:
   ```powershell
   .\Build-Deploy.ps1 -Deploy
   ```
5. Restart LaunchBox and open *Super Mario Bros. 2* → Manual → Open Full Screen; verify both short and long covers, centered page buttons, Back, and auto turn at 3/5/10/15 seconds. Also open Nintendo Power from Guides and check its reader controls.
6. Toggle Manual Reader between BuiltIn/External/GuideVault in Settings and verify the provider-specific footer buttons do not appear in other modes.

## Safety and compatibility

- Manifest SHA256 preflight validates the precise **v1.7** source module inputs; it will **refuse unknown local edits** rather than overwrite your ongoing project work. No files change without `-Apply`. Idempotent when already installed. Modified files are backed up under `ThemeBackups/` before replacement, with rollback on a write/hash failure.
- This environment lacks Windows/.NET WPF build and a running DreamPause. Static source checks pass; live build, visual fit, WebView2 page-turning, and timing **have not been runtime-verified**. Treat as a test candidate and review build output.
- GuideVault's DOM reader controls `#nextPage`, `#prevPage`, `#readerPagePosition`, and `#readerView` were corroborated against the supplied GuideVault source snapshot, but may differ from its latest deployed build. If they differ, this patch safely stops auto-turning instead of navigating unrelated pages.
- The native reader owns progress and flip animation. The auto-timer does not download full manuals or scan images. Closing the window stops the timer.
- For rollback: restore the two files from the backup directory printed by installer, then rebuild/deploy.
