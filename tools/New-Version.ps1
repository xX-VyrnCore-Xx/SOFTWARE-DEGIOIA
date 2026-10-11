# Prepara una nuova versione di PresenzeFlow aggiornando TUTTI i punti in cui
# compare il numero di versione. Va eseguito dalla root del repository.
#
# Uso:
#   .\tools\New-Version.ps1 -Version 1.7.1
#   git commit -am "Rilascio 1.7.1"
#   git tag v1.7.1
#   git push origin main --follow-tags
#
# Il push su main (o del tag v1.7.1) fa partire il workflow .github/workflows/release.yml,
# che compila l'app, costruisce l'installer e crea la Release GitHub con l'allegato.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$csprojPath = Join-Path (Join-Path $root 'PresenzeXmlConverter') 'PresenzeXmlConverter.csproj'
$installerPath = Join-Path $root 'Installer.iss'
$assemblyVersion = "$Version.0"

function Set-FirstMatch {
    param([string]$Path, [string]$Pattern, [string]$Replacement, [string]$Label)
    $content = Get-Content -Path $Path -Raw
    $updated = ([regex]::new($Pattern)).Replace($content, $Replacement, 1)
    if ($updated -eq $content) {
        throw "Voce non aggiornata (${Label}) in ${Path}: controlla il file."
    }
    Set-Content -Path $Path -Value $updated -NoNewline
}

Set-FirstMatch $csprojPath '<Version>[^<]*</Version>' "<Version>$Version</Version>" 'Version'
Set-FirstMatch $csprojPath '<AssemblyVersion>[^<]*</AssemblyVersion>' "<AssemblyVersion>$assemblyVersion</AssemblyVersion>" 'AssemblyVersion'
Set-FirstMatch $csprojPath '<FileVersion>[^<]*</FileVersion>' "<FileVersion>$assemblyVersion</FileVersion>" 'FileVersion'
Set-FirstMatch $csprojPath '<InformationalVersion>[^<]*</InformationalVersion>' "<InformationalVersion>$Version</InformationalVersion>" 'InformationalVersion'
Set-FirstMatch $installerPath '#define AppVersion "[^"]*"' "#define AppVersion `"$Version`"" 'AppVersion'

Write-Host "Versione aggiornata a $Version (csproj + Installer.iss)." -ForegroundColor Green
Write-Host ''
Write-Host 'Prossimi passi:' -ForegroundColor Cyan
Write-Host "  git commit -am `"Rilascio $Version`""
Write-Host "  git tag v$Version"
Write-Host '  git push origin main --follow-tags'
Write-Host ''
Write-Host 'La Release GitHub con l''installer verrà creata automaticamente dal workflow "Release".'
