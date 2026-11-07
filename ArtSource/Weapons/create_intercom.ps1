$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$voiceSynth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voiceSynth.Rate = 0
$voiceSynth.Volume = 85
$voiceNames = $voiceSynth.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo.Name }
if ($voiceNames -contains 'Microsoft David Desktop') { $voiceSynth.SelectVoice('Microsoft David Desktop') }
$outputDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Assets/Mission/Audio'))
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$announcements = @{
    'Intro' = 'Attention crew. Hold the boarding ramps for four minutes while we prepare the engines.'
    'BossWarning' = 'Attention crew. A powerful infected is approaching. Eliminate it and clear the remaining hostiles so we can take off.'
    'ClearDeck' = 'The heavy infected is down. Clear the remaining hostiles and return aboard. No more are boarding.'
    'Departure' = 'All hostiles eliminated. Ramps sealing. Brace for departure.'
}
foreach ($entry in $announcements.GetEnumerator()) {
    $voiceSynth.SetOutputToWaveFile((Join-Path $outputDirectory ($entry.Key + '.wav')))
    $voiceSynth.Speak($entry.Value)
    $voiceSynth.SetOutputToNull()
}
$voiceSynth.Dispose()
Write-Output ('Generated four offline intercom clips. Installed voices: ' + ($voiceNames -join ', '))
