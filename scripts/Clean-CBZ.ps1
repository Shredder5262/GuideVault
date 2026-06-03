param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath,

    [switch]$WhatIf
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$cbzFiles = Get-ChildItem -Path $RootPath -Filter *.cbz -Recurse -File

foreach ($cbz in $cbzFiles) {
    Write-Host "`nChecking: $($cbz.FullName)"

    try {
        $zip = [System.IO.Compression.ZipFile]::Open($cbz.FullName, [System.IO.Compression.ZipArchiveMode]::Update)

        try {
            $entriesToDelete = @()

            foreach ($entry in @($zip.Entries)) {
                $path = $entry.FullName.Replace('\', '/')
                $fileName = [System.IO.Path]::GetFileName($path)

                $shouldDelete =
                    $path -match '(^|/)__MACOSX(/|$)' -or
                    $fileName -ieq '.DS_Store' -or
                    $fileName -ieq 'zzz.jpg' -or
                    $path -match '(?i)retromags'

                if ($shouldDelete) {
                    $entriesToDelete += $entry
                }
            }

            if ($entriesToDelete.Count -eq 0) {
                Write-Host "  No junk entries found."
                continue
            }

            foreach ($entry in $entriesToDelete) {
                Write-Host "  Removing: $($entry.FullName)"

                if (-not $WhatIf) {
                    $entry.Delete()
                }
            }

            if ($WhatIf) {
                Write-Host "  WhatIf mode: nothing was actually deleted."
            } else {
                Write-Host "  Cleaned $($entriesToDelete.Count) entries."
            }
        }
        finally {
            $zip.Dispose()
        }
    }
    catch {
        Write-Warning "Failed to process $($cbz.FullName): $($_.Exception.Message)"
    }
}