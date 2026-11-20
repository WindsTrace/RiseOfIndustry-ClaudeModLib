# Builds the release zips into <repo>\dist (not committed; upload them to a GitHub Release):
#   ClaudeModLib-v<ver>-toolkit.zip    toolkit only (default download)
#   ClaudeModLib-v<ver>-with-mods.zip  toolkit + CityProgress + StarterChain
# Usage: powershell -ExecutionPolicy Bypass -File package.ps1
$ErrorActionPreference = 'Stop'
$Repo = Split-Path $PSScriptRoot -Parent
$Dist = Join-Path $Repo 'dist'

# Build everything first (also installs into the local game; close the game before running).
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -Mods 'ClaudeModLib,CityProgress,StarterChain'
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

$ver = (Get-Content (Join-Path $Repo 'ClaudeModLib\desc.json') -Raw | ConvertFrom-Json).versionString
$sources = @{
    'ClaudeModLib' = Join-Path $Repo 'ClaudeModLib'
    'CityProgress' = Join-Path $Repo 'CityProgress'
    'StarterChain' = Join-Path $Repo 'RoIMods\StarterChain'
}

$install = @"
Rise of Industry - ClaudeModLib $ver
====================================
Install / 安装:
  Copy every folder in this zip into
  把压缩包里的每个文件夹复制到
    <Steam>\steamapps\common\RiseOfIndustry\Mods\
  (create the Mods folder if it does not exist / 没有 Mods 文件夹就新建一个)

Use / 使用:
  In a game, click the gear button in the top-right toolbar or press F9 (rebindable in key settings).
  进入对局后点右上角工具栏的齿轮按钮，或按 F9（可在按键设置中修改）。
  Language: Auto (follows the game) / English / 简体中文 - switch it in the panel.
  语言：自动（跟随游戏）/ English / 简体中文，可在面板中切换。

Note / 注意: enabling mods disables Steam achievements (game rule). 启用 mod 会禁用成就（游戏规则）。
Source, docs, API: https://github.com/WindsTrace/RiseOfIndustry-ClaudeModLib
Written by Claude (Anthropic); ideas and testing by WindsTrace.
"@

function Make-Zip([string]$name, [string[]]$mods) {
    $stage = Join-Path $Dist "_stage_$name"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -Confirm:$false }
    foreach ($m in $mods) {
        $src = $sources[$m]; $dst = Join-Path $stage $m
        New-Item -ItemType Directory -Force $dst | Out-Null
        Copy-Item (Join-Path $src 'desc.json') $dst
        foreach ($d in 'code', 'assets', 'content', 'config') {
            if (-not (Test-Path (Join-Path $src $d))) { if ($d -ne 'config') { New-Item -ItemType Directory -Force (Join-Path $dst $d) | Out-Null }; continue }
            New-Item -ItemType Directory -Force (Join-Path $dst $d) | Out-Null
            Copy-Item (Join-Path $src "$d\*") (Join-Path $dst $d) -Recurse
        }
    }
    Set-Content (Join-Path $stage 'INSTALL.txt') $install -Encoding UTF8
    $zip = Join-Path $Dist $name
    if (Test-Path $zip) { Remove-Item $zip -Force -Confirm:$false }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    Remove-Item $stage -Recurse -Force -Confirm:$false
    "{0}  ({1:N0} KB): {2}" -f $name, ((Get-Item $zip).Length / 1KB), ($mods -join ', ')
}

New-Item -ItemType Directory -Force $Dist | Out-Null
Make-Zip "ClaudeModLib-v$ver-toolkit.zip" @('ClaudeModLib')   # "toolkit" sorts before "with-mods" on the release page
Make-Zip "ClaudeModLib-v$ver-with-mods.zip" @('ClaudeModLib', 'CityProgress', 'StarterChain')
