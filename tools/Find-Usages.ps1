# Lists every game method whose IL references a member (method/field/property accessor) matching a regex.
# Example:
#   powershell -File Find-Usages.ps1 -Managed "<game>\Rise of Industry_Data\Managed" -Pattern 'useFullVehicles|waitTillVehicleFull'
param([string]$Managed, [string]$Pattern)
$loaded = @{}
foreach ($f in Get-ChildItem $Managed -Filter *.dll) {
    if ($f.BaseName -match '^(mscorlib|System|System\..*|Mono\..*|netstandard)$') { continue }
    try { $loaded[$f.BaseName] = [Reflection.Assembly]::LoadFile($f.FullName) } catch {}
}
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($s,$e) $n=(New-Object Reflection.AssemblyName($e.Name)).Name; if($loaded.ContainsKey($n)){ return $loaded[$n] }; $null })
$asm = $loaded['Assembly-CSharp']
try { $types = $asm.GetTypes() } catch { $types = $_.Exception.Types | Where-Object { $_ } }
$ops = @{}
foreach ($fi in [Reflection.Emit.OpCodes].GetFields()) { $op = $fi.GetValue($null); $ops[[int][BitConverter]::ToUInt16([BitConverter]::GetBytes([int16]$op.Value),0)] = $op }
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
$rx = New-Object Text.RegularExpressions.Regex($Pattern)
foreach ($t in $types) {
    foreach ($m in @($t.GetMethods($flags)) + @($t.GetConstructors($flags))) {
        $body = try { $m.GetMethodBody() } catch { $null }; if (-not $body) { continue }
        $il = $body.GetILAsByteArray(); $i = 0; $hits = @()
        $tArgs = if ($t.IsGenericType) { $t.GetGenericArguments() } else { $null }
        $mArgs = if ($m.IsGenericMethod) { $m.GetGenericArguments() } else { $null }
        while ($i -lt $il.Length) {
            $code = [int]$il[$i]; $i++
            if ($code -eq 0xFE) { $code = 0xFE00 -bor $il[$i]; $i++ }
            $op = $ops[$code]; if (-not $op) { break }
            $sz = switch ($op.OperandType) { 'InlineNone' {0} 'ShortInlineBrTarget' {1} 'ShortInlineI' {1} 'ShortInlineVar' {1} 'InlineVar' {2} 'InlineI8' {8} 'InlineR' {8} 'ShortInlineR' {4} 'InlineSwitch' { 4 + 4*[BitConverter]::ToInt32($il,$i) } default {4} }
            if ($op.OperandType -in 'InlineMethod','InlineField') {
                try {
                    $r = $m.Module.ResolveMember([BitConverter]::ToInt32($il, $i), $tArgs, $mArgs)
                    if ($rx.IsMatch($r.Name)) { $hits += ('{0} {1}.{2}' -f $op.Name, $r.DeclaringType.Name, $r.Name) }
                } catch {}
            }
            $i += $sz
        }
        if ($hits) { '{0}::{1}  <- {2}' -f $t.FullName, $m.Name, (($hits | Sort-Object -Unique) -join '; ') }
    }
}
