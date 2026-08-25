# Triar — Sistema de Triagens em Saúde

Aplicativo multiplataforma (celular e computador) para aplicação de triagens em saúde,
com perguntas de **sim/não com pesos configuráveis** e **faixas de resultado personalizáveis**.

## Estrutura

```
TriarCompleto/
├── Triagem.App/MauiApp3/      # App .NET MAUI (Android, iOS, Windows, macOS)
├── Triagem.API/Triagem.API/   # API ASP.NET Core (.NET 10)
├── Triagem.Core/              # Regras e criptografia compartilhadas entre API e app
├── Triagem.API.Tests/         # Testes automatizados (xUnit) da API
├── Triagem.Core.Tests/        # Testes das regras compartilhadas e da criptografia
├── database/script.sql        # Script de referência do banco (SQL Server)
├── deploy/nginx/nginx.conf    # Load balancer (nginx, HTTP — uso local/dev)
├── deploy/nginx/nginx.conf.prod.example  # Referência de config com TLS para produção
├── .github/workflows/ci.yml   # CI: build + testes da API a cada push/PR
└── docker-compose.yml         # Sobe SQL Server + Redis + 2 APIs + load balancer
```

## Arquitetura

```
App MAUI ──► nginx (load balancer :5036) ──► api1 / api2 (ASP.NET Core)
                                                  │        │
                                                  ▼        ▼
                                           SQL Server    Redis
                                             2022       (cache)
```

- **Cache**: em camadas — **Redis** como cache distribuído (compartilhado entre api1/api2, invalidação por versão) quando `ConnectionStrings:Redis` está configurada; o docker-compose já sobe o Redis. Sem Redis (ex.: rodando local com LocalDB), a API cai automaticamente para cache em memória. O app MAUI ainda tem cache local próprio (5–10 min) para reduzir chamadas à API.
- **Rate limiting**: política geral (100 req/10s por IP), política de autenticação anti força-bruta (10 req/min por IP) e limite global (300 req/10s), além do rate limit de borda no nginx (30 r/s).
- **Load balancer**: nginx com `least_conn`, health-based failover (`proxy_next_upstream`) e 2 instâncias da API.
- **Resiliência**: `EnableRetryOnFailure` no EF Core, retry de inicialização aguardando o SQL Server, health checks em `/health`.
- **Segurança**:
  - **Autenticação JWT** — login/cadastro emitem um token; **todos** os endpoints de dados exigem `Authorization: Bearer <token>`. A identidade do usuário vem sempre do token, nunca de um `usuarioId` enviado pelo cliente (fecha IDOR) — inclusive na leitura de detalhe de uma triagem (`GET /api/triagens/{id}`), que só retorna triagens padrão do sistema ou criadas pelo próprio usuário autenticado.
  - Senhas com PBKDF2 (SHA-256, 100 mil iterações, salt aleatório); mínimo de 8 caracteres.
  - **Dados clínicos criptografados em repouso** (AES-256-GCM, chave em `DataProtection:Key`) — nome, idade, sexo, pontuação, classificação, recomendação e respostas das novas gravações ficam em envelopes protegidos. Registros antigos são migrados em lotes na inicialização.
  - **Segredos fora do código**: senha do banco, chave JWT e chave de criptografia vêm de variáveis de ambiente (`.env` no Docker), nunca versionadas.
  - **CORS restrito** por lista de origens (`Cors:AllowedOrigins`) e `X-Forwarded-For` aceito só de proxies confiáveis (evita spoof do rate limit).
  - **SQL Server e Redis não publicam porta no host** no docker-compose — só nginx expõe a porta pública; os demais serviços só são alcançáveis pela rede interna do compose.
  - Validação de autoria nas triagens personalizadas.
  - **Histórico paginado** (`pagina`/`tamanhoPagina`, teto de 200 itens por página) — evita que a consulta cresça sem limite conforme o histórico do usuário aumenta.
  - **Sessão persistida no app** via SecureStorage (Keychain/Keystore/DPAPI conforme a plataforma) — o usuário não precisa logar de novo a cada abertura.

## Como rodar

### Opção 0 — APK de demonstração (Android, sem API e sem internet)

Para demonstrar o app sem subir nada: `publish/Triar-demo-1.0.apk`.

É um build de Release compilado com `-p:TriarModoLocal=true`, que troca a API por um
**banco SQLite dentro do próprio aparelho** (`Services/BancoLocal.cs`). Cadastro, login,
catálogo de triagens, execução com pesos e faixas, histórico e configuração da home
funcionam offline; os dados ficam só no aparelho e somem se o app for desinstalado.

