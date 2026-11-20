# Checks whether a (workshop) mod DLL still works with the installed game version: loads it against the
# current game assemblies and resolves every type/method/field token used in its IL. Anything listed under
# MISSING no longer exists in this game version -> that code path throws MissingMethodException at runtime.
# Example:
#   powershell -File Test-ModCompat.ps1 -Managed "<game>\Rise of Industry_Data\Managed" -ModDll "<workshop>\671440\<id>\code\X.dll"
param([string]$Managed, [string]$ModDll, [string[]]$ExtraDirs = @())
$loaded = @{}
foreach ($dir in @($Managed) + $ExtraDirs) {
    foreach ($f in Get-ChildItem $dir -Filter *.dll) {
        if ($f.BaseName -match '^(mscorlib|System|System\..*|Mono\..*|netstandard)$') { continue }
        if ($loaded.ContainsKey($f.BaseName)) { continue }
        try { $loaded[$f.BaseName] = [Reflection.Assembly]::LoadFile($f.FullName) } catch {}
    }
}
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($s,$e) $n=(New-Object Reflection.AssemblyName($e.Name)).Name; if($loaded.ContainsKey($n)){ return $loaded[$n] }; $null })
$asm = [Reflection.Assembly]::LoadFile($ModDll)

"### assembly: " + $asm.FullName
"### runtime: " + $asm.ImageRuntimeVersion
"### references:"; $asm.GetReferencedAssemblies() | ForEach-Object { "  " + $_.FullName }
try { $types = $asm.GetTypes() } catch [Reflection.ReflectionTypeLoadException] {
    "!!! type load errors:"; $_.Exception.LoaderExceptions | ForEach-Object { "  " + $_.Message } | Sort-Object -Unique
    $types = $_.Exception.Types | Where-Object { $_ }
}
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
"### types:"
foreach ($t in $types) {
    $base = try { $t.BaseType } catch { "<unresolvable: $($_.Exception.Message)>" }
    "  $($t.FullName) : $base"
    try { foreach ($a in $t.GetCustomAttributesData()) { "      [attr] " + $a.ToString() } } catch { "      [attr] <error $($_.Exception.Message)>" }
    try { foreach ($fld in $t.GetFields($flags)) { "      F {0} {1}" -f $fld.FieldType.Name, $fld.Name } } catch {}
    foreach ($m in @($t.GetMethods($flags)) + @($t.GetConstructors($flags))) {
        $ps = try { ($m.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ',' } catch { '?' }
        "      M {0}({1})" -f $m.Name, $ps
        try { foreach ($a in $m.GetCustomAttributesData()) { "          [attr] " + $a.ToString() } } catch { "          [attr] <error $($_.Exception.Message)>" }
    }
}

# opcode table
$ops = @{}
foreach ($fi in [Reflection.Emit.OpCodes].GetFields()) { $op = $fi.GetValue($null); $ops[[int][BitConverter]::ToUInt16([BitConverter]::GetBytes([int16]$op.Value),0)] = $op }

$ok = New-Object 'System.Collections.Generic.HashSet[string]'
$missing = New-Object 'System.Collections.Generic.HashSet[string]'
$strings = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($t in $types) {
    foreach ($m in @($t.GetMethods($flags)) + @($t.GetConstructors($flags))) {
        $body = try { $m.GetMethodBody() } catch { $null }; if (-not $body) { continue }
        $il = $body.GetILAsByteArray(); $i = 0
        $tArgs = if ($t.IsGenericType) { $t.GetGenericArguments() } else { $null }
        $mArgs = if ($m.IsGenericMethod) { $m.GetGenericArguments() } else { $null }
        while ($i -lt $il.Length) {
            $code = [int]$il[$i]; $i++
            if ($code -eq 0xFE) { $code = 0xFE00 -bor $il[$i]; $i++ }
            $op = $ops[$code]; if (-not $op) { break }
            $sz = switch ($op.OperandType) { 'InlineNone' {0} 'ShortInlineBrTarget' {1} 'ShortInlineI' {1} 'ShortInlineVar' {1} 'InlineVar' {2} 'InlineI8' {8} 'InlineR' {8} 'ShortInlineR' {4} 'InlineSwitch' { 4 + 4*[BitConverter]::ToInt32($il,$i) } default {4} }
            if ($op.OperandType -in 'InlineMethod','InlineField','InlineType','InlineTok','InlineString') {
                $tok = [BitConverter]::ToInt32($il, $i)
                if ($op.OperandType -eq 'InlineString') { [void]$strings.Add($m.Module.ResolveString($tok)) }
                else {
                    try {
                        $r = $m.Module.ResolveMember($tok, $tArgs, $mArgs)
                        $dt = if ($r -is [Type]) { $r.Assembly.GetName().Name } else { $r.DeclaringType.Assembly.GetName().Name }
                        if ($dt -match '^(Assembly-CSharp|Assembly-CSharp-firstpass|0Harmony|UnityEngine)') {
                            $name = if ($r -is [Type]) { "type " + $r.FullName } else { "{0}::{1}" -f $r.DeclaringType.FullName, $r.Name }
                            [void]$ok.Add("[$dt] $name")
                        }
                    } catch {
                        $msg = $_.Exception.InnerException; if (-not $msg) { $msg = $_.Exception }
                        [void]$missing.Add("in $($t.Name).$($m.Name): $($msg.GetType().Name): $($msg.Message)")
                    }
                }
            }
            $i += $sz
        }
    }
}
"### MISSING (does not resolve against current game):"
if ($missing.Count -eq 0) { "  (none)" } else { $missing | Sort-Object | ForEach-Object { "  " + $_ } }
"### game/Unity/Harmony APIs used (resolved OK):"
$ok | Sort-Object | ForEach-Object { "  " + $_ }
"### string literals:"
$strings | Sort-Object | ForEach-Object { "  `"$_`"" }
