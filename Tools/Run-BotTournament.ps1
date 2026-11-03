param([string]$Unity = 'D:/Unity/6000.0.29f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $projectRoot 'Backups/TutorialValidationProject'
if (!(Test-Path -LiteralPath $Unity)) { throw "Unity Editor not found: $Unity" }
if (!(Test-Path -LiteralPath (Join-Path $validationRoot 'Packages/manifest.json'))) {
    throw 'Prepare the isolated validation project with Tools/Run-TutorialValidation.ps1 first.'
}
# Run only when this isolated checkout is idle. Never changes bots in the real project.
& robocopy (Join-Path $projectRoot 'Assets') (Join-Path $validationRoot 'Assets') /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Could not synchronize Assets into the isolated project.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tests/Unity/BotTournamentValidation.cs') -Destination (Join-Path $validationRoot 'Assets/Editor') -Force
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $validationRoot 'Assets/Scripts') -Filter '*.cs') +
    @(Get-Item -LiteralPath (Join-Path $validationRoot 'Assets/Scenes/MainBoard.unity')) +
    @(Get-ChildItem -LiteralPath (Join-Path $validationRoot 'Assets/ScriptableObjects') -Filter '*.asset') +
    @(Get-ChildItem -LiteralPath (Join-Path $validationRoot 'Assets/Skill_Neutral') -Filter '*.asset')
$sourceManifest = @($sourceFiles | Sort-Object FullName | ForEach-Object {
    @{ path = $_.FullName.Substring($validationRoot.Length + 1).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
@{ configuration = 'Level 2; 50 starts each; seeds 20261030..20261129'; files = $sourceManifest } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'Tests/Reports/BotTournament-Level2-source.json') -Encoding UTF8
$started = Get-Date
$log = Join-Path $validationRoot 'BotTournament-Level2.log'
$process = Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-nographics','-projectPath',
    ('"{0}"' -f $validationRoot),'-executeMethod','BotTournamentValidation.Run','-logFile',('"{0}"' -f $log)) -WindowStyle Hidden -PassThru
$process.WaitForExit()
$reportFile = Join-Path $validationRoot 'BotTournament-Level2.json'
if (!(Test-Path -LiteralPath $reportFile) -or (Get-Item -LiteralPath $reportFile).LastWriteTime -lt $started) {
    throw "No fresh tournament results. See $log"
}
$report = Get-Content -LiteralPath $reportFile -Raw | ConvertFrom-Json
$destination = Join-Path $projectRoot 'Tests/Reports'
foreach ($filename in @('BotTournament-Level2.json','BotTournament-Level2.csv')) {
    Copy-Item -LiteralPath (Join-Path $validationRoot $filename) -Destination $destination -Force
}
Write-Output "Level $($report.level); recorded: $($report.recorded); Cursermancer: $($report.cursermancerWins); Villager: $($report.villagerWins); unresolved: $($report.unresolved); errors: $($report.errors)."
if ($process.ExitCode -ne 0 -or !$report.complete -or $report.recorded -ne 100) { throw "Tournament incomplete. See $reportFile" }
if (@($report.matches | Where-Object level -ne 2).Count -ne 0) { throw 'A match used the wrong level.' }
foreach ($side in @('Cursermancer','Villager')) {
    if (@($report.matches | Where-Object firstPlayer -eq $side).Count -ne 50) { throw "Unbalanced first-player distribution: $side" }
}
