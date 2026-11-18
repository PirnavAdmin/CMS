# Run from your backend project directory after backing up your existing reports.
# Stops if a destination file already exists to avoid accidental overwrites.
$source = Join-Path (Get-Location) 'wwwroot\uploads\lab-reports'
$target = Join-Path (Get-Location) 'private-data\lab-reports'
if (!(Test-Path $source)) { Write-Host 'No legacy lab reports folder found.'; exit }
New-Item -ItemType Directory -Path $target -Force | Out-Null
$files = Get-ChildItem -LiteralPath $source -File
foreach ($file in $files) {
    if ($file.Name -notmatch '^[a-fA-F0-9]{32}\.(pdf|jpg|jpeg|png)$') {
        Write-Warning "Review unrecognized filename manually: $($file.Name)"
        continue
    }
    $destination = Join-Path $target $file.Name
    if (Test-Path -LiteralPath $destination) {
        Write-Warning "Destination exists; not overwriting: $destination"
        continue
    }
    Move-Item -LiteralPath $file.FullName -Destination $destination -ErrorAction Stop
}
Write-Host 'Legacy report migration finished. Check for skipped files and remove any remaining publicly accessible copies.'
