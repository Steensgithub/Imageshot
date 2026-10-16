# Pakker ImageShot til udgivelse:  installationsprogram (Inno Setup), bærbar zip og browserudvidelsens zip.
#   pwsh build/package.ps1 -Version 2.0.0
# Forudsætter, at Windows-programmet allerede er bygget i Release (dotnet build -c Release).
param(
  [Parameter(Mandatory = $true)][string]$Version,
  [string]$Out = "artifacts/release"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
New-Item -ItemType Directory -Force $Out | Out-Null
$bin = "windows/src/ImageShot/bin/Release/net48"
if (-not (Test-Path "$bin/ImageShot.exe")) { throw "Byg først: $bin/ImageShot.exe mangler" }

# 1) Inno Setup (findes ikke altid på GitHubs Windows-maskiner – installeres så via Chocolatey)
$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
  choco install innosetup -y --no-progress | Out-Host
  $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}
& $iscc "/DAppVersion=$Version" "/DSourceDir=$root\$($bin -replace '/','\')" "/O$root\$($Out -replace '/','\')" "installer\ImageShot.iss" | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Inno Setup fejlede ($LASTEXITCODE)" }

# 2) Bærbar udgave (kør direkte uden installation)
$portable = Join-Path $Out "ImageShot-$Version-portable.zip"
if (Test-Path $portable) { Remove-Item $portable }
$files = @("$bin/ImageShot.exe", "$bin/ImageShot.exe.config") + (Get-ChildItem "$bin/*.dll" | ForEach-Object { $_.FullName })
Compress-Archive -Path $files -DestinationPath $portable

# 3) Browserudvidelsen til Chrome Web Store / Edge Add-ons (uden tests og dokumentation)
$manifest = Get-Content extension/manifest.json -Raw | ConvertFrom-Json
if ($manifest.version -ne $Version) { Write-Warning "manifest.json har version $($manifest.version), forventede $Version" }
$stage = Join-Path ([IO.Path]::GetTempPath()) "imageshot-ext"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory $stage | Out-Null
Get-ChildItem extension -Exclude tests, '*.md' | Copy-Item -Destination $stage -Recurse
$extZip = Join-Path $Out "ImageShot-extension-$Version.zip"
if (Test-Path $extZip) { Remove-Item $extZip }
Compress-Archive -Path "$stage/*" -DestinationPath $extZip

Get-ChildItem $Out | Format-Table Name, Length | Out-Host
