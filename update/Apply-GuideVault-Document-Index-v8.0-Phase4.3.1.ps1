[CmdletBinding()]
param([string]$RepoRoot=(Get-Location).Path)
$ErrorActionPreference='Stop'
$RepoRoot=(Resolve-Path -LiteralPath $RepoRoot).Path
if(!(Test-Path -LiteralPath (Join-Path $RepoRoot 'GuideVault.sln'))){throw 'Run from the GuideVault repository root.'}
function HashText([string]$s){$sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($s)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}}
$name='src/Guidevault.Web/GuidevaultDocumentIndex.cs'
$path=Join-Path $RepoRoot $name
$before='aa10a60e1ee283f9f0d0c9bf0be759bfdaa77dff969b1c00a23c40236f87505c'
$after='e8ae98775aa34f14f6d4f7a2ed18d55cd34615d9794a958bd6cae7fd0407fdac'
$original=[IO.File]::ReadAllText($path).Replace("`r`n","`n")
$hash=HashText $original
if($hash -eq $after){Write-Host 'Phase 4.3.1 source correction already applied.'}
elseif($hash -ne $before){throw 'Unexpected source revision. Apply only to the original Phase 4.3 source. No files changed.'}
else{
    $anchor=@'
            using var image=Image.Load<L8>(args[2]);
            var scale=Math.Min(1d,edge/(double)Math.Max(image.Width,image.Height));
            int width=Math.Max(1,(int)Math.Round(image.Width*scale)),height=Math.Max(1,(int)Math.Round(image.Height*scale));
            if(width!=image.Width||height!=image.Height)image.Mutate(x=>x.Resize(width,height));
            await image.SaveAsPngAsync(args[4]);
'@
    $replacement=@'
            using var recoveryImage=Image.Load<L8>(args[2]);
            var scale=Math.Min(1d,edge/(double)Math.Max(recoveryImage.Width,recoveryImage.Height));
            int width=Math.Max(1,(int)Math.Round(recoveryImage.Width*scale)),height=Math.Max(1,(int)Math.Round(recoveryImage.Height*scale));
            if(width!=recoveryImage.Width||height!=recoveryImage.Height)recoveryImage.Mutate(x=>x.Resize(width,height));
            await recoveryImage.SaveAsPngAsync(args[4]);
'@
    $anchor=$anchor.Replace("`r`n","`n")
    $replacement=$replacement.Replace("`r`n","`n")
    if(([regex]::Matches($original,[regex]::Escape($anchor))).Count -ne 1){throw 'Expected recovery image block not found exactly once. No files changed.'}
    $target=$original.Replace($anchor,$replacement)
    if((HashText $target) -ne $after){throw 'Corrected source checksum mismatch. No files changed.'}
    $backup=Join-Path $RepoRoot ('.guidevault-patch-backups/document-index-v8-phase4.3.1-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    $dest=Join-Path $backup $name
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest)|Out-Null
    Copy-Item -LiteralPath $path -Destination $dest
    $bytes=[IO.File]::ReadAllBytes($path)
    $bom=$bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191
    try{
        [IO.File]::WriteAllText($path,$target,[Text.UTF8Encoding]::new($bom))
        if((HashText ([IO.File]::ReadAllText($path))) -ne $after){throw 'Write verification failed.'}
    }catch{Copy-Item -LiteralPath $dest -Destination $path -Force;throw}
    Write-Host "Backup: $backup"
}
& git -C $RepoRoot update-index --no-assume-unchanged -- $name
if($LASTEXITCODE -ne 0){throw 'Could not clear assume-unchanged.'}
& git -C $RepoRoot update-index --no-skip-worktree -- $name
if($LASTEXITCODE -ne 0){throw 'Could not clear skip-worktree.'}
& git -C $RepoRoot add -f -- $name
if($LASTEXITCODE -ne 0){throw 'Git staging failed.'}
& git -C $RepoRoot diff --exit-code -- $name
if($LASTEXITCODE -ne 0){throw 'Working tree differs from staged source.'}
Write-Host 'Phase 4.3.1 compiler correction installed and staged. OCR logic and existing indexes unchanged.' -ForegroundColor Green
& git -C $RepoRoot diff --cached --stat
