param(
    [Parameter(Mandatory)]
    [string]$FolderPath,

    [switch]$Recurse,

    [switch]$CreateBackup,

    [switch]$WhatIf
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$resolvedFolder = (Resolve-Path -LiteralPath $FolderPath).Path

$searchParameters = @{
    LiteralPath = $resolvedFolder
    Filter      = '*.cbz'
    File        = $true
}

if ($Recurse) {
    $searchParameters.Recurse = $true
}

$cbzFiles = Get-ChildItem @searchParameters

if (-not $cbzFiles) {
    Write-Warning "No CBZ files were found in: $resolvedFolder"
    exit
}

$archivesModified = 0
$entriesDeleted   = 0
$errorsEncountered = 0

foreach ($cbz in $cbzFiles) {
    $archive = $null

    try {
        # Open the CBZ archive and identify ComicInfo.xml entries.
        $archive = [System.IO.Compression.ZipFile]::Open(
            $cbz.FullName,
            [System.IO.Compression.ZipArchiveMode]::Update
        )

        # Convert to an array before deleting entries.
        $comicInfoEntries = @(
            $archive.Entries | Where-Object {
                [System.IO.Path]::GetFileName($_.FullName) -ieq 'ComicInfo.xml'
            }
        )

        if ($comicInfoEntries.Count -eq 0) {
            Write-Host "No ComicInfo.xml: $($cbz.FullName)"
            continue
        }

        if ($WhatIf) {
            foreach ($entry in $comicInfoEntries) {
                Write-Host "[WHATIF] Would delete '$($entry.FullName)' from '$($cbz.FullName)'"
            }

            continue
        }

        if ($CreateBackup) {
            # Dispose the archive temporarily so the file can be copied safely.
            $archive.Dispose()
            $archive = $null

            $timestamp  = Get-Date -Format 'yyyyMMdd-HHmmss'
            $backupPath = "$($cbz.FullName).$timestamp.bak"

            Copy-Item -LiteralPath $cbz.FullName -Destination $backupPath
            Write-Host "Backup created: $backupPath"

            $archive = [System.IO.Compression.ZipFile]::Open(
                $cbz.FullName,
                [System.IO.Compression.ZipArchiveMode]::Update
            )

            $comicInfoEntries = @(
                $archive.Entries | Where-Object {
                    [System.IO.Path]::GetFileName($_.FullName) -ieq 'ComicInfo.xml'
                }
            )
        }

        foreach ($entry in $comicInfoEntries) {
            $entryName = $entry.FullName
            $entry.Delete()

            Write-Host "Deleted '$entryName' from '$($cbz.Name)'"
            $entriesDeleted++
        }

        $archivesModified++
    }
    catch {
        Write-Warning "Failed to process '$($cbz.FullName)': $($_.Exception.Message)"
        $errorsEncountered++
    }
    finally {
        if ($null -ne $archive) {
            $archive.Dispose()
        }
    }
}

Write-Host ""
Write-Host "Finished."
Write-Host "CBZ files scanned: $($cbzFiles.Count)"
Write-Host "Archives modified: $archivesModified"
Write-Host "ComicInfo.xml files deleted: $entriesDeleted"
Write-Host "Errors: $errorsEncountered"


#.\Remove-ComicInfo.ps1 -FolderPath "\\DIEMOS\Multimedia\Digital Literature\Video Game Manuals" -Recurse -WhatIf