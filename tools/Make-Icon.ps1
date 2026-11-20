# Generates ClaudeModLib/assets/claudemodlib.svg: a thin outline gear (matching the game's line icons)
# with a Claude-style starburst in the middle. Outline = outer contour + reversed inner contour (a hole under
# both nonzero and evenodd fill rules), so no SVG stroke support is needed.
param([string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent) 'ClaudeModLib\assets\claudemodlib.svg'))
$inv = [Globalization.CultureInfo]::InvariantCulture
function P([double]$r, [double]$deg) {
    $a = $deg * [Math]::PI / 180
    '{0},{1}' -f ([Math]::Round(32 + $r * [Math]::Sin($a), 2)).ToString($inv), ([Math]::Round(32 - $r * [Math]::Cos($a), 2)).ToString($inv)
}
function GearPoints([double]$scale) {
    $tip = 29.5 * $scale; $root = 24 * $scale; $pts = @()
    for ($i = 0; $i -lt 8; $i++) {
        $c = $i * 45
        $pts += P $root ($c - 22.5); $pts += P $root ($c - 13)
        $pts += P $tip ($c - 8.5);   $pts += P $tip ($c + 8.5)
        $pts += P $root ($c + 13)
    }
    return , $pts
}
$outer = GearPoints 1.0
$inner = GearPoints 0.885
[array]::Reverse($inner)
$gear = 'M' + ($outer -join ' L') + ' Z M' + ($inner -join ' L') + ' Z'

# Starburst: 10 tapered spokes of slightly varying length and angle, like the Claude mark.
$spokes = @()
$lengths = 16, 13, 15.5, 12.5, 15, 13.5, 16, 12.5, 15, 13
$jitter = 0, 4, -3, 2, -2, 3, 0, -4, 2, -2
for ($i = 0; $i -lt 10; $i++) {
    $a = $i * 36 + $jitter[$i]; $len = $lengths[$i]
    # narrow at the centre, widening towards a blunt tip (half-width ~1.7 units at the end)
    $half = [Math]::Atan(1.7 / $len) * 180 / [Math]::PI
    $spokes += 'M' + (P 1.0 ($a - 40)) + ' L' + (P ($len - 1.2) ($a - $half)) + ' L' + (P $len ($a - $half * 0.45)) +
               ' L' + (P $len ($a + $half * 0.45)) + ' L' + (P ($len - 1.2) ($a + $half)) + ' L' + (P 1.0 ($a + 40)) + ' Z'
}

$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
  <path fill="#FFFFFF" fill-rule="evenodd" d="$gear"/>
  <path fill="#FFFFFF" d="$($spokes -join ' ')"/>
</svg>
"@
[IO.File]::WriteAllText($Out, $svg, (New-Object Text.UTF8Encoding $false))
"written $Out"
