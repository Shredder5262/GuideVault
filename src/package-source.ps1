[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "guidevault-source.zip")
)

$ErrorActionPreference = "Stop"
$sourceRoot = (Resolve-Path $PSScriptRoot).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $outputFullPath
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$sourceParent = Split-Path -Parent $sourceRoot
$excludedDirectories = @('bin', 'obj', 'data', '.git', '.vs', '.vscode', '_backup', '_releases')
$archiveStream = [System.IO.File]::Open($outputFullPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
try {
    $archive = [System.IO.Compression.ZipArchive]::new($archiveStream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        Get-ChildItem -LiteralPath $sourceRoot -File -Recurse -Force | ForEach-Object {
            $sourceFile = $_
            $relativeToSource = [System.IO.Path]::GetRelativePath($sourceRoot, $sourceFile.FullName).Replace('\', '/')
            $segments = $relativeToSource.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)
            $isExcluded = [System.IO.Path]::GetFullPath($sourceFile.FullName) -eq $outputFullPath
            foreach ($segment in $segments) {
                if ($excludedDirectories -contains $segment) { $isExcluded = $true; break }
            }

            if (-not $isExcluded) {
                $entryName = [System.IO.Path]::GetRelativePath($sourceParent, $sourceFile.FullName).Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $sourceFile.FullName,
                    $entryName,
                    [System.IO.Compression.CompressionLevel]::Optimal
                ) | Out-Null
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $archiveStream.Dispose()
}
Write-Host "Created clean source package: $outputFullPath"
