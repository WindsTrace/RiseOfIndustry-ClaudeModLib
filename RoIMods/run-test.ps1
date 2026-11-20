# Restarts Rise of Industry (closing an instance that is already running), waits for the
# AssetDump output, then prints the mod-related lines of output_log.txt.
param([int]$TimeoutSec = 150, [string]$Build = 'StarterChain')
$dir = "$env:USERPROFILE\AppData\LocalLow\Dapper Penguin Studios\Rise of Industry"
$out = "$dir\roi_asset_dump.txt"
$log = "$dir\output_log.txt"

Get-Process | Where-Object { $_.Name -like 'Rise of Industry*' } | ForEach-Object {
    [void]$_.CloseMainWindow()
    if (-not $_.WaitForExit(20000)) { Stop-Process -Id $_.Id -Force -Confirm:$false }
}
Start-Sleep 3
# Build only after the game has exited: a running game locks code\*.dll.
if ($Build -and $Build -ne 'none') {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -Mods $Build
    if ($LASTEXITCODE -ne 0) { throw 'build failed' }
}
Remove-Item $out -Confirm:$false -ErrorAction SilentlyContinue
Start-Process 'steam://rungameid/671440'

$t = 0
while (-not (Test-Path $out) -and $t -lt $TimeoutSec) {
    Start-Sleep 3; $t += 3
    if ((Test-Path $log) -and (Select-String -Path $log -Pattern 'OnModError' -Quiet)) { break }
}
Start-Sleep 3
"dump written: $(Test-Path $out) after ${t}s"
$L = Get-Content $log
for ($i = 0; $i -lt $L.Count; $i++) {
    if ($L[$i] -match 'Loading mod|\[StarterChain\]|\[AssetDump\]|Asset not found|No loader|could not be found|Error parsing|is invalid|Exception') {
        $L[$i]
        if ($L[$i] -match 'Exception') { $L[($i + 1)..([Math]::Min($i + 8, $L.Count - 1))] | Where-Object { $_ -match '^\s+at ' } }
    }
}
