-- =====================================================================
-- Cria o login/usuário dedicado da API (triar_app), com o mínimo de
-- permissão necessário — nunca 'sa' — usado apenas pelo serviço de
-- inicialização do docker-compose (ver docker-compose.yml, serviço
-- "db-init"). Idempotente: seguro rodar de novo a cada `docker compose up`.
--
-- Escopo das permissões, todas restritas ao banco TriarDb:
--   db_datareader / db_datawriter — uso normal (CRUD) da API.
--   db_ddladmin                   — a API aplica EF Migrations (CREATE/ALTER
--                                   TABLE) na própria inicialização (ver
--                                   DbSeeder.SeedAsync); sem este papel, o
--                                   primeiro deploy e qualquer atualização
--                                   de schema falhariam.
-- Nada além disso: sem sysadmin, sem acesso a master/msdb/outras bases, sem
-- permissão para criar logins ou alterar configuração do servidor — ao
-- contrário de 'sa', que a API usava antes e que herda controle total da
-- instância caso a aplicação seja comprometida.
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'triar_app')
BEGIN
    CREATE LOGIN [triar_app] WITH PASSWORD = N'$(AppDbPassword)', CHECK_POLICY = ON;
END
GO

IF DB_ID(N'TriarDb') IS NULL
    CREATE DATABASE TriarDb;
GO

USE TriarDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'triar_app')
BEGIN
    CREATE USER [triar_app] FOR LOGIN [triar_app];
END
GO

ALTER ROLE db_datareader ADD MEMBER [triar_app];
ALTER ROLE db_datawriter ADD MEMBER [triar_app];
ALTER ROLE db_ddladmin ADD MEMBER [triar_app];
GO
