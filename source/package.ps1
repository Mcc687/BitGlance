# SPDX-License-Identifier: AGPL-3.0-only
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoDir 'artifacts\release' }
$releaseDir = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
# Each invocation uses a fresh staging directory; it never removes user folders.
$stageDir = Join-Path $repoDir ('artifacts\package\' + [Guid]::NewGuid().ToString('N'))
$binaryDir = Join-Path $stageDir 'build'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $binaryDir
$exe = Join-Path $binaryDir 'BitGlance.exe'
$version = [Version]([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion)
$versionText = $version.ToString(3)
[xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'app.manifest') -Raw
if ($manifest.assembly.assemblyIdentity.version -ne $version.ToString()) { throw 'Manifest and executable versions differ.' }
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne ('v' + $versionText)) { throw 'Git tag and executable versions differ.' }

$report = Join-Path $stageDir 'logic-checks.txt'
$run = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
if ($run.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw 'Logic checks failed.' }
Get-Content -LiteralPath $report | Write-Host

$payload = Join-Path $stageDir 'BitGlance'
New-Item -ItemType Directory -Force -Path $payload | Out-Null
foreach ($name in @('BitGlance.exe','BitGlance.exe.config','BitGlance.exe.sha256')) {
    Copy-Item -LiteralPath (Join-Path $binaryDir $name) -Destination $payload
}
foreach ($name in @('README.md','LICENSE','CHANGELOG.md','THIRD_PARTY_NOTICES.md','CONTRIBUTING.md','使用说明.md','验证记录.md')) {
    Copy-Item -LiteralPath (Join-Path $repoDir $name) -Destination $payload
}
# Copy only source, resources, documentation and notices, never local settings.
foreach ($folder in @('source','legal','docs')) {
    $base = Join-Path $repoDir $folder
    $allowed = if ($folder -eq 'source') { @('.cs','.xaml','.ps1','.ico','.manifest','.config') } else { @('.md','.png') }
    foreach ($file in Get-ChildItem -LiteralPath $base -Recurse -File) {
        if ($file.Extension -notin $allowed) { continue }
        $relative = $file.FullName.Substring($base.Length).TrimStart('\','/')
        $destination = Join-Path (Join-Path $payload $folder) $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zipPath = Join-Path $releaseDir ('BitGlance-v' + $versionText + '-Windows.zip')
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }
# Older Windows PowerShell hosts otherwise emit backslashes in ZIP entry names.
$archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $payload -Recurse -File) {
        $relative = $file.FullName.Substring($payload.Length).TrimStart('\','/').Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, ('BitGlance/' + $relative), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$sha = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zipPath + '.sha256'), $sha + '  ' + [IO.Path]::GetFileName($zipPath) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $report -Destination (Join-Path $releaseDir 'logic-checks.txt') -Force
Write-Host ('Packaged: ' + $zipPath)
