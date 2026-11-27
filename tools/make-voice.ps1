#Requires -PSEdition Desktop
# T043 (#83): records the Russian voice clips of the scanner with a Windows ru-RU voice (System.Speech, which only
# Windows PowerShell 5.1 has). The clips are committed (Assets/_Project/Audio/Voice); run again only to change the
# phrases or the voice. Keep this file in UTF-8 with a BOM: PowerShell 5.1 reads it as ANSI otherwise.
#   ./tools/make-voice.ps1 [-Voice "Microsoft Irina Desktop"] [-Rate 1]
param(
    [string]$Voice = "Microsoft Irina Desktop",
    [int]$Rate = 1,
    [string]$Out = (Join-Path $PSScriptRoot "..\Assets\_Project\Audio\Voice")
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Speech

# «Тот заказ. Семь из восьми»: the verdict, then the count short enough to be heard before the next scan.
$phrases = [ordered]@{
    "match" = "Тот заказ"
    "other" = "Не тот заказ"
    "done"  = "Заказ выдан"
    "of"    = "из"
}
$numbers = @("ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять",
    "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать",
    "восемнадцать", "девятнадцать", "двадцать")
# «… из восьми»: the total in the genitive.
$ofNumbers = @("нуля", "одного", "двух", "трёх", "четырёх", "пяти", "шести", "семи", "восьми", "девяти", "десяти",
    "одиннадцати", "двенадцати", "тринадцати", "четырнадцати", "пятнадцати", "шестнадцати", "семнадцати",
    "восемнадцати", "девятнадцати", "двадцати")
for ($i = 0; $i -lt $numbers.Count; $i++) { $phrases["n$i"] = $numbers[$i]; $phrases["g$i"] = $ofNumbers[$i] }

New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$installed = $synth.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo.Name }
if ($installed -notcontains $Voice) { throw "Voice '$Voice' is not installed. Installed: $($installed -join ', ')" }
$synth.SelectVoice($Voice)
$synth.Rate = $Rate
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
foreach ($key in $phrases.Keys) {
    $synth.SetOutputToWaveFile((Join-Path $Out "$key.wav"), $format)
    $synth.Speak($phrases[$key])
}
$synth.SetOutputToNull()
$synth.Dispose()

# Trim the silence the synthesizer leaves around each phrase, so the clips follow each other without gaps
# (ffmpeg, if it is installed; the clips work without it, just slower).
if (Get-Command ffmpeg -ErrorAction SilentlyContinue) {
    $trim = "silenceremove=start_periods=1:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse,apad=pad_dur=0.06"
    foreach ($key in $phrases.Keys) {
        $path = Join-Path $Out "$key.wav"
        $tmp = Join-Path ([IO.Path]::GetTempPath()) "pvz-voice-$key.wav" # outside Assets: Unity never imports a leftover
        & ffmpeg -loglevel error -y -i $path -af $trim -ar 22050 -ac 1 $tmp
        if ($LASTEXITCODE -eq 0) { Move-Item -Force $tmp $path } elseif (Test-Path $tmp) { Remove-Item -Force $tmp }
    }
}
Write-Output "Recorded $($phrases.Count) clips with '$Voice' into $Out"
