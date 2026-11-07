Add-Type -AssemblyName System.Speech
$radioLines = @(
'The chopper is out of fuel. Clear the crash courtyard, break into the electrical room, and restore backup power to the subway.',
'Backup power restored. Subway lights are online. Finish clearing the crash courtyard.',
'Sprinters are pouring out beneath the raised tail. Demolitions, plant C four on the tail and seal that opening.',
'Charge armed. Stand clear of the tail.',
'Tail opening sealed. Clear the ticket hall, corridor, and both platforms. Watch the waiting rooms.',
'Platforms are clear. Board the intact train on platform one. Departure is automatic.',
'Station stop. Infected are boarding. Clear the train before we can continue.',
'Research district reached. Clear the platform. Use security to enter the laboratory and recover the vaccine.',
'Heavy infected near the parking enclosure. Regurgitator incoming! Keep moving and bring it down.',
'Finish clearing the district and recover the vaccine. Extraction is standing by.',
'Landing at the marked ship zone. Bring the vaccine aboard. We leave when everyone is inside.',
'All personnel aboard. Vaccine secured. Lifting off.'
)
$radioDir = Join-Path $PSScriptRoot '../Assets/Vaccine/Combat/Audio'
New-Item -ItemType Directory -Force -Path $radioDir | Out-Null
$voice = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voice.Rate = 0
for($i=0;$i -lt $radioLines.Count;$i++){
 $file=Join-Path $radioDir ('Radio-{0:00}.wav' -f $i)
 $voice.SetOutputToWaveFile($file)
 $voice.Speak($radioLines[$i])
 $voice.SetOutputToNull()
}
$voice.Dispose()
Write-Output 'Twelve mission radio clips created.'
