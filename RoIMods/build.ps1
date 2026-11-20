# Builds and installs Rise of Industry mods from this folder.
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1 [-Mods StarterChain,AssetDump] [-NoInstall]
#   -NoInstall: compile only (the game may keep running; used by package.ps1)
param([string[]]$Mods = @('StarterChain'), [switch]$NoInstall)

$ErrorActionPreference = 'Stop'
$Mods = $Mods | ForEach-Object { $_ -split ',' } | Where-Object { $_ }
$Game    = 'C:\Program Files (x86)\Steam\steamapps\common\RiseOfIndustry'
$Managed = "$Game\Rise of Industry_Data\Managed"
$Csc     = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$Root    = $PSScriptRoot

$refs = 'mscorlib','System','System.Core','netstandard','Assembly-CSharp','Assembly-CSharp-firstpass',
        'UnityEngine','UnityEngine.CoreModule','UnityEngine.ParticleSystemModule','0Harmony',
        'UnityEngine.IMGUIModule','UnityEngine.TextRenderingModule','UnityEngine.UI','UnityEngine.UIModule','Unity.TextMeshPro','Newtonsoft.Json' |
        ForEach-Object { "$Managed\$_.dll" } | Where-Object { Test-Path $_ } | ForEach-Object { "/r:$_" }
$LibDll = Join-Path (Split-Path $Root -Parent) 'ClaudeModLib\code\ClaudeModLib.dll'

# Build the library first when it is in the list, so dependent mods compile against the fresh DLL.
$Mods = @($Mods | Where-Object { $_ -eq 'ClaudeModLib' }) + @($Mods | Where-Object { $_ -ne 'ClaudeModLib' })

foreach ($mod in $Mods) {
    # Mod source lives next to this script (RoIMods\<mod>) or as a sibling project (D:\ClaudeModding\<mod>).
    $src = Join-Path $Root $mod
    if (-not (Test-Path (Join-Path $src 'desc.json'))) { $src = Join-Path (Split-Path $Root -Parent) $mod }
    if (-not (Test-Path (Join-Path $src 'desc.json'))) { throw "mod source not found: $mod" }
    Write-Host "== $mod ($src)"

    # 1. Generate placeholder PNG icon (for .sprite) if missing.
    $png = Join-Path $src 'assets\toyrobot_tex.png'
    if ($mod -eq 'StarterChain' -and -not (Test-Path $png)) {
        Add-Type -AssemblyName System.Drawing
        $bmp = New-Object System.Drawing.Bitmap 128, 128
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = 'AntiAlias'
        $g.Clear([System.Drawing.Color]::FromArgb(255, 46, 125, 160))
        $w = [System.Drawing.Brushes]::White
        $d = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 40, 40, 40))
        $g.FillRectangle($w, 60, 8, 8, 16); $g.FillEllipse($w, 56, 2, 16, 12)
        $g.FillRectangle($w, 32, 24, 64, 40); $g.FillEllipse($d, 44, 34, 16, 16); $g.FillEllipse($d, 68, 34, 16, 16)
        $g.FillRectangle($w, 28, 68, 72, 40); $g.FillRectangle($w, 12, 72, 12, 28); $g.FillRectangle($w, 104, 72, 12, 28)
        $g.FillRectangle($w, 40, 110, 16, 16); $g.FillRectangle($w, 72, 110, 16, 16)
        $g.Dispose(); $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    }

    # 2. Compile src/*.cs -> code/<mod>.dll
    $cs = @(Get-ChildItem (Join-Path $src 'src') -Filter *.cs -ErrorAction SilentlyContinue)
    if ($cs.Count) {
        New-Item -ItemType Directory -Force (Join-Path $src 'code') | Out-Null
        $modRefs = @($refs)
        $desc = Get-Content (Join-Path $src 'desc.json') -Raw | ConvertFrom-Json
        if ($desc.dependencies -contains 'ClaudeModLib') {
            if (-not (Test-Path $LibDll)) { throw "$mod depends on ClaudeModLib; build ClaudeModLib first" }
            $modRefs += "/r:$LibDll"
        }
        & $Csc /nologo /noconfig /nostdlib+ /target:library /langversion:5 /optimize+ /doc:"$(Join-Path $src "code\$mod.xml")" /nowarn:1591 `
            "/out:$(Join-Path $src "code\$mod.dll")" $modRefs ($cs | ForEach-Object FullName)
        if ($LASTEXITCODE -ne 0) { throw "compile failed: $mod" }
    }

    if ($NoInstall) { continue }

    # 3. Install: copy desc.json, code, assets, content into <Game>\Mods\<mod>
    $dst = Join-Path $Game "Mods\$mod"
    # config\ holds user-editable files (e.g. theme.json): keep the installed copy across rebuilds.
    $keep = $null
    if (Test-Path (Join-Path $dst 'config')) {
        $keep = Join-Path $env:TEMP "roimod_config_$mod"
        if (Test-Path $keep) { Remove-Item $keep -Recurse -Force -Confirm:$false }
        Copy-Item (Join-Path $dst 'config') $keep -Recurse
    }
    if (Test-Path $dst) { Remove-Item $dst -Recurse -Force -Confirm:$false }
    foreach ($d in 'code', 'assets', 'content', 'config') {
        if ($d -eq 'config' -and -not (Test-Path (Join-Path $src $d)) -and -not $keep) { continue }
        New-Item -ItemType Directory -Force (Join-Path $dst $d) | Out-Null
        if (Test-Path (Join-Path $src $d)) { Copy-Item (Join-Path $src "$d\*") (Join-Path $dst $d) -Recurse -Force }
    }
    if ($keep) { Copy-Item (Join-Path $keep '*') (Join-Path $dst 'config') -Recurse -Force; Remove-Item $keep -Recurse -Force -Confirm:$false; Write-Host "  (kept installed config\)" }
    Copy-Item (Join-Path $src 'desc.json') $dst -Force
    Get-ChildItem $dst -Recurse -File | ForEach-Object { '  ' + $_.FullName.Substring($dst.Length + 1) }
}
