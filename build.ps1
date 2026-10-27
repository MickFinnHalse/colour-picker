# Builds Colour Picker with the C# compiler that ships with Windows.
# Nothing to install: no SDK, no NuGet, no .NET download.
#
#   build.ps1                 builds into C:\Tools\ColourPicker and creates the shortcuts
#   build.ps1 -OutDir <path>  builds somewhere else
#   build.ps1 -Package        builds the downloads for other people into C:\Tools\_dev\ColourPicker-dist
#                             (or -DistDir <path>): ColourPickerSetup.exe, with the app inside it,
#                             and ColourPicker-<version>-portable.zip. No shortcuts are touched.
#
# CHANGES ON DISK: writes (overwrites) ColourPicker.exe and ColourPicker.ico in the output
#                  folder, and creates or overwrites "Colour Picker.lnk" on the Desktop and
#                  in the Start menu. With -Package, writes only to the dist folder.
#                  It never starts the app or the installer.
#
# Why C:\Tools: it is local disk, and it is the folder AVG has an exception for. Every
# rebuild is a brand-new executable to an antivirus, and AVG flags new executables it
# finds anywhere else. (AVG refuses exceptions under AppData.)

param(
    [string]$OutDir = 'C:\Tools\ColourPicker',
    [switch]$Package,
    [string]$DistDir = 'C:\Tools\_dev\ColourPicker-dist'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if ($Package) { $OutDir = $DistDir }
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe  = Join-Path $OutDir 'ColourPicker.exe'
$ico  = Join-Path $OutDir 'ColourPicker.ico'

if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }
# A running exe cannot be overwritten, so stop before writing anything.
if (-not $Package -and (Get-Process ColourPicker -ErrorAction SilentlyContinue)) { throw 'Close Colour Picker first, then run build.ps1 again.' }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

function Get-HsvColor([double]$h, [double]$s, [double]$v) {
    $c = $v * $s; $x = $c * (1 - [Math]::Abs((($h / 60) % 2) - 1)); $m = $v - $c
    switch ([int][Math]::Floor($h / 60) % 6) {
        0 { $r = $c; $g = $x; $b = 0 }  1 { $r = $x; $g = $c; $b = 0 }
        2 { $r = 0; $g = $c; $b = $x }  3 { $r = 0; $g = $x; $b = $c }
        4 { $r = $x; $g = 0; $b = $c }  default { $r = $c; $g = 0; $b = $x }
    }
    [System.Drawing.Color]::FromArgb([int](($r + $m) * 255), [int](($g + $m) * 255), [int](($b + $m) * 255))
}

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = 'AntiAlias'
        $g.Clear([System.Drawing.Color]::Transparent)

        # Dark rounded tile
        $pad = [single][Math]::Max(0.5, $size * 0.03)
        $w = [single]($size - 2 * $pad)
        $d = [single]($w * 0.44)
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc($pad, $pad, $d, $d, 180, 90)
        $path.AddArc([single]($pad + $w - $d), $pad, $d, $d, 270, 90)
        $path.AddArc([single]($pad + $w - $d), [single]($pad + $w - $d), $d, $d, 0, 90)
        $path.AddArc($pad, [single]($pad + $w - $d), $d, $d, 90, 90)
        $path.CloseFigure()
        $tile = [System.Drawing.Color]::FromArgb(255, 30, 34, 44)
        $tileBrush = New-Object System.Drawing.SolidBrush $tile
        $g.FillPath($tileBrush, $path)

        # Colour wheel: 12 hue slices
        $inset = [single]($size * 0.16)
        $wd = [single]($size - 2 * $inset)
        for ($i = 0; $i -lt 12; $i++) {
            $brush = New-Object System.Drawing.SolidBrush (Get-HsvColor ($i * 30) 0.85 1)
            $g.FillPie($brush, $inset, $inset, $wd, $wd, [single]($i * 30 - 105), [single]31)
            $brush.Dispose()
        }

        # Hole in the middle with a white ring, like a picker handle
        $hole = [single]($wd * 0.42)
        $hx = [single](($size - $hole) / 2)
        $g.FillEllipse($tileBrush, $hx, $hx, $hole, $hole)
        $ring = [single]($hole * 0.52)
        $rx = [single](($size - $ring) / 2)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single][Math]::Max(1, $size * 0.045))
        $g.DrawEllipse($pen, $rx, $rx, $ring, $ring)
        $pen.Dispose(); $tileBrush.Dispose(); $path.Dispose()
    }
    finally { $g.Dispose() }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

# --- Icon (.ico with PNG frames) ---
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$frames = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($s in $sizes) { $frames.Add((New-IconPng $s)) }
$fs = [System.IO.File]::Create($ico)
try {
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$frames[$i].Length); $bw.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($f in $frames) { $bw.Write($f) }
    $bw.Flush()
}
finally { $fs.Dispose() }
Write-Output "Icon written: $ico"

# --- Compile ---
$sources = Get-ChildItem -Path (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
if (-not $sources) { throw "No .cs files found in $root\src" }
& $csc /nologo /target:winexe /optimize+ "/out:$exe" "/win32icon:$ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Compile failed (exit code $LASTEXITCODE)" }
Write-Output "Compiled: $exe"

if ($Package) {
    # --- Installer: setup\Setup.cs with the app embedded as a resource ---
    $setup = Join-Path $OutDir 'ColourPickerSetup.exe'
    & $csc /nologo /target:winexe /optimize+ "/out:$setup" "/win32icon:$ico" "/resource:$exe,ColourPicker.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $root 'setup\Setup.cs')
    if ($LASTEXITCODE -ne 0) { throw "Installer compile failed (exit code $LASTEXITCODE)" }
    Write-Output "Installer: $setup"

    # --- Portable zip: just the app; it keeps its settings next to itself ---
    $version = (Get-Item $exe).VersionInfo.FileVersion -replace '\.0$', ''
    $zip = Join-Path $OutDir "ColourPicker-$version-portable.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path $exe -DestinationPath $zip
    Write-Output "Portable zip: $zip"
    return
}

# --- Shortcuts ---
$shell = New-Object -ComObject WScript.Shell
$targets = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Colour Picker.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'Colour Picker.lnk')
)
foreach ($t in $targets) {
    $lnk = $shell.CreateShortcut($t)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $OutDir
    $lnk.IconLocation = "$exe,0"
    $lnk.Description = 'Pick a colour and copy its hex code'
    $lnk.Save()
    Write-Output "Shortcut: $t"
}
