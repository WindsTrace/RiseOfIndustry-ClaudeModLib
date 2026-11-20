# Lists fields/properties/methods of game types via reflection (works without a decompiler).
# Example:
#   powershell -File Dump-Types.ps1 -Managed "<game>\Rise of Industry_Data\Managed" -Types "ProjectAutomata.Shop,ProjectAutomata.Recipe"
#   powershell -File Dump-Types.ps1 -Managed "<...>\Managed" -Pattern '^ProjectAutomata\.Settlement'   # find types by regex
# Run in a separate powershell process: loaded assemblies stay locked until it exits.
param([string]$Managed, [string[]]$Types, [string]$Pattern)
$ErrorActionPreference = 'Continue'
$loaded = @{}
foreach ($f in Get-ChildItem $Managed -Filter *.dll) {
    if ($f.BaseName -match '^(mscorlib|System|System\..*|Mono\..*|netstandard)$') { continue }
    try { $loaded[$f.BaseName] = [Reflection.Assembly]::LoadFile($f.FullName) } catch {}
}
[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $e)
    $n = (New-Object Reflection.AssemblyName($e.Name)).Name
    if ($loaded.ContainsKey($n)) { return $loaded[$n] }
    return $null
})
$asm = $loaded['Assembly-CSharp']
try { $all = $asm.GetTypes() } catch [Reflection.ReflectionTypeLoadException] { $all = $_.Exception.Types | Where-Object { $_ } }
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
if ($Pattern) {
    "### types matching $Pattern"
    $all | Where-Object { $_.FullName -match $Pattern } | ForEach-Object { "{0}  : {1}" -f $_.FullName, $_.BaseType }
}
foreach ($tn in ($Types -join ',').Split(',')) {
    $t = $all | Where-Object { $_.FullName -eq $tn } | Select-Object -First 1
    if (-not $t) { "### $tn NOT FOUND"; continue }
    "### $($t.FullName) : $($t.BaseType)"
    try { $t.GetFields($flags) | ForEach-Object { $v=''; if($_.IsLiteral){ $v = ' = ' + $_.GetRawConstantValue() }; $ft=$_.FieldType; $tn2=$ft.Name; if($ft.IsGenericType){ $tn2 += '<' + (($ft.GetGenericArguments() | ForEach-Object Name) -join ',') + '>' }; "  F {0} {1}{2}{3}" -f $tn2, $_.Name, $(if($_.IsStatic){' static'}), $v } } catch { "  fields err $_" }
    try { $t.GetProperties($flags) | ForEach-Object { "  P {0} {1}" -f $_.PropertyType.Name, $_.Name } } catch { "  props err $_" }
    try { $t.GetMethods($flags) | Where-Object { -not $_.IsSpecialName } | ForEach-Object { $ps = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '; "  M {0} {1}({2}){3}" -f $_.ReturnType.Name, $_.Name, $ps, $(if($_.IsVirtual){' virtual'}) } } catch { "  methods err $_" }
}
