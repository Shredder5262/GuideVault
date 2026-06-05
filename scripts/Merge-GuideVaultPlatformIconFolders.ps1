<#
.SYNOPSIS
  One-off cleanup script to merge Guidevault platform icon subfolders into the main platforms folder.

.DESCRIPTION
  Merges:
    src\Guidevault.Web\wwwroot\assets\icons\platforms\Platform Categories
    src\Guidevault.Web\wwwroot\assets\icons\platforms\Platforms
    src\Guidevault.Web\wwwroot\assets\icons\platforms\Playlists

  into:
    src\Guidevault.Web\wwwroot\assets\icons\platforms

  Collision behavior:
    - If destination does not exist: git mv source file to root platforms folder.
    - If destination exists and content is identical: git rm the duplicate source file.
    - If destination exists but content differs: backs up the source file outside the repo,
      then git rm the source file. This keeps the repo clean while preserving a local audit copy.

  Optional:
    -UpdateReferences updates obvious hardcoded references from nested icon folders
     to the flat main platforms folder.

.EXAMPLES
  .\Merge-GuideVaultPlatformIconFolders.ps1 -DryRun

  .\Merge-GuideVaultPlatformIconFolders.ps1 -UpdateReferences

  .\Merge-GuideVaultPlatformIconFolders.ps1 -UpdateReferences -AllowDirty
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = "C:\Users\Andrew\Documents\VSCode\GuideVault\_repo\Guidevault-source-clean",
    [string]$AuditRoot = "C:\Users\Andrew\Documents\VSCode\GuideVault\_audit",
    [switch]$UpdateReferences,
    [switch]$AllowDirty,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Text)
    Write-Host ""
    Write-Host "=== $Text ===" -ForegroundColor Cyan
}

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)

    Write-Host "> git $($Arguments -join ' ')" -ForegroundColor DarkGray
    if ($DryRun) { return }

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed with exit code $LASTEXITCODE`: git $($Arguments -join ' ')"
    }
}

function Get-RelativePath {
    param([string]$Root, [string]$Path)
    return $Path.Substring($Root.Length + 1).Replace('\','/')
}

function Get-UniqueBackupPath {
    param([string]$BaseFolder, [string]$FileName)

    $target = Join-Path $BaseFolder $FileName
    if (-not (Test-Path $target)) {
        return $target
    }

    $name = [System.IO.Path]::GetFileNameWithoutExtension($FileName)
    $ext = [System.IO.Path]::GetExtension($FileName)
    $i = 1

    do {
        $candidate = Join-Path $BaseFolder ("$name.conflict-$i$ext")
        $i++
    } while (Test-Path $candidate)

    return $candidate
}

function Backup-Conflict {
    param(
        [string]$SourcePath,
        [string]$OriginFolder,
        [string]$AuditFolder
    )

    $destFolder = Join-Path $AuditFolder $OriginFolder
    if (-not $DryRun -and -not (Test-Path $destFolder)) {
        New-Item -ItemType Directory -Force -Path $destFolder | Out-Null
    }

    $backupPath = Get-UniqueBackupPath -BaseFolder $destFolder -FileName ([System.IO.Path]::GetFileName($SourcePath))

    Write-Host "Backing up distinct conflict:"
    Write-Host "  Source: $SourcePath"
    Write-Host "  Backup: $backupPath"

    if (-not $DryRun) {
        Copy-Item -LiteralPath $SourcePath -Destination $backupPath -Force
    }

    return $backupPath
}

function Update-TextReferences {
    param([string]$Root)

    $files = @(
        "src\Guidevault.Web\Program.cs",
        "src\Guidevault.Web\wwwroot\app.js",
        "src\Guidevault.Web\wwwroot\index.html",
        "src\Guidevault.Web\wwwroot\styles.css"
    )

    $replacements = @{
        "/assets/icons/platforms/Platform Categories/" = "/assets/icons/platforms/"
        "/assets/icons/platforms/Platforms/"           = "/assets/icons/platforms/"
        "/assets/icons/platforms/Playlists/"           = "/assets/icons/platforms/"
        "assets/icons/platforms/Platform Categories/"  = "assets/icons/platforms/"
        "assets/icons/platforms/Platforms/"            = "assets/icons/platforms/"
        "assets/icons/platforms/Playlists/"            = "assets/icons/platforms/"
    }

    foreach ($relative in $files) {
        $path = Join-Path $Root $relative
        if (-not (Test-Path $path)) { continue }

        $text = Get-Content -Raw -Path $path
        $original = $text

        foreach ($key in $replacements.Keys) {
            $text = $text.Replace($key, $replacements[$key])
        }

        if ($text -ne $original) {
            Write-Host "Updated references in $relative"
            if (-not $DryRun) {
                Set-Content -Path $path -Value $text -Encoding UTF8
            }
        }
    }
}

Require-Command git

if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
    throw "RepoRoot is not a Git repository: $RepoRoot"
}

$targetRoot = Join-Path $RepoRoot "src\Guidevault.Web\wwwroot\assets\icons\platforms"
if (-not (Test-Path $targetRoot)) {
    throw "Platform icon root does not exist: $targetRoot"
}

$foldersToMerge = @(
    "Platform Categories",
    "Platforms",
    "Playlists"
)

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$auditFolder = Join-Path $AuditRoot "platform-icon-merge-$timestamp"

Write-Section "Guidevault Platform Icon Folder Merge"
Write-Host "Repo root:    $RepoRoot"
Write-Host "Icon root:    $targetRoot"
Write-Host "Audit folder: $auditFolder"
Write-Host "Dry run:      $DryRun"

Push-Location $RepoRoot
try {
    $dirty = (& git status --porcelain)
    if ($dirty -and -not $AllowDirty) {
        Write-Host ""
        Write-Host "Repo has uncommitted changes:" -ForegroundColor Yellow
        git status --short
        Write-Host ""
        throw "Commit/stash first, or rerun with -AllowDirty if the current changes are expected."
    }

    Write-Section "Current nested reference scan"
    $referenceFiles = @(
        ".\src\Guidevault.Web\Program.cs",
        ".\src\Guidevault.Web\wwwroot\app.js",
        ".\src\Guidevault.Web\wwwroot\index.html",
        ".\src\Guidevault.Web\wwwroot\styles.css"
    ) | Where-Object { Test-Path $_ }

    $refs = Select-String -Path $referenceFiles -Pattern 'Platform Categories/','Platforms/','Playlists/' -ErrorAction SilentlyContinue
    if ($refs) {
        $refs | Select-Object Path,LineNumber,Line | Format-Table -AutoSize
        if (-not $UpdateReferences) {
            Write-Warning "Nested references were found. Use -UpdateReferences to rewrite obvious icon paths."
        }
    }
    else {
        Write-Host "No obvious nested icon references found."
    }

    $report = New-Object System.Collections.Generic.List[object]

    Write-Section "Merging nested icon folders"
    foreach ($folderName in $foldersToMerge) {
        $folderPath = Join-Path $targetRoot $folderName
        if (-not (Test-Path $folderPath)) {
            Write-Host "Missing folder, skipping: $folderName"
            continue
        }

        Get-ChildItem -Path $folderPath -Recurse -File | ForEach-Object {
            $source = $_.FullName
            $fileName = $_.Name
            $destination = Join-Path $targetRoot $fileName
            $sourceRel = Get-RelativePath -Root $RepoRoot -Path $source
            $destRel = Get-RelativePath -Root $RepoRoot -Path $destination

            if (-not (Test-Path $destination)) {
                Write-Host "Move: $sourceRel -> $destRel"
                Invoke-Git mv $sourceRel $destRel
                $report.Add([pscustomobject]@{
                    Action = "Moved"
                    Source = $sourceRel
                    Destination = $destRel
                    Notes = ""
                })
                return
            }

            $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash
            $destHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash

            if ($sourceHash -eq $destHash) {
                Write-Host "Remove identical duplicate: $sourceRel"
                Invoke-Git rm $sourceRel
                $report.Add([pscustomobject]@{
                    Action = "Removed duplicate"
                    Source = $sourceRel
                    Destination = $destRel
                    Notes = "Identical hash"
                })
                return
            }

            $backupPath = Backup-Conflict -SourcePath $source -OriginFolder $folderName -AuditFolder $auditFolder
            Write-Host "Remove distinct conflict from repo: $sourceRel"
            Invoke-Git rm $sourceRel
            $report.Add([pscustomobject]@{
                Action = "Removed distinct conflict"
                Source = $sourceRel
                Destination = $destRel
                Notes = "Backed up to $backupPath"
            })
        }

        # Remove empty nested folder if Git leaves it locally.
        if ((Test-Path $folderPath) -and -not (Get-ChildItem -Path $folderPath -Recurse -Force -ErrorAction SilentlyContinue)) {
            Write-Host "Remove empty folder: $folderPath"
            if (-not $DryRun) {
                Remove-Item -LiteralPath $folderPath -Force
            }
        }
    }

    if ($UpdateReferences) {
        Write-Section "Updating code references"
        Update-TextReferences -Root $RepoRoot
    }

    Write-Section "Writing audit report"
    if (-not $DryRun) {
        if (-not (Test-Path $auditFolder)) {
            New-Item -ItemType Directory -Force -Path $auditFolder | Out-Null
        }

        $reportPath = Join-Path $auditFolder "platform-icon-merge-report.csv"
        $report | Export-Csv -Path $reportPath -NoTypeInformation -Encoding UTF8
        Write-Host "Report:"
        Write-Host $reportPath
    }
    else {
        $report | Format-Table -AutoSize
    }

    Write-Section "Git status"
    git status --short

    Write-Section "Next commands"
    Write-Host "Run a build:"
    Write-Host '  dotnet build .\src\Guidevault.Web\Guidevault.Web.csproj --configuration Release'
    Write-Host ""
    Write-Host "Commit if everything looks right:"
    Write-Host '  git add .'
    Write-Host '  git commit -m "Merge platform icon folders"'
    Write-Host '  git push origin main'
}
finally {
    Pop-Location
}


