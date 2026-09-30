# SPDX-License-Identifier: AGPL-3.0-only
# Original, code-drawn Bitcoin symbol. No PayDance brand assets are used.
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 64,64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$orange = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#E9812D'))
$graphics.FillEllipse($orange,1,1,62,62)
$font = New-Object System.Drawing.Font 'Segoe UI',39,([System.Drawing.FontStyle]::Bold),([System.Drawing.GraphicsUnit]::Pixel)
$format = New-Object System.Drawing.StringFormat
$format.Alignment = [System.Drawing.StringAlignment]::Center
$format.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.DrawString([string][char]0x20BF,$font,[System.Drawing.Brushes]::White,(New-Object System.Drawing.RectangleF 0,-2,64,64),$format)
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream = [System.IO.File]::Create((Join-Path $PSScriptRoot 'app.ico'))
$icon.Save($stream)
$stream.Dispose(); $icon.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $font.Dispose(); $format.Dispose(); $orange.Dispose()
