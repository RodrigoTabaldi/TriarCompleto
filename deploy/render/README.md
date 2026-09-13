# Deploy — Azure SQL (banco) + Render (API)

Guia para colocar a Triar API no ar. Cada passo diz **o que fazer** e **por quê**,
porque várias escolhas aqui existem para não estourar a cota gratuita nem expor
segredo.

Arquitetura final:

```
APK (celular) ──HTTPS──► Render (Triar API, container Docker)
                              │
                              └──TLS──► Azure SQL Database (serverless, free offer)
```

---

## Passo 1 — Criar o banco no Azure

No portal do Azure, **Create a resource → Azure SQL → SQL databases → Single database**.

| Campo | Valor | Motivo |
|---|---|---|
| Database name | `TriarDb` | — |
| **Apply offer** | **Free offer** (botão no topo) | Habilita 100.000 vCore-segundos + 32 GB por mês, sem prazo de validade |
| Compute tier | **Serverless** (General Purpose, Gen5) | Único tier com auto-pause; é o que zera o custo quando ninguém usa |
| Min vCores | **0,5** | É o piso cobrado enquanto o banco está ligado. Aumentar dobra a queima da cota |
| **Auto-pause delay** | **15 minutos** | O padrão é 60. Com 60, cada acesso avulso custa 1 hora de banco ligado; com 15, custa 15 min |
| **Free limit reached behavior** | **Auto-pause até o mês seguinte** | É esta opção que garante custo zero. A outra libera cobrança no cartão |
| Backup redundancy | **Locally-redundant (LRS)** | Geo-redundante exige habilitar cobrança |
| Region | **East US 2** (ou a que fizer par com a região do Render) | Latência entre API e banco |

Depois, em **Networking**:

- **Allow Azure services and resources to access this server**: *No*.
- Em **Firewall rules**, libere apenas os IPs de saída do Render (Passo 4).
- **Minimum TLS version**: 1.2.

> Deixar "Allow Azure services" ligado abre o banco para **qualquer** recurso hospedado
> no Azure, inclusive de outras contas. É um dos erros de configuração mais comuns.

Crie um login SQL dedicado para a aplicação — **não use o administrador do servidor**:

```sql
-- No banco master:
CREATE LOGIN triar_app WITH PASSWORD = 'UMA-SENHA-LONGA-E-ALEATORIA';

-- No banco TriarDb:
CREATE USER triar_app FOR LOGIN triar_app;
ALTER ROLE db_datareader ADD MEMBER triar_app;
ALTER ROLE db_datawriter ADD MEMBER triar_app;
ALTER ROLE db_ddladmin  ADD MEMBER triar_app;  -- necessário para criar as tabelas no 1º deploy
```

> `db_ddladmin` só é preciso enquanto `Database__SeedOnStartup=true`. Depois do Passo 6
> você pode removê-lo (`ALTER ROLE db_ddladmin DROP MEMBER triar_app;`) e a aplicação
> segue funcionando com permissão apenas de leitura e escrita de dados — princípio do
> menor privilégio.

---

## Passo 2 — Gerar os segredos

Três valores, **todos diferentes entre si**, com no mínimo 32 caracteres:

```bash
openssl rand -base64 48   # Jwt__Key
openssl rand -base64 48   # DataProtection__Key
```

- **`Jwt__Key`** — assina os tokens de sessão. Quem tiver essa chave emite token de
  qualquer usuário. A API se recusa a subir se ela tiver menos de 32 caracteres.
- **`DataProtection__Key`** — criptografa os dados clínicos em repouso (AES-256-GCM).
  Guarde uma cópia num cofre de segredos separado do backup do banco.
- **`DataProtection__KeyId`** — identificador não secreto da chave atual (por exemplo,
  `primary` ou `2026-09`). O identificador é gravado no envelope criptográfico.
- **`ConnectionStrings__DefaultConnection`** — a string do Passo 3.

Nunca coloque nenhum dos três em arquivo versionado.

---

## Passo 3 — Montar a connection string

```
Server=tcp:SEU-SERVIDOR.database.windows.net,1433;Initial Catalog=TriarDb;User ID=triar_app;Password=SUA-SENHA;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;
```

Dois parâmetros não são opcionais:

- **`Connection Timeout=60`** — o padrão é 15 s, e um banco em auto-pause leva cerca de
  1 minuto para retomar. Com 15 s, o primeiro acesso depois de cada pausa falharia.
- **`Encrypt=True;TrustServerCertificate=False`** — exige TLS **e** valida o certificado
  do servidor. `TrustServerCertificate=True` aceitaria certificado forjado e abriria
  espaço para interceptação entre o Render e o Azure.

Não defina `Min Pool Size`. O padrão é 0; qualquer valor acima disso mantém sessões
abertas permanentemente, o banco **nunca** entra em auto-pause e a cota do mês evapora
em dois dias.

---

## Passo 4 — Criar o serviço no Render

No painel do Render: **New → Blueprint**, apontando para este repositório. Ele lê o
`render.yaml` da raiz e já configura runtime, Dockerfile, região e health check.

