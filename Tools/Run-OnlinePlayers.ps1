param(
    [string]$Player = 'D:/Project/Thereasm/Backups/TutorialValidationProject/OnlinePlayerBuild/Thereasm.exe',
    [ValidateSet('A','B','Both')][string]$Episode = 'Both'
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Player)) { throw 'Build OnlinePlayerBuildValidation with Tests/Players/OnlineMatchProbe.cs in the isolated project first.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$episodes = if ($Episode -eq 'Both') { @('A','B') } else { @($Episode) }
$results = @()
foreach ($case in $episodes) {
    $folder = Join-Path $projectRoot ('Backups/OnlineMultiplayerValidation/' + $case + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $room = 'THR-' + [Guid]::NewGuid().ToString('N').Substring(0,8).ToUpperInvariant()
    $shared = @('-batchmode','-nographics','-screen-width','1600','-screen-height','900','-thereasm-probe',
        '-probe-folder',('"{0}"' -f $folder),'-probe-room',$room,'-probe-episode',$case)
    $hostLanguage = if ($case -eq 'A') { 'en' } else { 'vi' }
    $clientLanguage = if ($case -eq 'A') { 'vi' } else { 'en' }
    $hostRun = Start-Process -FilePath $Player -ArgumentList ($shared + @('-thereasm-language',$hostLanguage,'-probe-mode','host','-logFile',('"{0}/host.log"' -f $folder))) -WorkingDirectory (Split-Path $Player) -WindowStyle Hidden -PassThru
    $clientRun = Start-Process -FilePath $Player -ArgumentList ($shared + @('-thereasm-language',$clientLanguage,'-probe-mode','client','-logFile',('"{0}/client.log"' -f $folder))) -WorkingDirectory (Split-Path $Player) -WindowStyle Hidden -PassThru
    @($hostRun.Id,$clientRun.Id) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'pids.json')
    Write-Output "Online case $case reports: $folder"
    $startedAt = Get-Date
    $lastProgress = ''
    while (!$hostRun.HasExited -or !$clientRun.HasExited) {
        $hostRun.Refresh(); $clientRun.Refresh()
        $progress = @()
        foreach ($peer in @('host','client')) {
            $report = Join-Path $folder "$peer.json"
            if (Test-Path -LiteralPath $report) {
                try { $data = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json; $progress += "$peer=$($data.stage)" } catch { }
            }
        }
        $current = $progress -join ', '
        if ($current -ne $lastProgress) { Write-Output "$case $current"; $lastProgress = $current }
        if (((Get-Date) - $startedAt).TotalSeconds -gt 340) {
            foreach ($proc in @($hostRun,$clientRun)) { if (!$proc.HasExited) { Stop-Process -Id $proc.Id } }
            throw "Online case $case timed out. Reports: $folder"
        }
        Start-Sleep -Milliseconds 500
    }
    $hostRun.WaitForExit(); $clientRun.WaitForExit()
    $hostReport = Get-Content -LiteralPath (Join-Path $folder 'host.json') -Raw | ConvertFrom-Json
    $clientReport = Get-Content -LiteralPath (Join-Path $folder 'client.json') -Raw | ConvertFrom-Json
    if (!$hostReport.passed -or !$clientReport.passed) {
        Write-Output "Host: $($hostReport.message)"
        Write-Output "Client: $($clientReport.message)"
        throw "Online case $case failed. Reports: $folder"
    }
    $reports = Join-Path $projectRoot 'Tests/Reports'
    New-Item -ItemType Directory -Force -Path $reports | Out-Null
    foreach ($peer in @('host','client')) { Copy-Item -LiteralPath (Join-Path $folder "$peer.json") -Destination (Join-Path $reports "OnlineMatch-$case-$peer.json") -Force }
    $results += [PSCustomObject]@{ episode=$case; passed=$true; hostLanguage=$hostLanguage; clientLanguage=$clientLanguage; host=$hostReport; client=$clientReport; logs=$folder }
    Write-Output "PASS: online case $case, full match and rematch."
}
[PSCustomObject]@{ passed=$true; episodes=$results } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $projectRoot 'Tests/Reports/OnlineMatchValidationResult.json')
