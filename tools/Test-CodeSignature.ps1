# Verifica che i file pubblicati siano firmati con il certificato atteso.
#
# Lo usa il workflow di Release subito dopo la firma: una Release non deve poter
# dichiarare di essere firmata senza una verifica indipendente (la firma di
# signtool e quella fatta da Inno Setup sull'installer sono due percorsi diversi
# e possono fallire in silenzio).
#
# Il certificato è autofirmato e sul runner NON è attendibile, quindi la verifica
# non si basa sulla catena: controlla che
#   - esista una firma Authenticode (non "non firmato");
#   - l'hash corrisponda (il file non sia stato modificato dopo la firma);
#   - il certificato che ha firmato sia esattamente quello atteso (impronta);
#   - il certificato non sia scaduto al momento della verifica.
#
# Uso:
#   .\tools\Test-CodeSignature.ps1 -Path dist\app\PresenzeFlow.exe -Thumbprint $env:SIGN_CERT_THUMBPRINT
#   .\tools\Test-CodeSignature.ps1 -Path a.exe, b.exe -Thumbprint $thumbprint

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,

    [Parameter(Mandatory = $true)]
    [string]$Thumbprint
)

$ErrorActionPreference = 'Stop'

# Decisione sulla firma, separata dalla lettura del file: prende l'oggetto
# restituito da Get-AuthenticodeSignature e restituisce il problema trovato
# (o $null se va tutto bene). Così la regola è verificabile con oggetti finti.
function Get-SignatureProblem {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        $Signature,
        [Parameter(Mandatory = $true)][string]$ExpectedThumbprint
    )

    if ($null -eq $Signature) {
        return "$FileName non è firmato: nessuna firma Authenticode."
    }
    if ($Signature.Status -eq 'NotSigned' -or $null -eq $Signature.SignerCertificate) {
        return "$FileName non è firmato (stato: $($Signature.Status))."
    }
    if ($Signature.Status -eq 'HashMismatch') {
        return "$FileName è stato modificato dopo la firma: l'hash non corrisponde più."
    }
    if ($Signature.SignerCertificate.Thumbprint -ne $ExpectedThumbprint) {
        return "$FileName è firmato da un certificato diverso da quello atteso: $($Signature.SignerCertificate.Thumbprint) invece di $ExpectedThumbprint."
    }
    if ($Signature.SignerCertificate.NotAfter -lt (Get-Date)) {
        return "$FileName è firmato con un certificato scaduto il $($Signature.SignerCertificate.NotAfter.ToString('yyyy-MM-dd'))."
    }

    return $null
}

$expected = $Thumbprint.Trim().ToUpperInvariant()
$verified = 0

foreach ($file in $Path) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "File da verificare mancante: $file"
    }

    $signature = Get-AuthenticodeSignature -FilePath $file
    $problem = Get-SignatureProblem -FileName $file -Signature $signature -ExpectedThumbprint $expected
    if ($problem) {
        throw "Firma non valida: $problem"
    }

    $timestamp = if ($null -ne $signature.TimeStamperCertificate) { 'con marca temporale' } else { 'SENZA marca temporale' }
    if ($null -eq $signature.TimeStamperCertificate) {
        Write-Warning "La firma di $file non ha una marca temporale: diventerà non valida quando il certificato scade."
    }

    Write-Host "Firma valida: $file ($($signature.Status), $timestamp, impronta $($signature.SignerCertificate.Thumbprint))."
    $verified++
}

Write-Host "Verifica completata: $verified file firmati con il certificato atteso."
