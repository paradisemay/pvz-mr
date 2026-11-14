<#
.SYNOPSIS
  Pulls snapshots and recordings made by the debug remote from the Quest and turns recordings into MP4 + GIF.

.EXAMPLE
  ./tools/pvz-media.ps1 -Out docs/media/device
  ./tools/pvz-media.ps1 -Out docs/media/device -Name shift-normal -Gif

.NOTES
  Needs ffmpeg (FFMPEG overrides the path). Recordings are frame sequences (f_00001.jpg …) under
  /sdcard/Android/data/com.pvzmr.prototype/files/pvz-media/rec/<yyyyMMdd-HHmmss-NN>; the newest one is converted.
#>
param(
    [string]$Out = "docs/media/device",
    [string]$Name = "",
    [switch]$Gif,
    [int]$Fps = 12,
    [switch]$KeepFrames
)

$adb = $env:ADB
if (-not $adb) { $adb = "C:\Program Files\Unity\Hub\Editor\6000.2.2f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" }
$ffmpeg = $env:FFMPEG
if (-not $ffmpeg) {
    $ffmpeg = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Gyan.FFmpeg*\*\bin\ffmpeg.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not (Test-Path $adb)) { throw "adb not found: $adb (set ADB)" }
if ($Name -and -not ($ffmpeg -and (Test-Path $ffmpeg))) { throw "ffmpeg not found (set FFMPEG)" }
$remote = "/sdcard/Android/data/com.pvzmr.prototype/files/pvz-media"
New-Item -ItemType Directory -Force $Out | Out-Null
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("pvz-media-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Force $work | Out-Null

& $adb pull "$remote/shots" $work | Out-Null
Get-ChildItem (Join-Path $work "shots") -Filter *.png -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $Out $_.Name) -Force
    "shot: $(Join-Path $Out $_.Name)"
}

$latest = (& $adb shell "ls -1 $remote/rec 2>/dev/null" | Where-Object { $_ -match '^\d{8}-\d{6}-\d+$' } | Sort-Object | Select-Object -Last 1)
if ($latest -and $Name) {
    & $adb pull "$remote/rec/$latest" $work | Out-Null
    $frames = Join-Path $work $latest
    $mp4 = Join-Path $Out "$Name.mp4"
    & $ffmpeg -y -loglevel error -framerate $Fps -i (Join-Path $frames "f_%05d.jpg") -c:v libx264 -pix_fmt yuv420p -crf 26 -movflags +faststart $mp4
    "video: $mp4 ($((Get-Item $mp4).Length / 1MB) MB)"
    if ($Gif) {
        $gifPath = Join-Path $Out "$Name.gif"
        & $ffmpeg -y -loglevel error -i $mp4 -vf "fps=8,scale=640:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse=dither=bayer" $gifPath
        "gif: $gifPath ($((Get-Item $gifPath).Length / 1MB) MB)"
    }
    if (-not $KeepFrames) { & $adb shell "rm -rf $remote/rec/$latest" | Out-Null }
}
Remove-Item -Recurse -Force $work
