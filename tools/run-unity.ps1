<#
.SYNOPSIS
  Runs a static editor method of this project in Unity batchmode and prints the relevant log lines.

.EXAMPLE
  ./tools/run-unity.ps1 -Method Pvz.Editor.PvzSceneBuilder.BuildAll
  ./tools/run-unity.ps1 -Method Pvz.Editor.PvzTestRunner.RunEditMode -NoQuit -Extra '-pvzResults','Logs/editmode.xml'

.NOTES
  Waits only for the editor process itself (Start-Process -Wait would also wait for Unity's
  long-lived child processes such as the licensing client and hang).
  Set UNITY_EXE to override the editor path.
#>
param(
    [Parameter(Mandatory = $true)][string]$Method,
    [string]$Log = "Logs/unity.log",
    [switch]$NoQuit,
    [string[]]$Extra = @(),
    [int]$TimeoutSec = 900
)

$unity = $env:UNITY_EXE
if (-not $unity) { $unity = "C:\Program Files\Unity\Hub\Editor\6000.2.2f1\Editor\Unity.exe" }
$project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$logPath = Join-Path $project $Log

$unityArgs = @('-batchmode', '-projectPath', "`"$project`"", '-executeMethod', $Method, '-logFile', "`"$logPath`"") + $Extra
if (-not $NoQuit) { $unityArgs = @('-quit') + $unityArgs }

$process = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
$null = $process.Handle  # keep the handle so ExitCode is available after exit
if (-not $process.WaitForExit($TimeoutSec * 1000)) {
    Stop-Process -Id $process.Id -Force
    Write-Host "TIMEOUT after $TimeoutSec s"
    exit 124
}

if (Test-Path $logPath) {
    Select-String -Path $logPath -Pattern 'error CS|Exception|threw exception|\[Pvz' |
        ForEach-Object { $_.Line } | Select-Object -First 120
}
Write-Host "exit: $($process.ExitCode)"
exit $process.ExitCode
