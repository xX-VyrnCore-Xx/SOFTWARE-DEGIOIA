# Crea (o rinnova) un certificato di firma del codice per PresenzeFlow e prepara
# tutto ciò che serve al workflow di Release.
#
# Esegui su Windows, dalla root del repository:
#   .\tools\New-CodeSigningCertificate.ps1
#
# Lo script NON pubblica nulla su Internet e non richiede privilegi di
# amministratore: il certificato viene creato nell'archivio dell'utente corrente.
#
# Produce, nella cartella .\codesign (ignorata da git, NON committarla):
#   PresenzeFlow-codesign.pfx            certificato con chiave privata (per firmare)
#   PresenzeFlow-codesign.pfx.base64.txt contenuto per il secret CODE_SIGNING_PFX_BASE64
#   PresenzeFlow-codesign.cer            certificato pubblico da installare sui PC
#   PASSWORD.txt                         la password del .pfx, da copiare senza errori
#
# I due secret devono venire dallo STESSO certificato: la password di un altro
# certificato fa fallire la firma con "The specified network password is not
# correct", anche quando sembra giusta.
#
# Poi, su GitHub → Settings → Secrets and variables → Actions, crea:
#   CODE_SIGNING_PFX_BASE64  = contenuto di PresenzeFlow-codesign.pfx.base64.txt
#   CODE_SIGNING_PFX_PASSWORD = la password salvata in codesign\PASSWORD.txt
#
# Il certificato è AUTOFIRMATO: firma l'app e l'installer, ma Windows si fida
# solo se il .cer viene aggiunto ai "Trusted Root" e "Trusted Publishers" dei PC
# (vedi README, sezione "Firma digitale"). Per una firma riconosciuta da tutti
# serve un certificato emesso da una CA pubblica.

[CmdletBinding()]
param(
    [string]$Subject = 'CN=DeGioia, O=DeGioia, OU=PresenzeFlow',
    [string]$Password,
    [ValidateRange(1, 10)]
    [int]$Years = 3,
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'codesign')
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 5) {
    throw 'Serve Windows PowerShell 5.1 o PowerShell 7.'
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    $Password = ([Guid]::NewGuid().ToString('N').Substring(0, 20)) + '!Aa1'
}

Write-Host "Creo un certificato di firma del codice valido per $Years anni…" -ForegroundColor Cyan
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -FriendlyName 'PresenzeFlow code signing' `
    -KeyAlgorithm RSA `
    -KeyLength 4096 `
    -HashAlgorithm SHA256 `
    -KeyUsage DigitalSignature `
    -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears($Years) `
    -CertStoreLocation 'Cert:\CurrentUser\My'

if (-not $certificate.HasPrivateKey) {
    throw 'Il certificato creato non ha la chiave privata: impossibile firmare.'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$pfxPath = Join-Path $OutputDirectory 'PresenzeFlow-codesign.pfx'
$cerPath = Join-Path $OutputDirectory 'PresenzeFlow-codesign.cer'
$base64Path = Join-Path $OutputDirectory 'PresenzeFlow-codesign.pfx.base64.txt'
$passwordPath = Join-Path $OutputDirectory 'PASSWORD.txt'

$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $securePassword | Out-Null
Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null
[IO.File]::WriteAllText($base64Path, [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath)), [Text.Encoding]::ASCII)
# Anche la password su file: copiarla dal terminale e' il modo piu' facile di
# sbagliarla (uno spazio o un a capo in piu' e il .pfx non si apre).
[IO.File]::WriteAllText($passwordPath, $Password, [Text.Encoding]::ASCII)

Write-Host ''
Write-Host 'Certificato creato e pronto.' -ForegroundColor Green
Write-Host "  Impronta (thumbprint): $($certificate.Thumbprint)"
Write-Host "  Scadenza:              $($certificate.NotAfter.ToString('yyyy-MM-dd'))"
Write-Host "  Cartella:              $OutputDirectory"
Write-Host ''
Write-Host 'PASSAGGIO 1 — secret su GitHub (Settings → Secrets → Actions):' -ForegroundColor Cyan
Write-Host '  CODE_SIGNING_PFX_BASE64   = contenuto di PresenzeFlow-codesign.pfx.base64.txt'
Write-Host "  CODE_SIGNING_PFX_PASSWORD = $Password   (anche in ${passwordPath}, copiala da li')"
Write-Host ''
Write-Host 'PASSAGGIO 2 — rendi il certificato attendibile sui PC (per-utente, senza admin):' -ForegroundColor Cyan
Write-Host "  certutil -addstore -user Root `"$cerPath`""
Write-Host "  certutil -addstore -user TrustedPublisher `"$cerPath`""
Write-Host '  (per l''intero PC, da prompt con privilegi elevati: senza -user)'
Write-Host ''
Write-Host 'PASSAGGIO 3 — elimina la cartella codesign dopo aver copiato i secret:' -ForegroundColor Cyan
Write-Host '  Remove-Item -Recurse -Force .\codesign'
Write-Host ''
Write-Host 'La cartella codesign è già esclusa da git: non committare mai il .pfx.' -ForegroundColor Yellow
