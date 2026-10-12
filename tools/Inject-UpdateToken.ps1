<#
.SYNOPSIS
  Inietta nella build il token di sola lettura usato dall'app per gli aggiornamenti.

.DESCRIPTION
  Legge il token dalla variabile d'ambiente PRESENZEFLOW_GITHUB_TOKEN (mai da un
  parametro: i parametri finiscono nei log), lo offusca con uno XOR a chiave
  casuale (stesso schema di EmbeddedUpdateToken.cs) e riscrive le due costanti
  Payload e Key in PresenzeXmlConverter\EmbeddedUpdateToken.cs.

  Il file viene modificato solo nell'area di lavoro della CI: non va mai
  committato con i valori compilati.

  Il token e' offuscato, non cifrato: chi ha l'eseguibile puo' estrarlo. Deve avere
  il solo permesso "Contents: Read-only" sul solo repository delle Release.

.PARAMETER Path
  File da riscrivere (predefinito: EmbeddedUpdateToken.cs del progetto).
#>
param(
  [string]$Path = (Join-Path $PSScriptRoot '..\PresenzeXmlConverter\EmbeddedUpdateToken.cs')
)

$ErrorActionPreference = 'Stop'

$raw = $env:PRESENZEFLOW_GITHUB_TOKEN
if ([string]::IsNullOrWhiteSpace($raw)) {
  throw 'La variabile d''ambiente PRESENZEFLOW_GITHUB_TOKEN e'' vuota: nessun token da iniettare.'
}

# Stessa pulizia di UpdateService.SanitizeToken: spazi, a capo, virgolette e prefisso Bearer.
$token = $raw.Trim().Trim('"', "'", '`').Trim()
$token = $token -replace '^(?i:bearer|token)\s+', ''
$token = $token -replace '\s+', ''
if ($token -notmatch '^[A-Za-z0-9_]{20,400}$') {
  throw 'Il valore di PRESENZEFLOW_GITHUB_TOKEN non sembra un token GitHub (solo lettere, numeri e "_", da 20 a 400 caratteri).'
}

if (-not (Test-Path $Path)) { throw "File non trovato: $Path" }

$key = New-Object byte[] 32
$generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $generator.GetBytes($key) } finally { $generator.Dispose() }

$data = [System.Text.Encoding]::UTF8.GetBytes($token)
for ($index = 0; $index -lt $data.Length; $index++) {
  $data[$index] = $data[$index] -bxor $key[$index % $key.Length]
}
$payload = [Convert]::ToBase64String($data)
$keyText = [Convert]::ToBase64String($key)

$source = [System.IO.File]::ReadAllText($Path)
$payloadPattern = 'private const string Payload = "[^"]*";'
$keyPattern = 'private const string Key = "[^"]*";'
if (-not [regex]::IsMatch($source, $payloadPattern) -or -not [regex]::IsMatch($source, $keyPattern)) {
  throw "Le costanti Payload e Key non sono state trovate in $Path."
}
$source = [regex]::Replace($source, $payloadPattern, "private const string Payload = `"$payload`";")
$source = [regex]::Replace($source, $keyPattern, "private const string Key = `"$keyText`";")
[System.IO.File]::WriteAllText($Path, $source, (New-Object System.Text.UTF8Encoding($false)))

# Controllo di ritorno: il file riscritto deve contenere valori non vuoti.
$check = [System.IO.File]::ReadAllText($Path)
if ($check -match 'private const string Payload = "";' -or $check -match 'private const string Key = "";') {
  throw 'Iniezione non riuscita: le costanti sono rimaste vuote.'
}
Write-Host "Token di aggiornamento iniettato in $Path ($($token.Length) caratteri, offuscato)."
