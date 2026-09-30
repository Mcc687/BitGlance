# SPDX-License-Identifier: AGPL-3.0-only
$ErrorActionPreference = 'Stop'
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkDir 'csc.exe'
$sourceDir = $PSScriptRoot
$outputPath = Join-Path (Split-Path $sourceDir -Parent) 'BitGlance.exe'
$refs = @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir $_) }
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir ('WPF\' + $_)) }
$sourceFiles = Get-ChildItem -LiteralPath $sourceDir -Filter '*.cs' | Select-Object -ExpandProperty FullName
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',('/out:' + $outputPath),('/win32manifest:' + (Join-Path $sourceDir 'app.manifest')),('/win32icon:' + (Join-Path $sourceDir 'app.ico')),('/resource:' + (Join-Path $sourceDir 'MainWindow.xaml') + ',MainWindow.xaml'),('/resource:' + (Join-Path $sourceDir 'app.ico') + ',app.ico'))
& $compilerPath @arguments @refs @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host ('Built: ' + $outputPath)
