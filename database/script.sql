-- =====================================================================
-- Triar - Script de criação do banco (SQL Server)
--
-- Gerado a partir das EF Migrations (Triagem.API/Triagem.API/Data/Migrations) —
-- NÃO edite este arquivo à mão. As migrations são a única fonte de verdade do
-- schema; um script mantido manualmente à parte diverge do modelo com o tempo
-- (foi o que aconteceu com a versão anterior deste arquivo: a coluna
-- NomePaciente ficava em NVARCHAR(150) aqui enquanto o modelo já exigia 500
-- para acomodar o nome cifrado, e um nome com mais de ~84 caracteres
-- estourava essa coluna).
--
-- Para regenerar após uma nova migration:
--   dotnet ef migrations script --idempotent -o database/script.sql \
--     --project Triagem.API/Triagem.API/Triagem.API.csproj \
--     --startup-project Triagem.API/Triagem.API/Triagem.API.csproj
-- (o `dotnet-ef` já está disponível como ferramenta local do repositório —
-- `dotnet tool restore` na raiz caso o comando não seja encontrado.)
--
-- Observação: a API aplica estas mesmas migrations automaticamente na
-- inicialização (ver DbSeeder.SeedAsync). Este script existe para quem
-- preferir criar/atualizar o banco manualmente, fora da API.
-- =====================================================================

IF DB_ID('TriarDb') IS NULL
    CREATE DATABASE TriarDb;
GO

USE TriarDb;
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE TABLE [Usuarios] (
        [Id] int NOT NULL IDENTITY,
        [Nome] nvarchar(120) NOT NULL,
        [Email] nvarchar(180) NOT NULL,
        [SenhaHash] nvarchar(500) NOT NULL,
        [CriadoEm] datetime2 NOT NULL,
        CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE TABLE [TriagemModelos] (
        [Id] int NOT NULL IDENTITY,
        [Titulo] nvarchar(150) NOT NULL,
        [PublicoAlvo] nvarchar(150) NOT NULL,
        [Descricao] nvarchar(600) NOT NULL,
        [Icone] nvarchar(16) NOT NULL,
        [CriadorUsuarioId] int NULL,
        [Ativa] bit NOT NULL,
        [CriadoEm] datetime2 NOT NULL,
        CONSTRAINT [PK_TriagemModelos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TriagemModelos_Usuarios_CriadorUsuarioId] FOREIGN KEY ([CriadorUsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE TABLE [FaixasResultado] (
        [Id] int NOT NULL IDENTITY,
        [TriagemModeloId] int NOT NULL,
        [Titulo] nvarchar(120) NOT NULL,
        [Recomendacao] nvarchar(600) NOT NULL,
        [PontuacaoMin] int NOT NULL,
        [PontuacaoMax] int NOT NULL,
        [Cor] nvarchar(9) NOT NULL,
        [Ordem] int NOT NULL,
        CONSTRAINT [PK_FaixasResultado] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FaixasResultado_TriagemModelos_TriagemModeloId] FOREIGN KEY ([TriagemModeloId]) REFERENCES [TriagemModelos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE TABLE [Perguntas] (
        [Id] int NOT NULL IDENTITY,
        [TriagemModeloId] int NOT NULL,
        [Texto] nvarchar(500) NOT NULL,
        [Peso] int NOT NULL,
        [Ordem] int NOT NULL,
        CONSTRAINT [PK_Perguntas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Perguntas_TriagemModelos_TriagemModeloId] FOREIGN KEY ([TriagemModeloId]) REFERENCES [TriagemModelos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE TABLE [TriagemResultados] (
        [Id] int NOT NULL IDENTITY,
        [TriagemModeloId] int NOT NULL,
        [UsuarioId] int NOT NULL,
        [NomePaciente] nvarchar(500) NOT NULL,
        [Idade] int NOT NULL,
        [Sexo] nvarchar(30) NOT NULL,
        [Pontuacao] int NOT NULL,
        [PontuacaoMaxima] int NOT NULL,
        [Classificacao] nvarchar(120) NOT NULL,
        [Recomendacao] nvarchar(600) NOT NULL,
        [Cor] nvarchar(9) NOT NULL,
        [Data] datetime2 NOT NULL,
        CONSTRAINT [PK_TriagemResultados] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TriagemResultados_TriagemModelos_TriagemModeloId] FOREIGN KEY ([TriagemModeloId]) REFERENCES [TriagemModelos] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TriagemResultados_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE TABLE [UsuarioTriagensHome] (
        [UsuarioId] int NOT NULL,
        [TriagemModeloId] int NOT NULL,
        [Visivel] bit NOT NULL,
        [Ordem] int NOT NULL,
        CONSTRAINT [PK_UsuarioTriagensHome] PRIMARY KEY ([UsuarioId], [TriagemModeloId]),
        CONSTRAINT [FK_UsuarioTriagensHome_TriagemModelos_TriagemModeloId] FOREIGN KEY ([TriagemModeloId]) REFERENCES [TriagemModelos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UsuarioTriagensHome_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE TABLE [RespostasDadas] (
        [Id] int NOT NULL IDENTITY,
        [TriagemResultadoId] int NOT NULL,
        [PerguntaId] int NOT NULL,
        [Valor] bit NOT NULL,
        CONSTRAINT [PK_RespostasDadas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RespostasDadas_TriagemResultados_TriagemResultadoId] FOREIGN KEY ([TriagemResultadoId]) REFERENCES [TriagemResultados] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE INDEX [IX_FaixasResultado_TriagemModeloId] ON [FaixasResultado] ([TriagemModeloId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE INDEX [IX_Perguntas_TriagemModeloId] ON [Perguntas] ([TriagemModeloId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE INDEX [IX_RespostasDadas_TriagemResultadoId] ON [RespostasDadas] ([TriagemResultadoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE INDEX [IX_TriagemModelos_CriadorUsuarioId] ON [TriagemModelos] ([CriadorUsuarioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE INDEX [IX_TriagemResultados_TriagemModeloId] ON [TriagemResultados] ([TriagemModeloId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE INDEX [IX_TriagemResultados_UsuarioId_TriagemModeloId] ON [TriagemResultados] ([UsuarioId], [TriagemModeloId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuarios_Email] ON [Usuarios] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    CREATE INDEX [IX_UsuarioTriagensHome_TriagemModeloId] ON [UsuarioTriagensHome] ([TriagemModeloId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821173519_InicialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821173519_InicialCreate', N'10.0.0');
END;

COMMIT;
GO
