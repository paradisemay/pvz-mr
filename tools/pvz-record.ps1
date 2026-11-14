<#
.SYNOPSIS
  Records what the Quest shows — the real room (passthrough) plus the game — and makes an MP4 (and a GIF) of one eye.

.EXAMPLE
  ./tools/pvz-record.ps1 -Seconds 60 -Name shift-normal -Gif

.NOTES
  Uses the headset's own `screenrecord` (both eyes, ~4128x2208). The left eye is cropped and scaled down.
  Recording is limited to 180 s by Android. Needs ffmpeg (FFMPEG overrides the path).
#>
param(
    [int]$Seconds = 30,
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$Out = "docs/media/device",
    [switch]$Gif,
    [int]$Width = 960,
    [double]$GifStart = 0,
    [double]$GifSeconds = 20
)

$adb = $env:ADB
if (-not $adb) { $adb = "C:\Program Files\Unity\Hub\Editor\6000.2.2f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" }
$ffmpeg = $env:FFMPEG
if (-not $ffmpeg) {
    $ffmpeg = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Gyan.FFmpeg*\*\bin\ffmpeg.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not (Test-Path $adb)) { throw "adb not found: $adb (set ADB)" }
if (-not ($ffmpeg -and (Test-Path $ffmpeg))) { throw "ffmpeg not found (set FFMPEG)" }
$Seconds = [Math]::Max(1, [Math]::Min(180, $Seconds))
New-Item -ItemType Directory -Force $Out | Out-Null
$remote = "/sdcard/pvz_$Name.mp4"

& $adb shell "screenrecord --time-limit $Seconds --bit-rate 20000000 $remote"
$raw = Join-Path ([System.IO.Path]::GetTempPath()) "pvz_$Name.mp4"
& $adb pull $remote $raw | Out-Null
& $adb shell "rm -f $remote" | Out-Null

# Left eye only (the frame holds both eyes side by side), cropped to the lens centre and scaled down.
$mp4 = Join-Path $Out "$Name.mp4"
& $ffmpeg -y -loglevel error -i $raw -vf "crop=iw/2*0.82:ih*0.72:iw/2*0.09:ih*0.12,scale=${Width}:-2" -c:v libx264 -pix_fmt yuv420p -crf 27 -movflags +faststart -an $mp4
"video: $mp4 ($([Math]::Round((Get-Item $mp4).Length / 1MB, 1)) MB)"
if ($Gif) {
    $gifPath = Join-Path $Out "$Name.gif"
    # A short excerpt keeps the GIF small enough to show inline on GitHub.
    & $ffmpeg -y -loglevel error -ss $GifStart -t $GifSeconds -i $mp4 -vf "fps=7,scale=480:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=64[p];[b][p]paletteuse=dither=bayer" $gifPath
    "gif: $gifPath ($([Math]::Round((Get-Item $gifPath).Length / 1MB, 1)) MB)"
}
Remove-Item $raw -ErrorAction SilentlyContinue