- **Instalação**: copie o `.apk` para o celular e abra-o. O Android pedirá para permitir
  a instalação de fontes desconhecidas (é um APK assinado fora da Play Store).
- **Requisitos**: Android 5.0 (API 21) ou superior, arquitetura **arm64** (todo celular
  atual) — o pacote traz `arm64-v8a` e `x86_64` (este último para emulador).
- **Conta pronta**: `demo@triar.com` / `triar1234`, que já vem com histórico de exemplo.
  Também dá para criar uma conta nova na hora pelo próprio app.

Para gerar de novo (o keystore de demonstração está em `deploy/firebase/`, fora do
controle de versão):

```bash
export TRIAR_KEYSTORE=".../deploy/firebase/triar-demo.keystore"
export TRIAR_KEY_ALIAS=triar-demo TRIAR_KEY_PASS=... TRIAR_STORE_PASS=...
dotnet publish Triagem.App/MauiApp3/MauiApp3.csproj -f net10.0-android -c Release \
  -p:TriarModoLocal=true -p:AndroidPackageFormat=apk
```

> Sem `-p:TriarModoLocal=true` nada muda: o app continua consumindo a Triagem.API
> normalmente, como descrito nas opções abaixo. A chave em `deploy/firebase/` serve só
> para a demonstração — para publicar na Play Store, gere uma chave própria com senha
> secreta, porque a chave de assinatura de um app publicado não pode ser trocada depois.

### Opção 1 — Docker (recomendada: sobe tudo)

Primeiro crie o arquivo de segredos a partir do exemplo e preencha os valores:

```bash
cp .env.example .env
# edite .env: defina SA_PASSWORD (senha forte), JWT_KEY e DATA_PROTECTION_KEY
# (>= 32 caracteres cada, aleatórias e DIFERENTES entre si)
```

Depois suba tudo:

```bash
docker compose up -d --build
```

A API fica em `http://localhost:5036` (mesma porta que o app usa). O banco é criado
e populado com 6 triagens padrão automaticamente na primeira execução.

### Opção 2 — Sem Docker (SQL Server LocalDB)

Não precisa instalar nada além do Visual Studio: o **SQL Server LocalDB** já vem
com a carga de trabalho ".NET desktop" / "ASP.NET" do Visual Studio.

1. Confirme que o LocalDB está disponível (no terminal):
   ```bash
   sqllocaldb info MSSQLLocalDB
   ```
   Se não existir, crie e inicie:
   ```bash
   sqllocaldb create MSSQLLocalDB
   sqllocaldb start MSSQLLocalDB
   ```
2. Rode a API (o `appsettings.Development.json` já aponta para o LocalDB —
   o banco `TriarDb` é criado e populado sozinho na primeira execução):
   ```bash
   cd Triagem.API/Triagem.API
   dotnet run
   ```
   A API sobe em `http://localhost:5036`. Teste em `http://localhost:5036/health`.
3. Com a API rodando, rode o app MAUI pelo Visual Studio (projeto `MauiApp3`),
   escolhendo Windows ou Android.
   - No emulador Android a API é acessada via `10.0.2.2:5036` (já configurado no `ApiService`).

### Opção 3 — SQL Server próprio

Se você já tem um SQL Server instalado (Express ou completo), ajuste a
`ConnectionStrings:DefaultConnection` no `appsettings.Development.json` para o
seu servidor e rode a API com `dotnet run`.

## Publicar em produção (nuvem)

**Banco no Azure SQL + API no Render** — passo a passo completo em
[`deploy/render/README.md`](deploy/render/README.md), e a infraestrutura da API
descrita como código em [`render.yaml`](render.yaml).

O projeto já vem preparado para isso:

- A API honra a variável `PORT` injetada pela plataforma.
- Dois endpoints de saúde separados: **`/health/live`** (não toca o banco — é o que o
  Render monitora) e **`/health`** (consulta o banco, para diagnóstico sob demanda).
  A separação existe porque uma conexão ao Azure SQL serverless impede o auto-pause,
  e um monitor periódico no endpoint errado consumiria a cota mensal em poucos dias.
- `Database:SeedOnStartup` permite desligar a criação/seed do banco depois do primeiro
  deploy, para que acordar a API não acorde o banco junto.
