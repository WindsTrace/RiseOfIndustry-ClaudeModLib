# Prints the calls, field accesses and string literals inside game methods (a tiny IL reader) - enough to
# understand load order, formulas and which data a method uses, without a decompiler.
# Example:
#   powershell -File Show-ILCalls.ps1 -Managed "<game>\Rise of Industry_Data\Managed" -Spec "ProjectAutomata.ModLoader::LoadMod;ProjectAutomata.Shop::GetValidProducts"
# Nested types: "Outer+Inner::Method". Generic types: "Name``1::Method".
param([string]$Managed, [string]$Spec)  # Spec: "Type::Method;Type::Method"
$loaded = @{}
foreach ($f in Get-ChildItem $Managed -Filter *.dll) {
    if ($f.BaseName -match '^(mscorlib|System|System\..*|Mono\..*|netstandard)$') { continue }
    try { $loaded[$f.BaseName] = [Reflection.Assembly]::LoadFile($f.FullName) } catch {}
}
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($s,$e) $n=(New-Object Reflection.AssemblyName($e.Name)).Name; if($loaded.ContainsKey($n)){ return $loaded[$n] }; $null })
$asm = $loaded['Assembly-CSharp']
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
# opcode table
$ops = @{}
foreach ($fi in [Reflection.Emit.OpCodes].GetFields()) { $op = $fi.GetValue($null); $ops[[int][BitConverter]::ToUInt16([BitConverter]::GetBytes([int16]$op.Value),0)] = $op }
foreach ($item in $Spec.Split(';')) {
    $tn, $mn = $item.Split(@('::'), [StringSplitOptions]::None)
    $t = $asm.GetType($tn)
    if (-not $t) { "### type $tn not found"; continue }
    foreach ($m in @($t.GetMethods($flags)) + @($t.GetConstructors($flags)) | Where-Object { $_.Name -eq $mn }) {
        "### $tn::$mn(" + (($m.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ',') + ")"
        $body = $m.GetMethodBody(); if (-not $body) { "  (no body)"; continue }
        $il = $body.GetILAsByteArray(); $i = 0
        while ($i -lt $il.Length) {
            $code = [int]$il[$i]; $i++
            if ($code -eq 0xFE) { $code = 0xFE00 -bor $il[$i]; $i++ }
            $op = $ops[$code]; if (-not $op) { "  ?? $code"; break }
            $sz = switch ($op.OperandType) { 'InlineNone' {0} 'ShortInlineBrTarget' {1} 'ShortInlineI' {1} 'ShortInlineVar' {1} 'InlineVar' {2} 'InlineI8' {8} 'InlineR' {8} 'ShortInlineR' {4} 'InlineSwitch' { 4 + 4*[BitConverter]::ToInt32($il,$i) } default {4} }
            if ($op.OperandType -in 'InlineMethod','InlineField','InlineString','InlineType','InlineTok') {
                $tok = [BitConverter]::ToInt32($il, $i)
                try {
                    switch ($op.OperandType) {
                        'InlineString' { "  {0} `"{1}`"" -f $op.Name, $m.Module.ResolveString($tok) }
                        'InlineMethod' { $r = $m.Module.ResolveMethod($tok, $(if($t.IsGenericType){$t.GetGenericArguments()}), $(if($m.IsGenericMethod){$m.GetGenericArguments()})); "  {0} {1}::{2}" -f $op.Name, $r.DeclaringType.Name, $r.Name }
                        'InlineField'  { $r = $m.Module.ResolveField($tok, $(if($t.IsGenericType){$t.GetGenericArguments()}), $(if($m.IsGenericMethod){$m.GetGenericArguments()})); "  {0} {1}::{2}" -f $op.Name, $r.DeclaringType.Name, $r.Name }
                        default { }
                    }
                } catch { "  {0} <tok {1:x}>" -f $op.Name, $tok }
            }
            $i += $sz
        }
    }
}
