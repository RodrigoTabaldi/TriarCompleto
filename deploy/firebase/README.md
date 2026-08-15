# Publicar o Triar no Firebase (App Distribution)

Este guia leva o app **Triar (Android)** para o Firebase App Distribution — de onde
você manda um link de instalação para os testadores no celular.

> O Firebase distribui o **app**. Ele **não hospeda a sua API .NET**. Antes de
> distribuir para celulares reais, a API precisa estar num endereço **público HTTPS**
> e o app precisa apontar para ele (veja o passo 0).

---

## Pré-requisitos (uma vez)

1. **Node.js** instalado e a **Firebase CLI**:
   ```bash
   npm install -g firebase-tools
   firebase login
   ```
2. Um **projeto no Firebase** (console.firebase.google.com) com um **app Android**
   cujo pacote seja **`com.triar.app`** (o mesmo `ApplicationId` do projeto).
   Anote o **App ID** do Firebase (formato `1:1234567890:android:abcdef...`).
3. **JDK** no PATH (o `keytool` vem com ele) e a carga **.NET MAUI** no Visual Studio.

---

## Passo 0 — Apontar o app para a API pública

Num celular real, `localhost` é o próprio telefone. Então:

1. Hospede a API .NET num lugar público com **HTTPS** (Azure App Service, Render,
   uma VPS com o `docker-compose.yml` deste repositório + um proxy TLS, etc.).
2. Em `Triagem.App/MauiApp3/Services/ApiService.cs`, troque a constante:
   ```csharp
   private const string UrlProducao = "https://SUA-API-DE-PRODUCAO.com";
   ```
   pela URL real. Em builds de **Release** o app usa essa URL automaticamente.

> Use **HTTPS**. O Android bloqueia tráfego HTTP em texto puro em builds de produção.

---

## Passo 1 — Criar o keystore de assinatura (uma vez)

O `.aab` precisa ser assinado. Gere um keystore e **guarde-o em segurança** (se perder,
não consegue mais atualizar o app):

```bash
keytool -genkeypair -v -keystore triar.keystore -alias triar \
  -keyalg RSA -keysize 2048 -validity 10000
```

Guarde a senha do store, o alias (`triar`) e a senha da chave.
**Nunca** comite o `.keystore` (já está no `.gitignore`).

---

## Passo 2 — Gerar o `.aab` assinado

No terminal, dentro de `Triagem.App/MauiApp3`, informe os segredos por variáveis de
ambiente e compile em Release:

**PowerShell (Windows):**
```powershell
$env:TRIAR_KEYSTORE = "C:\caminho\triar.keystore"
$env:TRIAR_KEY_ALIAS = "triar"
$env:TRIAR_STORE_PASS = "SUA_SENHA_STORE"
$env:TRIAR_KEY_PASS   = "SUA_SENHA_CHAVE"

dotnet publish -f net10.0-android -c Release
```

O pacote sai em:
`Triagem.App/MauiApp3/bin/Release/net10.0-android/publish/com.triar.app-Signed.aab`

> Alternativa: no Visual Studio, botão direito no projeto → **Publish…** →
> **Android** → selecione o keystore e gere o `.aab`.

---

## Passo 3 — Enviar para o Firebase App Distribution

Use o script pronto (ele acha o `.aab` mais recente) ou o comando direto:

**Script (recomendado):**
```powershell
./deploy/firebase/distribute.ps1 -AppId "1:1234567890:android:abcdef" -Grupo "testers"
```

**Comando direto:**
```bash
firebase appdistribution:distribute \
  "Triagem.App/MauiApp3/bin/Release/net10.0-android/publish/com.triar.app-Signed.aab" \
  --app "1:1234567890:android:abcdef" \
  --groups "testers" \
  --release-notes-file "deploy/firebase/release-notes.txt"
```

No console do Firebase → **App Distribution**, crie o grupo `testers` e adicione os
e-mails. Cada testador recebe um convite e instala pelo link.

---

## Atualizar versões

A cada envio, incremente em `Triagem.App/MauiApp3/MauiApp3.csproj`:
```xml
<ApplicationDisplayVersion>1.1</ApplicationDisplayVersion>  <!-- versão visível -->
<ApplicationVersion>2</ApplicationVersion>                  <!-- inteiro, sempre subindo -->
```

## Quando for para a Google Play

O mesmo `.aab` assinado serve para a Play Console. Recomendado usar a **Play App Signing**.
