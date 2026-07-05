# Triar — Sistema de Triagens em Saúde

Aplicativo multiplataforma (celular e computador) para aplicação de triagens em saúde,
com perguntas de **sim/não com pesos configuráveis** e **faixas de resultado personalizáveis**.

## Estrutura

```
TriarCompleto/
├── Triagem.App/MauiApp3/      # App .NET MAUI (Android, iOS, Windows, macOS)
├── Triagem.API/Triagem.API/   # API ASP.NET Core (.NET 10)
├── database/script.sql        # Script de referência do banco (SQL Server)
├── deploy/nginx/nginx.conf    # Load balancer (nginx)
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
- **Segurança**: senhas com PBKDF2 (SHA-256, 100 mil iterações, salt aleatório), validação de autoria nas triagens personalizadas.

## Como rodar

### Opção 1 — Docker (recomendada: sobe tudo)

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
| POST | `/api/auth/register` | Cadastro |
| POST | `/api/auth/login` | Login |
| GET | `/api/triagens?usuarioId=` | Lista triagens do usuário |
| GET | `/api/triagens/{id}` | Perguntas (pesos) + faixas |
| POST | `/api/triagens` | Cria triagem personalizada |
| PUT | `/api/triagens/{id}` | Edita triagem própria |
| DELETE | `/api/triagens/{id}?usuarioId=` | Remove triagem própria |
| POST | `/api/triagens/{id}/responder` | Calcula e grava o resultado |
| GET | `/api/triagem/usuario/{id}` | Histórico (filtro `?triagemModeloId=`) |
| PUT | `/api/usuarios/{id}/home` | Configura a home |
| GET | `/health` | Health check |

> Projeto acadêmico: os resultados das triagens são orientativos e não substituem avaliação profissional.
