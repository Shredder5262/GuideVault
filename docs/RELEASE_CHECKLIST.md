# Release checklist

## Server prerequisite

- GuideVault server is running **0.9.260** or newer.
- Confirm the server has LaunchBox scoped sync and relationship endpoints.

## Local plugin build

From the repository root:

```powershell
.\scripts\Build-Plugin.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox"
```

Run the core smoke tests:

```powershell
.\scripts\Test-Core.ps1
```

## Local install smoke test

```powershell
.\scripts\Install-Plugin.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox" -ForceCloseLaunchBox
```

Smoke-test these items inside LaunchBox:

- GuideVault connector opens from the Tools menu.
- Settings shows Sign In and Sign Out & Forget controls.
- Administrator sign-in succeeds and the password field clears immediately.
- A remote plain-HTTP sign-in opens the GuideVault-themed warning with Go Back focused by default.
- Restarting LaunchBox reuses the DPAPI-encrypted credentials without placing them in `settings.json`.
- Expiring the server session causes one automatic credential re-authentication and request retry.
- Embedded and default-browser opens consume a short-lived authenticated link and reach the requested target without a second login prompt.
- Sign Out & Forget removes `Plugins\GuideVault\auth-session.dat` and protected API calls request sign-in again.
- Status tab shows plugin and server version.
- Sync Library works.
- Sync Selected Manuals works.
- A manual-only sync output omits strategy-guide/magazine counters and shows imported, manual-matched, ambiguous, unmatched, and current-game values.
- An all-content sync output still shows manual, strategy-guide, and magazine counters.
- Sync All Manuals works.
- Sync Selected Guides works.
- Sync All Guides works.
- Sync Selected Magazines works.
- Sync All Magazines works.
- View Matched Guides opens.
- View Matched Magazines opens.
- Open Manual works from a matched game.
- Open Strategy Guide works from a matched game.
- Open Magazine works from a matched game.
- Embedded WebView2 window opens.
- Fullscreen opens correctly.
- Escape closes fullscreen/window as expected.
- Cursor does not constantly flip into a loading state while idle.
- Right-click GuideVault menus open immediately while the server is stopped and show cached/loading status.
- A completed sync refreshes badge and right-click match caches.
- A legacy settings file has credential properties removed after it is loaded.
- An invalid or cross-origin server-provided open URL is rejected.

## Package

```powershell
.\scripts\Package-PluginRelease.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox"
.\scripts\Verify-Package.ps1 -PackagePath ".\dist\GuideVault-LaunchBox-Connector-0.4.28-LaunchBoxRoot.zip"
```

The package script creates a LaunchBox-root-style zip under `dist`, for example:

```text
dist\GuideVault-LaunchBox-Connector-0.4.28-LaunchBoxRoot.zip
```

Upload that generated file to GitHub Releases.

## Package structure check

Open the zip and confirm the top level contains:

```text
Plugins\GuideVault\
ThirdParty\GuideVaultReaderLauncher\
Images\Media Packs\Overrides\Badges\
README_INSTALL.txt
RELEASE_NOTES_0.4.28.md
LICENSE
THIRD-PARTY-NOTICES.md
CHECKSUMS.txt
```

The zip should not contain a personal machine path, a parent folder such as `C:\Users\...`, `settings.json`, or `auth-session.dat`.

Confirm the adjacent `.sha256` file matches the published release ZIP. For a source release, run `.\scripts\Package-Source.ps1` from a clean Git worktree so build output, nested archives, and repository metadata are excluded.
