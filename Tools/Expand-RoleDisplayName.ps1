# One-time copy change. C# identifiers and networking/asset keys are deliberately preserved.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerRoot = 'C:/Program Files/Unity/Hub/Editor/6000.0.29f1/Editor/Data/Tools/Compilation/ApiUpdater'
Add-Type -Path (Join-Path $compilerRoot 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $compilerRoot 'Microsoft.CodeAnalysis.CSharp.dll')
$backup = Join-Path $projectRoot 'Backups/2026-10-27-role-display-name'
New-Item -ItemType Directory -Force -Path $backup | Out-Null
$literalKind = [int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::StringLiteralToken
$textKind = [int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::InterpolatedStringTextToken
Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts') -Filter '*.cs' | ForEach-Object {
    $source = [IO.File]::ReadAllText($_.FullName)
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source)
    $changes = @($tree.GetRoot().DescendantTokens() | Where-Object { ($_.RawKind -eq $literalKind -or $_.RawKind -eq $textKind) -and $_.Text -cmatch '\b(?:Cur|CUR)\b' })
    if ($changes.Count) {
        $saved = Join-Path $backup $_.Name
        if (!(Test-Path $saved)) { Copy-Item -LiteralPath $_.FullName -Destination $saved }
        foreach ($token in ($changes | Sort-Object { $_.Span.Start } -Descending)) {
            $updated = [regex]::Replace($token.Text,'\bCur\b','Cursermancer')
            $updated = [regex]::Replace($updated,'\bCUR\b','CURSERMANCER')
            $source = $source.Remove($token.Span.Start,$token.Span.Length).Insert($token.Span.Start,$updated)
        }
        [IO.File]::WriteAllText($_.FullName,$source,[Text.UTF8Encoding]::new($false))
        Write-Output "Updated display text: $($_.Name)"
    }
}
