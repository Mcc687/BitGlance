# SPDX-License-Identifier: AGPL-3.0-only
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkDir 'csc.exe'
$sourceDir = $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path (Split-Path $sourceDir -Parent) 'artifacts\build' }
$buildDir = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
$outputPath = Join-Path $buildDir 'BitGlance.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework compiler was not found. Build on Windows with .NET Framework 4.8 installed.' }
$refs = @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir $_) }
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir ('WPF\' + $_)) }
$sourceFiles = Get-ChildItem -LiteralPath $sourceDir -Filter '*.cs' | Select-Object -ExpandProperty FullName
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',('/out:' + $outputPath),('/win32manifest:' + (Join-Path $sourceDir 'app.manifest')),('/win32icon:' + (Join-Path $sourceDir 'app.ico')),('/resource:' + (Join-Path $sourceDir 'MainWindow.xaml') + ',MainWindow.xaml'),('/resource:' + (Join-Path $sourceDir 'app.ico') + ',app.ico'))
& $compilerPath @arguments @refs @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $sourceDir 'App.config') -Destination ($outputPath + '.config') -Force
$sha = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($outputPath + '.sha256'), $sha + '  BitGlance.exe' + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Host ('Built: ' + $outputPath)
