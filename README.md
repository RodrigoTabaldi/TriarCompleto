# Triar — Sistema de Triagens em Saúde

Aplicativo MAUI e site separado em React + TypeScript, com API ASP.NET Core.

## Modos na tela inicial do site

- **Individual:** sem login, SQLite real no navegador, sem envio de dados à API. Histórico e triagens persistem no IndexedDB. Há cópia do banco em Sobre. Limpar os dados do navegador apaga esse banco.
- **Em grupo:** cadastro/login, API ASP.NET e PostgreSQL, preparado para futura hospedagem compartilhada em nuvem. Não há sincronização automática dos bancos individuais.

## Executar

Mantenha o `.env` existente ou configure `.env.example` com `JWT_KEY` aleatória de pelo menos 32 caracteres e `POSTGRES_PASSWORD`.

```sh
docker compose up -d --build --wait
```

Site: http://localhost:8080. API MAUI: http://localhost:5036. Esse Compose oferece **os dois modos** na tela inicial, com PostgreSQL, Redis, duas APIs e nginx. Não use `down -v` para preservar os bancos.

Após o primeiro carregamento completo, a versão de produção guarda os arquivos para o modo individual offline. Publicação remota requer HTTPS. O aplicativo MAUI é instalado nos dispositivos; o Docker executa o site e os serviços.

## Estrutura

- `Triagem.App/MauiApp3`: aplicativo existente, preservado.
- `Triagem.Web`: site, SQLite local, telas responsivas e testes Chromium/WebKit.
- `Triagem.API/Triagem.API`: API .NET 10, PostgreSQL, autenticação JWT e isolamento por usuário.
- `docker-compose.yml`: implantação principal.
- `docker-compose.individual.yml`: alternativa especial com SQLite no servidor; não é necessária para o modo Individual do navegador.
- `database/script.sql`: referência histórica de SQL Server.

O site utiliza os sete protocolos e os recursos visuais do MAUI atualizado. Oferece perguntas sim/não e múltiplas opções, cadastro do paciente, resultado, histórico, Excel e criação/edição de triagens. A atualização do esquema da API adiciona os novos campos sem apagar históricos existentes.

Instruções de desenvolvimento, testes, limites do armazenamento local e geração do catálogo: [Triagem.Web/README.md](Triagem.Web/README.md).

> Projeto acadêmico: resultados orientativos, que não substituem avaliação profissional.
