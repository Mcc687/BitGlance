# SPDX-License-Identifier: AGPL-3.0-only
param([Parameter(Mandatory=$true)][string]$WorkDirectory)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null
$testDir = (Resolve-Path -LiteralPath $WorkDirectory).Path
$sourceDir = Split-Path $PSScriptRoot -Parent
$packageDir = Split-Path $sourceDir -Parent
$fwDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
Copy-Item -LiteralPath (Join-Path $packageDir 'BitGlance.exe') -Destination $testDir -Force
Copy-Item -LiteralPath (Join-Path $packageDir 'BitGlance.exe.config') -Destination $testDir -Force
$refs = @('System.Xaml.dll','System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $fwDir $_) }
$testExe = Join-Path $testDir 'DesktopChecks.exe'
$argsList = @('/nologo','/target:winexe','/codepage:65001',('/out:' + $testExe),('/reference:' + (Join-Path $testDir 'BitGlance.exe')))
& (Join-Path $fwDir 'csc.exe') @argsList @refs (Join-Path $PSScriptRoot 'DesktopChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
$run = Start-Process -FilePath $testExe -ArgumentList ('"' + $testDir + '"') -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $testDir 'desktop-checks.txt')
if ($run.ExitCode -ne 0) { throw 'Desktop checks failed.' }
$probeExe = Join-Path $testDir 'HitProbe.exe'
& (Join-Path $fwDir 'csc.exe') /nologo /target:winexe /codepage:65001 ('/out:' + $probeExe) ('/reference:' + (Join-Path $testDir 'BitGlance.exe')) @refs (Join-Path $PSScriptRoot 'HitProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Hit probe build failed.' }
$probe = Start-Process -FilePath $probeExe -ArgumentList ('"' + $testDir + '"') -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $testDir 'hit-probe.txt')
if ($probe.ExitCode -ne 0) { throw 'Native hit testing failed.' }
