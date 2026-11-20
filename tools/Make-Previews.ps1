# Draws the Steam Workshop preview images (512x512 PNG) in the native theme palette into <repo>\dist\workshop-preview\.
#   ClaudeModLib.png  - gear + starburst icon (from ClaudeModLib\assets\claudemodlib.svg)
#   CityProgress.png  - mock of the city cards
param([string]$Repo = (Split-Path $PSScriptRoot -Parent))
Add-Type -AssemblyName System.Drawing
$out = Join-Path $Repo 'dist\workshop-preview'
New-Item -ItemType Directory -Force $out | Out-Null
$inv = [Globalization.CultureInfo]::InvariantCulture

function Col([string]$hex) { [Drawing.ColorTranslator]::FromHtml($hex) }
$cream = Col '#F1EBDC'; $card = Col '#F7F2E7'; $orange = Col '#C87A06'; $brown = Col '#3A2B20'
$text = Col '#4A4036'; $sub = Col '#86837B'; $grey = Col '#9F9C91'

function Font([string[]]$names, [single]$size) {
    $families = (New-Object Drawing.Text.InstalledFontCollection).Families | ForEach-Object Name
    foreach ($n in $names) { if ($families -contains $n) { return New-Object Drawing.Font($n, $size, [Drawing.GraphicsUnit]::Pixel) } }
    return New-Object Drawing.Font('Arial', $size, [Drawing.GraphicsUnit]::Pixel)
}
$fBold = { param($s) Font @('Bahnschrift SemiBold SemiConden', 'Bahnschrift SemiBold', 'Segoe UI Semibold') $s }
$fBody = { param($s) Font @('Bahnschrift SemiCondensed', 'Bahnschrift', 'Segoe UI') $s }
$fZh   = { param($s) Font @('Microsoft YaHei UI', 'Microsoft YaHei') $s }

function New-Canvas { $b = New-Object Drawing.Bitmap 512, 512; $g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.Clear($cream); return $b, $g }
function Brush($c) { New-Object Drawing.SolidBrush $c }
function Header($g, [string]$title, [string]$subtitle) {
    $g.FillRectangle((Brush $brown), 0, 0, 512, 96)
    $g.DrawString($title, (& $fBold 44), (Brush $cream), 26, 10)
    $g.DrawString($subtitle, (& $fBody 22), (Brush (Col '#E3C089')), 28, 60)
    $g.FillRectangle((Brush $orange), 0, 96, 512, 8)
}
function Fill-SvgPaths($g, [string]$svgFile, [single]$x, [single]$y, [single]$scale, $color) {
    $svg = Get-Content $svgFile -Raw
    $st = $g.Save(); $g.TranslateTransform($x, $y); $g.ScaleTransform($scale, $scale)
    foreach ($m in [regex]::Matches($svg, 'd="([^"]+)"')) {
        $gp = New-Object Drawing.Drawing2D.GraphicsPath; $gp.FillMode = 'Alternate'
        foreach ($sub in ($m.Groups[1].Value -split 'Z' | Where-Object { $_.Trim() })) {
            $pts = [regex]::Matches($sub, '(-?[\d.]+),(-?[\d.]+)') | ForEach-Object { New-Object Drawing.PointF ([single]::Parse($_.Groups[1].Value, $inv)), ([single]::Parse($_.Groups[2].Value, $inv)) }
            $gp.AddPolygon([Drawing.PointF[]]$pts)
        }
        $g.FillPath((Brush $color), $gp)
    }
    $g.Restore($st)
}
function Rounded([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2*$r, 2*$r, 180, 90); $p.AddArc($x+$w-2*$r, $y, 2*$r, 2*$r, 270, 90)
    $p.AddArc($x+$w-2*$r, $y+$h-2*$r, 2*$r, 2*$r, 0, 90); $p.AddArc($x, $y+$h-2*$r, 2*$r, 2*$r, 90, 90); $p.CloseFigure(); return $p
}

