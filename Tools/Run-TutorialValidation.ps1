param([string]$Unity = 'D:/Unity/6000.0.29f1/Editor/Unity.exe',
    [ValidateSet('Tutorial','Support','MenuModes','Fusion','Skills','Online','Visibility','Localization','Languages','Academy','Interaction','Presentation','SkillFx','Audio','BotSmoke')][string]$Suite = 'Tutorial')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $projectRoot 'Backups/TutorialValidationProject'
if (!(Test-Path $Unity)) { throw "Unity Editor not found: $Unity" }
# Independent project instance for batch automated testing.
New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
foreach ($folder in @('Assets','ProjectSettings','Packages')) {
    $destination = Join-Path $validationRoot $folder
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Copy-Item -Path (Join-Path $projectRoot "$folder/*") -Destination $destination -Recurse -Force
}
# Reuse installed packages locally rather than fetching them again.
Get-ChildItem (Join-Path $projectRoot 'Library/PackageCache') -Directory | ForEach-Object {
    $manifest = Join-Path $_.FullName 'package.json'
    if (Test-Path $manifest) {
        $packageName = (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).name
        $link = Join-Path $validationRoot "Packages/$packageName"
        & robocopy $_.FullName $link /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 /MT:8 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Failed to copy cached package $packageName" }
    }
}
$editorTests = Join-Path $validationRoot 'Assets/Editor'
New-Item -ItemType Directory -Force -Path $editorTests | Out-Null
Get-ChildItem (Join-Path $projectRoot 'Tests/Unity') -Filter '*.cs' | Copy-Item -Destination $editorTests -Force
if ($Suite -eq 'Online') {
    $probeDirectory = Join-Path $validationRoot 'Assets/OnlineValidation'
    New-Item -ItemType Directory -Force -Path $probeDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Tests/Players/OnlineMatchProbe.cs') -Destination $probeDirectory -Force
}
$testClass = switch ($Suite) { 'Support' { 'TutorialSupportValidation' } 'Localization' { 'GameLocalizationValidation' } 'Languages' { 'LanguageVariantBuild' } 'MenuModes' { 'MenuModeValidation' } 'Fusion' { 'FusionSdkValidation' } 'Skills' { 'SkillInteractionValidation' } 'Online' { 'OnlinePlayerBuildValidation' } 'Visibility' { 'OnlineVisibilityValidation' } 'Academy' { 'TutorialAcademyValidation' } 'Interaction' { 'TutorialAcademyValidation' } 'Presentation' { 'BoardPresentationValidation' } 'SkillFx' { 'NeutralSkillFxValidation' } 'Audio' { 'SkillAudioValidation' } 'BotSmoke' { 'BotTournamentValidation' } default { 'TutorialPlayModeValidation' } }
$resultName = switch ($Suite) { 'Support' { 'TutorialSupportValidationResult.json' } 'Localization' { 'GameLocalizationValidationResult.json' } 'Languages' { 'LanguageVariantBuildResult.json' } 'MenuModes' { 'MenuModeValidationResult.json' } 'Fusion' { 'FusionSdkValidationResult.json' } 'Skills' { 'SkillInteractionValidationResult.json' } 'Online' { 'OnlinePlayerBuildResult.json' } 'Visibility' { 'OnlineVisibilityValidationResult.json' } 'Academy' { 'TutorialAcademyValidationResult.json' } 'Interaction' { 'TutorialInteractionValidationResult.json' } 'Presentation' { 'BoardPresentationValidationResult.json' } 'SkillFx' { 'NeutralSkillFxValidationResult.json' } 'Audio' { 'SkillAudioValidationResult.json' } 'BotSmoke' { 'BotTournament-Level2.json' } default { 'TutorialValidationResult.json' } }
$entryMethod = if ($Suite -eq 'Online' -or $Suite -eq 'Languages') { "$testClass.Build" } elseif ($Suite -eq 'Interaction') { "$testClass.RunInteractionOnly" } elseif ($Suite -eq 'BotSmoke') { "$testClass.RunSmoke" } else { "$testClass.Run" }
$unityLog = Join-Path $validationRoot "$Suite`Validation.log"
if ($Suite -eq 'Fusion') {
    # Persist SDK defines/settings in a separate Editor run before compiling the test branch.
    $setupLog = Join-Path $validationRoot 'FusionSdkImport.log'
    $setup = Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-nographics','-projectPath',
        ('"{0}"' -f $validationRoot),'-executeMethod','PhotonFusionSetup.Configure','-quit','-logFile',
        ('"{0}"' -f $setupLog)) -WorkingDirectory $validationRoot -WindowStyle Hidden -PassThru
    $setup.WaitForExit()
    if ($setup.ExitCode -ne 0) { throw "Unity Fusion setup failed; inspect $setupLog" }
}
$testStarted = Get-Date
$unityProcess = Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-projectPath',
    ('"{0}"' -f $validationRoot),'-executeMethod',$entryMethod,'-logFile',('"{0}"' -f $unityLog)) -WorkingDirectory $validationRoot -WindowStyle Hidden -PassThru
$unityProcess.WaitForExit()
Write-Output "Unity exit code: $($unityProcess.ExitCode)"
$report = Join-Path $validationRoot $resultName
if ((Test-Path $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $testStarted) {
    Get-Content -LiteralPath $report -Raw | Write-Output
    $reportsDirectory = Join-Path $projectRoot 'Tests/Reports'
    New-Item -ItemType Directory -Force -Path $reportsDirectory | Out-Null
    $savedReport = if ($Suite -eq 'Tutorial') { 'TutorialPlayModeResult.json' } else { $resultName }
    Copy-Item -LiteralPath $report -Destination (Join-Path $reportsDirectory $savedReport) -Force
}
else { Get-Content -LiteralPath $unityLog -Tail 45 | Write-Output; throw "Unity did not produce a $Suite validation report." }
if ($unityProcess.ExitCode -ne 0) { throw "Unity $Suite validation failed." }
if ($Suite -eq 'Online') {
    & (Join-Path $PSScriptRoot 'Run-OnlinePlayers.ps1') -Player (Join-Path $validationRoot 'OnlinePlayerBuild/Thereasm.exe') -Episode Both
}
