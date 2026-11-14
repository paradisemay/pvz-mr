# T043 (#83): records the Russian voice clips of the scanner with a Windows ru-RU voice (System.Speech).
# The clips are committed (Assets/_Project/Audio/Voice); run again only to change the phrases or the voice.
#   ./tools/make-voice.ps1 [-Voice "Microsoft Irina Desktop"] [-Rate 1]
param(
    [string]$Voice = "Microsoft Irina Desktop",
    [int]$Rate = 1,
    [string]$Out = "Assets/_Project/Audio/Voice"
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Speech

$phrases = [ordered]@{
    "match"   = "Тот заказ"
    "other"   = "Не тот заказ"
    "done"    = "Заказ выдан"
    "items"   = "Товаров"
    "scanned" = "отсканировано"
}
$numbers = @("ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять",
    "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать",
    "восемнадцать", "девятнадцать", "двадцать")
for ($i = 0; $i -lt $numbers.Count; $i++) { $phrases["n$i"] = $numbers[$i] }

New-Item -ItemType Directory -Force $Out | Out-Null
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$installed = $synth.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo.Name }
if ($installed -notcontains $Voice) { throw "Voice '$Voice' is not installed. Installed: $($installed -join ', ')" }
$synth.SelectVoice($Voice)
$synth.Rate = $Rate
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
foreach ($key in $phrases.Keys) {
    $path = Join-Path (Resolve-Path $Out) "$key.wav"
    $synth.SetOutputToWaveFile($path, $format)
    $synth.Speak($phrases[$key])
}
$synth.SetOutputToNull()
$synth.Dispose()

# Trim the silence the synthesizer leaves around each phrase, so the scanner says «Товаров восемь, отсканировано
# семь» without gaps (ffmpeg, if it is installed; the clips work without it, just slower).
if (Get-Command ffmpeg -ErrorAction SilentlyContinue) {
    $trim = "silenceremove=start_periods=1:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse,apad=pad_dur=0.06"
    foreach ($key in $phrases.Keys) {
        $path = Join-Path (Resolve-Path $Out) "$key.wav"
        $tmp = "$path.trim.wav"
        & ffmpeg -loglevel error -y -i $path -af $trim -ar 22050 -ac 1 $tmp
        if ($LASTEXITCODE -eq 0) { Move-Item -Force $tmp $path }
    }
}
Write-Output "Recorded $($phrases.Count) clips with '$Voice' into $Out"