# --- ClaudeModLib -----------------------------------------------------------------------------
$b, $g = New-Canvas
Header $g 'ClaudeModLib' 'Rise of Industry modding toolkit'
$g.FillPath((Brush $orange), (Rounded 136 136 240 240 28))
# icon is 64x64 scaled 3.5 = 224 px; centre it in the 240 px square at 136..376 -> offset 256 - 112 = 144
Fill-SvgPaths $g (Join-Path $Repo 'ClaudeModLib\assets\claudemodlib.svg') 144 144 3.5 $cream
$g.DrawString('Mod panel · Themes · Hotkeys · API', (& $fBody 24), (Brush $text), 256, 408, (New-Object Drawing.StringFormat -Property @{ Alignment = 'Center' }))
$g.DrawString('English / 简体中文', (& $fZh 22), (Brush $sub), 256, 446, (New-Object Drawing.StringFormat -Property @{ Alignment = 'Center' }))
$g.Dispose(); $b.Save((Join-Path $out 'ClaudeModLib.png'), [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()

# --- CityProgress: replica of the in-game window (English UI, CityProgress 0.4.0) ---------------
# Values are illustrative; texts and layout match CityProgressMod.cs (title bar + ×, section bar, cards).
$b, $g = New-Canvas
$fmtR = New-Object Drawing.StringFormat -Property @{ Alignment = 'Far' }
$g.FillRectangle((Brush $brown), 0, 0, 512, 48)
$g.DrawString('City progress', (& $fBold 26), (Brush $cream), 16, 9)
$g.DrawString('×', (& $fBold 30), (Brush $orange), 496, 4, $fmtR)
$g.FillRectangle((Brush $orange), 12, 60, 488, 36)
$g.DrawString('All cities  (3)', (& $fBold 22), (Brush $cream), 22, 65)
$g.DrawString('Population settles every 15 days as shops consume goods.', (& $fBody 15), (Brush $sub), 14, 100)

$cards = @(
    @{ name = 'LORSBECK';   tier = 'Hamlet → Village'; pop = '91,426';  p = 0.621; st = 'Growing';                     sc = '#4E8A2E'; next = 'Next tier at 147.3k  ·  55.9k to go  ·  62.1%' },
    @{ name = 'MOOSEURACH'; tier = 'Village → Town';   pop = '238,450'; p = 0.579; st = 'Stagnant (not enough goods)'; sc = '#C87A06'; next = 'Next tier at 301.2k  ·  62.8k to go  ·  57.9%' },
    @{ name = 'DUISBURG';   tier = 'Town → City';      pop = '512,400'; p = 1.0;   st = 'At limit · needs a sponsor (region permit)'; sc = '#B3412E'; next = 'Next tier at 512.4k  ·  0 to go  ·  100.0%' }
)
$y = 128; $x = 12; $w = 488; $h = 120
foreach ($c in $cards) {
    $g.FillRectangle((Brush $card), $x, $y, $w, $h)
    $g.DrawRectangle((New-Object Drawing.Pen($orange, 2)), $x, $y, $w, $h)
    $g.DrawString($c.name, (& $fBold 21), (Brush $text), ($x + 12), ($y + 6))
    $g.DrawString($c.tier, (& $fBody 15), (Brush $sub), ($x + $w - 12), ($y + 10), $fmtR)
    $g.DrawString('Population', (& $fBody 14), (Brush $sub), ($x + 12), ($y + 34))
    $g.DrawString('Status', (& $fBody 14), (Brush $sub), ($x + 250), ($y + 34))
    $g.DrawString($c.pop, (& $fBold 30), (Brush $orange), ($x + 10), ($y + 48))
    $g.DrawString($c.st, (& $fBody 16), (Brush (Col $c.sc)), (New-Object Drawing.RectangleF(($x + 250), ($y + 49), ($w - 262), 38)))
    $g.FillRectangle((Brush $grey), ($x + 12), ($y + 90), ($w - 24), 8)
    $g.FillRectangle((Brush (Col $c.sc)), ($x + 12), ($y + 90), [int](($w - 24) * $c.p), 8)
    $g.DrawString($c.next, (& $fBody 13), (Brush $sub), ($x + 12), ($y + 100))
    $y += $h + 8
}
$g.Dispose(); $b.Save((Join-Path $out 'CityProgress.png'), [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()

Get-ChildItem $out -Filter *.png | ForEach-Object { "{0}  {1:N0} KB" -f $_.FullName, ($_.Length / 1KB) }