- `ForwardedHeaders:TrustPlatformProxy` faz o rate limit por IP funcionar atrás do
  proxy da plataforma sem abrir espaço para spoof do `X-Forwarded-For`.
- OpenAPI fica restrito a Development; a imagem Docker roda como usuário sem
  privilégios e usa restore com lock file.

## Funcionalidades

- **Login / cadastro** de usuários.
- **Home dinâmica e responsiva** (1 coluna no celular, 2–3 no computador) com as triagens padrão
  e as criadas pelo usuário; botão **Editar home** para escolher quais triagens aparecem.
- **6 triagens padrão**: Saúde Mental, Saúde Infantil, Saúde da Mulher, Saúde do Idoso,
  Respiratória e Clínica Geral (10 perguntas cada).
- **Criar sua triagem**: perguntas sim/não com peso configurável e faixas de resultado
  (metas) com título, intervalo de pontuação e recomendação. Pode editar e excluir depois.
- **Execução da triagem** com dados do paciente, barra de progresso e validação.
- **Tela de resultado** com pontuação, classificação colorida, recomendação e botão
  para **aplicar a mesma triagem em outra pessoa**.
- **Histórico por triagem** com exportação para Excel.

## Endpoints principais da API

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/auth/register` | Cadastro (retorna token JWT) |
| POST | `/api/auth/login` | Login (retorna token JWT) |
| GET | `/api/triagens` | Lista triagens do usuário autenticado |
| GET | `/api/triagens/{id}` | Perguntas (pesos) + faixas |
| POST | `/api/triagens` | Cria triagem personalizada |
| PUT | `/api/triagens/{id}` | Edita triagem própria |
| DELETE | `/api/triagens/{id}` | Remove triagem própria |
| POST | `/api/triagens/{id}/responder` | Calcula e grava o resultado |
| GET | `/api/triagens/{id}/historico` | Histórico de uma triagem (paginado: `?pagina=&tamanhoPagina=`, máx. 200/página) |
| GET | `/api/triagem/usuario/{id}` | Histórico do usuário autenticado (filtro `?triagemModeloId=`, paginado: `?pagina=&tamanhoPagina=`) |
| PUT | `/api/usuarios/home` | Configura a home |
| GET | `/health/live` | Liveness público, sem consultar o banco |
| GET | `/health` | Readiness do banco (exige autenticação) |

> Exceto `/api/auth/*` e `/health`, **todos os endpoints exigem** o cabeçalho
> `Authorization: Bearer <token>`. O usuário é sempre o dono do token, nunca de um id
> na rota ou na query — por isso rotas como `/api/usuarios/home` e `/api/triagens` nem
> aceitam mais um `usuarioId`, e `GET /api/triagens/{id}` só retorna triagens padrão do
> sistema ou pertencentes ao próprio usuário do token.

> Projeto acadêmico: os resultados das triagens são orientativos e não substituem avaliação profissional.

## Testes

A API tem uma suíte de testes automatizados (`Triagem.API.Tests`, xUnit) em duas
camadas: testes de unidade contra `TriagemService` — validação de modelo, cálculo de
pontuação e classificação, autorização de escrita (editar/excluir só pelo criador),
paginação do histórico e, especificamente, um teste de regressão para a restrição de
acesso de `ObterDetalheAsync` (garante que uma triagem privada de um usuário não é
visível para outro) — além de `PasswordHasher`, `TokenService`,
`ClaimsPrincipalExtensions` e `FieldEncryptionService`; e testes de integração HTTP
(`Integration/`, via `WebApplicationFactory<Program>`) que sobem a API real (JWT,
rate limiting, `[Authorize]`) contra um banco EF InMemory, cobrindo registro/login e
o bloqueio de rotas autenticadas sem token. A validação de modelo de triagem em si
(título/perguntas/faixas/imagem) vive em `Triagem.Core.TriagemRules`, compartilhada
com o modo offline do app (`BancoLocal`) e testada em `Triagem.Core.Tests`. Rodar
localmente:

```bash
dotnet test Triagem.Core.Tests/Triagem.Core.Tests.csproj
dotnet test Triagem.API.Tests/Triagem.API.Tests.csproj
```

O CI também coleta cobertura, compila o alvo Android, verifica dependências, procura
segredos acidentalmente versionados e executa análise estática CodeQL. O esquema do
SQL Server é evoluído por migrations do EF Core; bancos antigos criados por
`EnsureCreated` recebem automaticamente o baseline antes das migrations seguintes.


