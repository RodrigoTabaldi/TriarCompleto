$ErrorActionPreference = 'Stop'
$taskProject = Join-Path $PSScriptRoot '../../Triagem.App/MauiApp3/MauiApp3.csproj'

# Mesmo servidor de desenvolvimento e assinatura do APK de teste anterior.
# Reconstrói com o runtime padrão, sem misturar arquivos do antigo perfil AOT.
# Este APK serve para teste funcional; não comprova desempenho em produção.
dotnet build $taskProject -f net10.0-android -t:Rebuild
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$taskApk = Join-Path $PSScriptRoot '../../Triagem.App/MauiApp3/bin/Debug/net10.0-android/com.triar.app-Signed.apk'
Get-Item -LiteralPath $taskApk | Select-Object FullName, Length, LastWriteTime