O Render vai pedir os três valores marcados como `sync: false`:

| Variável | Valor |
|---|---|
| `ConnectionStrings__DefaultConnection` | a string do Passo 3 |
| `Jwt__Key` | o segredo do Passo 2 |
| `DataProtection__Key` | o outro segredo do Passo 2 |
| `DataProtection__KeyId` | `primary` no primeiro deploy |

> O duplo sublinhado (`__`) é como o .NET traduz hierarquia de configuração em
> variável de ambiente: `ConnectionStrings__DefaultConnection` corresponde a
> `ConnectionStrings:DefaultConnection` do `appsettings.json`.

### Rotação e recuperação da chave clínica

Nunca substitua `DataProtection__Key` sem preservar a chave anterior. Para rotacionar:

1. copie a chave atual para `DataProtection__PreviousKeys__ID_ANTIGO`;
2. configure uma nova `DataProtection__Key` e um novo `DataProtection__KeyId`;
3. faça o deploy e valide a leitura de registros antigos e novos;
4. mantenha a chave anterior enquanto existir registro cifrado com seu identificador.

Um backup recuperável é composto pelo banco **e** pelas versões de chave necessárias,
guardados em controles separados. Teste a restauração em ambiente isolado.

Feito o primeiro deploy, copie os **IPs de saída** do serviço (Render → seu serviço →
Connect → Outbound) e cadastre-os no firewall do Azure SQL (Passo 1).

---

## Passo 5 — Verificar

```bash
curl https://SEU-SERVICO.onrender.com/               # {"servico":"Triar API","status":"online"}
curl https://SEU-SERVICO.onrender.com/health/live    # Healthy — não toca o banco
curl https://SEU-SERVICO.onrender.com/health         # Healthy — este consulta o banco
```

Confira também que a documentação **não** está exposta:

```bash
curl -i https://SEU-SERVICO.onrender.com/openapi/v1.json   # deve dar 404 em Production
```

E que o rate limit enxerga o IP real de cada cliente — 15 requisições rápidas ao
endpoint de login devem começar a devolver `429`:

```bash
for i in $(seq 1 15); do
  curl -s -o /dev/null -w "%{http_code} " -X POST \
    https://SEU-SERVICO.onrender.com/api/auth/login \
    -H "Content-Type: application/json" -d '{"email":"x@x.com","senha":"errada"}'
done
```

> Se **nenhuma** devolver 429, o `X-Forwarded-For` não está sendo aceito e todos os
> clientes estão caindo no mesmo balde (ou em baldes errados). Revise
> `ForwardedHeaders__TrustPlatformProxy=true`.

---

## Passo 6 — Inicialização do banco

Migrations e seed são independentes:

```
Database__MigrateOnStartup = true
Database__SeedOnStartup = true
```

Esses valores priorizam consistência: toda versão aplica migrations e sincroniza o
catálogo antes de aceitar requisições. Em produção com um job de migration separado,
ambos podem ser desligados no processo web depois que o job concluir. Enquanto
`MigrateOnStartup=true`, mantenha a permissão de DDL do usuário da aplicação.

---

## Passo 7 — Apontar o app para a API

Passe a URL ao build; se esquecer, o Release **falha de propósito** antes de gerar o
pacote. Gere o APK — **sem** a
flag `-p:TriarModoLocal=true`, que é a do APK de demonstração offline:

```bash
dotnet publish Triagem.App/MauiApp3/MauiApp3.csproj -f net10.0-android -c Release \
  -p:AndroidPackageFormat=apk -p:TriarApiBaseUrl=https://SEU-SERVICO.onrender.com
```

---

## Operação do dia a dia

**Antes de uma apresentação**, abra `https://SEU-SERVICO.onrender.com/` uns 2 minutos
antes. Isso acorda a API (~1 min) sem acordar o banco. O banco acorda sozinho no
primeiro login, em cerca de mais 1 minuto.

**Nunca** configure um monitor externo (UptimeRobot e similares) apontando para
`/health` — além de exigir autenticação, ele consulta o banco a cada checagem e mantém
o Azure SQL ligado 24/7. Para monitoramento público, use `/health/live`.

**Acompanhe a cota** no portal do Azure, na métrica **"Free amount remaining"** do
banco. Se ela cair rápido demais, o Activity Log mostra, nas operações
`Resume Databases`, a propriedade `Caller` — é assim que se descobre quem está
acordando o banco (costuma ser um monitor esquecido ou uma janela do SSMS aberta).

**Ao inspecionar dados** pelo SSMS ou Azure Data Studio, **feche a conexão ao terminar**.
Uma sessão aberta impede o auto-pause pela noite inteira.

---

## Evolução do banco

O esquema é versionado por **EF Core Migrations** em `Data/Migrations`. Bancos antigos
criados com `EnsureCreated` são detectados e recebem o baseline automaticamente, sem
apagar dados. `Database__MigrateOnStartup` controla somente o esquema;
`Database__SeedOnStartup` controla o backfill clínico e o catálogo. Nenhuma alternância
manual é necessária com os valores do Blueprint.
