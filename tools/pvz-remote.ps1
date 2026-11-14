<#
.SYNOPSIS
  Sends commands to the debug remote of the game on a Quest over adb (debug build: pvz-mr-dev.apk).

.EXAMPLE
  ./tools/pvz-remote.ps1 state
  ./tools/pvz-remote.ps1 "begin normal" "autoplay on"
  ./tools/pvz-remote.ps1 "shot counter_view counter"

.NOTES
  Forwards tcp:7777 to the headset, reads the remote's token from the app's files over adb, then sends each command
  as one line and prints the JSON reply. Set ADB to override the adb path (default: the Android SDK shipped with Unity).
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)][string[]]$Commands,
    [int]$Port = 7777,
    [switch]$NoForward,
    [string]$Package = "com.pvzmr.prototype"
)

$ErrorActionPreference = "Stop"
$adb = $env:ADB
if (-not $adb) { $adb = "C:\Program Files\Unity\Hub\Editor\6000.2.2f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" }
if (-not (Test-Path $adb)) { throw "adb not found: $adb (set ADB)" }
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if (-not $NoForward) {
    & $adb forward "tcp:$Port" "tcp:$Port" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "adb forward failed (is the headset connected and authorised?)" }
}
$token = (& $adb shell "cat /sdcard/Android/data/$Package/files/remote-token.txt" 2>$null | Out-String).Trim()
if (-not $token) { throw "no remote token on the headset: is the debug build (pvz-mr-dev.apk) running?" }

$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("127.0.0.1", $Port)
$stream = $client.GetStream()
$stream.ReadTimeout = 40000 # the game answers within 30 s or reports a timeout itself
$utf8 = New-Object System.Text.UTF8Encoding($false)
$writer = New-Object System.IO.StreamWriter($stream, $utf8)
$writer.AutoFlush = $true
$writer.NewLine = "`n"
$reader = New-Object System.IO.StreamReader($stream, $utf8)
try {
    $writer.WriteLine("token $token")
    $auth = $reader.ReadLine()
    if ($auth -notmatch '"auth":true') { throw "the remote refused the token: $auth" }
    foreach ($command in $Commands) {
        $writer.WriteLine($command)
        $reader.ReadLine()
    }
}
finally { $client.Close() }
