# Triar Web

Site separado em React + TypeScript, com API ASP.NET Core. A tela inicial permite escolher o modo, como no MAUI atualizado. Logo, fontes, ícones, ilustrações, cores, barra lateral e navegação mobile foram reproduzidos a partir das páginas e recursos do aplicativo.

## Modos

- **Individual:** entra sem login. Um banco SQLite real, executado com sql.js, fica no navegador e é persistido no IndexedDB. Pacientes, respostas, triagens particulares e histórico não são enviados à API. Em Sobre, use **Baixar cópia do SQLite** para guardar uma cópia. Limpar os dados do navegador apaga esse banco; dispositivos e navegadores diferentes possuem bancos separados.
- **Em grupo:** exige cadastro/login e usa a API ASP.NET com PostgreSQL. Os dados são associados ao usuário. O token fica em memória; recarregar retorna à escolha e exige novo login nesse modo.

Há sete protocolos padrão do catálogo MAUI atualizado, perguntas sim/não e múltiplas opções, escolaridade, doenças prévias, resultados, histórico, exportação Excel e edição de triagens particulares. Protocolos antigos permanecem no histórico.

## Docker

Na raiz, mantenha o `.env` existente ou copie `.env.example` e configure `JWT_KEY` aleatória de pelo menos 32 caracteres e `POSTGRES_PASSWORD`.

```sh
docker compose up -d --build --wait
```

Abra http://localhost:8080. **Esse único comando oferece os dois modos na tela inicial.** A API para o MAUI fica em http://localhost:5036. PostgreSQL e Redis usam volumes persistentes; não execute `down -v` para preservar os bancos.

O Compose alternativo `docker-compose.individual.yml` configura SQLite **no servidor da API**, para instalações especiais sem PostgreSQL. Ele não é necessário para escolher Individual no site e não representa o modo em grupo com PostgreSQL. Usa as mesmas portas do Compose principal.

## Offline e nuvem

Depois de carregar os arquivos do site pela primeira vez, o service worker guarda a aplicação para uso individual offline. A publicação remota precisa de HTTPS; localhost também permite esse recurso. O modo em grupo precisa de conexão com a API. SQLite local não é sincronizado automaticamente com PostgreSQL. A futura nuvem hospedará site/API e banco compartilhado; domínio, HTTPS e backups ainda precisam ser configurados.

## Desenvolvimento

```sh
cd Triagem.Web
npm ci
npm run dev
```

Abra http://localhost:5173. O proxy `/api` aponta para localhost:5036. O modo individual funciona sem API. Para o grupo, configure conexão PostgreSQL e chave JWT e execute, na raiz:

```sh
dotnet run --project Triagem.API/Triagem.API --urls http://localhost:5036
```

`npm run build` verifica TypeScript, gera os arquivos de produção e o cache offline. O service worker é habilitado apenas na versão de produção.

## Verificação

Com o site Docker ativo:

```sh
cd Triagem.Web
npm exec -- playwright install chromium webkit
npm run test:e2e
```

Os testes verificam escolha inicial, login obrigatório no grupo, SQLite persistente, cálculo de opções, XLSX, cópia SQLite, ausência de chamadas à API no individual, layout mobile e falha explícita quando o armazenamento é bloqueado.

Para integração da API, use banco de testes: o script cria usuários e registros fictícios.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-web-api.ps1
```

O catálogo web deriva de `Triagem.API/Triagem.API/Data/DefaultTriageCatalog.cs`. Para regenerar seu JSON, na raiz:

```sh
dotnet run --project scripts/ExportCatalog -- Triagem.Web/src/default-triages.json
```

O cliente MAUI deste checkout foi preservado. A referência visual e de protocolos utilizada é o MAUI atualizado em `C:\Users\rodri\source\repos\RodrigoTabaldi\TriarCompleto`.
