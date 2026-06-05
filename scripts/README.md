# Guidevault Platform Icon Merge

One-off cleanup script to merge:

```text
src/Guidevault.Web/wwwroot/assets/icons/platforms/Platform Categories
src/Guidevault.Web/wwwroot/assets/icons/platforms/Platforms
src/Guidevault.Web/wwwroot/assets/icons/platforms/Playlists
```

into:

```text
src/Guidevault.Web/wwwroot/assets/icons/platforms
```

## Recommended location

```text
C:\Users\Andrew\Documents\VSCode\GuideVault\scripts\tools\Merge-GuideVaultPlatformIconFolders.ps1
```

## Run dry first

```powershell
& "C:\Users\Andrew\Documents\VSCode\GuideVault\scripts\tools\Merge-GuideVaultPlatformIconFolders.ps1" -DryRun
```

## Run for real

```powershell
& "C:\Users\Andrew\Documents\VSCode\GuideVault\scripts\tools\Merge-GuideVaultPlatformIconFolders.ps1" -UpdateReferences
```

If your repo already has expected changes:

```powershell
& "C:\Users\Andrew\Documents\VSCode\GuideVault\scripts\tools\Merge-GuideVaultPlatformIconFolders.ps1" -UpdateReferences -AllowDirty
```

Then build and commit:

```powershell
cd "C:\Users\Andrew\Documents\VSCode\GuideVault\_repo\Guidevault-source-clean"

dotnet build .\src\Guidevault.Web\Guidevault.Web.csproj --configuration Release

git add .
git commit -m "Merge platform icon folders"
git push origin main
```
