$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/ThreadBack.App/Assets'
$sourceImage = [System.Drawing.Image]::FromFile((Join-Path $assetDir 'ThreadBack.png'))
$iconSizes = @(16,20,24,32,40,48,64,128,256)
$frames = @()
try {
    foreach ($size in $iconSizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size,$size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $memory = [System.IO.MemoryStream]::new()
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($sourceImage,0,0,$size,$size)
            $bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$memory.ToArray()
        } finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $sourceImage.Dispose() }
$writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create((Join-Path $assetDir 'ThreadBack.ico')))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$iconSizes.Count)
    $offset = 6 + 16 * $iconSizes.Count
    for ($i=0; $i -lt $iconSizes.Count; $i++) {
        $edge = if ($iconSizes[$i] -eq 256) { 0 } else { $iconSizes[$i] }
        $writer.Write([byte]$edge); $writer.Write([byte]$edge); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
