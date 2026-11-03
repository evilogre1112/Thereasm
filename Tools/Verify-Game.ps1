param(
    [string]$CompilerRoot = 'C:/Program Files/Unity/Hub/Editor/6000.0.29f1/Editor/Data/Tools/Compilation/ApiUpdater'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $CompilerRoot 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $CompilerRoot 'Microsoft.CodeAnalysis.CSharp.dll')
$parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)
$trees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts') -Filter '*.cs' | ForEach-Object {
    $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [System.IO.File]::ReadAllText($_.FullName), $parseOptions, $_.FullName))
}
$syntaxErrors = @($trees | ForEach-Object { $_.GetDiagnostics() } | Where-Object Severity -eq 'Error')
if ($syntaxErrors.Count) { $syntaxErrors | ForEach-Object { Write-Output $_.ToString() }; throw 'C# syntax check failed.' }
Write-Output "PASS: C# 9 syntax for $($trees.Count) runtime scripts."

# Parse both supported Fusion callback variants even before the SDK is installed.
foreach ($symbols in @(@('THEREASM_FUSION'), @('THEREASM_FUSION', 'THEREASM_FUSION_21'))) {
    $fusionOptions = $parseOptions.WithPreprocessorSymbols([string[]]$symbols)
    $fusionTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText((Join-Path $projectRoot 'Assets/Scripts/OnlineSession.Fusion.cs')), $fusionOptions)
    $fusionErrors = @($fusionTree.GetDiagnostics() | Where-Object Severity -eq 'Error')
    if ($fusionErrors.Count) { $fusionErrors | ForEach-Object { Write-Output $_.ToString() }; throw 'Fusion adapter syntax failed.' }
}
Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Editor') -Filter '*.cs' | ForEach-Object {
    $editorTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($_.FullName), $parseOptions)
    $editorErrors = @($editorTree.GetDiagnostics() | Where-Object Severity -eq 'Error')
    if ($editorErrors.Count) { $editorErrors | ForEach-Object { Write-Output $_.ToString() }; throw 'Editor script syntax failed.' }
}
Write-Output 'PASS: Editor scripts and Fusion 2.0/2.1 adapter syntax (SDK API compilation still requires importing Fusion).'

# Compile the independent rules against the current .NET runtime and execute them.
$runtimeReferences = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
([AppContext]::GetData('TRUSTED_PLATFORM_ASSEMBLIES') -split [IO.Path]::PathSeparator) | ForEach-Object {
    $runtimeReferences.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($_))
}
$ruleTrees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
foreach ($relative in @('Assets/Scripts/GameEnums.cs', 'Assets/Scripts/GameSettings.cs',
    'Assets/Scripts/GameRules.cs', 'Assets/Scripts/OnlineProtocol.cs', 'Assets/Scripts/TutorialRules.cs',
    'Tests/GameRulesTests.cs', 'Tests/OnlineRulesTests.cs', 'Tests/TutorialRulesTests.cs')) {
    $file = Join-Path $projectRoot $relative
    $ruleTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText($file), $parseOptions, $file))
}
$options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary)
$compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('ThereasmRulesTests', $ruleTrees, $runtimeReferences, $options)
$stream = [IO.MemoryStream]::new()
$result = $compilation.Emit($stream)
if (!$result.Success) { $result.Diagnostics | ForEach-Object { Write-Output $_.ToString() }; throw 'Rules compilation failed.' }
$assembly = [Reflection.Assembly]::Load($stream.ToArray())
$assembly.GetType('GameRulesTests').GetMethod('Run').Invoke($null, @()) | Write-Output
$assembly.GetType('OnlineRulesTests').GetMethod('Run').Invoke($null, @()) | Write-Output
$assembly.GetType('TutorialRulesTests').GetMethod('Run').Invoke($null, @()) | Write-Output
$stream.Dispose()

# Cached player assemblies provide an additional API/type check when Editor is absent.
# They are stripped: missing API errors must be distinguished from actual source errors.
$cache = Join-Path $projectRoot 'Library/Bee/artifacts/WinPlayerBuildProgram/ManagedStripped'
if (Test-Path $cache) {
    $references = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
    Get-ChildItem $cache -Filter '*.dll' | Where-Object { $_.Name -notmatch '^(Assembly-CSharp|UnityEngine.UI|Unity.TextMeshPro)\.dll$' } | ForEach-Object {
        $references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($_.FullName))
    }
    foreach ($name in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll')) {
        $references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $projectRoot "Library/ScriptAssemblies/$name")))
    }
    $compile = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('ThereasmCheck', $trees, $references, $options)
    $diagnostics = @($compile.GetDiagnostics() | Where-Object Severity -eq 'Error')
    if ($diagnostics.Count) {
        $diagnostics | ForEach-Object { Write-Output $_.ToString() }
        throw "Cached Unity reference check found $($diagnostics.Count) errors."
    }
    Write-Output 'PASS: Runtime scripts compile against cached Unity player references.'
}
