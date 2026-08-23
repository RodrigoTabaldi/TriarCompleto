<#
    Envia o .aab mais recente do Triar para o Firebase App Distribution.

    Uso:
      ./deploy/firebase/distribute.ps1 -AppId "1:123:android:abc" -Grupo "testers"

    Pré-requisitos: Firebase CLI instalada e logada (firebase login),
    e um .aab de Release já gerado (veja README.md).
#>
param(
    [Parameter(Mandatory = $true)] [string] $AppId,
    [string] $Grupo = "testers",
    [string] $NotasArquivo = "deploy/firebase/release-notes.txt"
)

$ErrorActionPreference = "Stop"

$publishDir = "Triagem.App/MauiApp3/bin/Release/net10.0-android/publish"
$aab = Get-ChildItem -Path $publishDir -Filter "*-Signed.aab" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($null -eq $aab) {
    Write-Error "Nenhum .aab assinado encontrado em '$publishDir'. Rode o build de Release primeiro (veja README.md)."
}

Write-Host "Distribuindo: $($aab.FullName)" -ForegroundColor Cyan

firebase appdistribution:distribute $aab.FullName `
    --app $AppId `
    --groups $Grupo `
    --release-notes-file $NotasArquivo

if ($LASTEXITCODE -eq 0) {
    Write-Host "OK! Build enviado ao Firebase App Distribution." -ForegroundColor Green
} else {
    Write-Error "Falha ao distribuir (exit $LASTEXITCODE)."
}
