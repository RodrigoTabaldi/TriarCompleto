-- =====================================================================
-- Triar - Script de criação do banco (SQL Server)
-- Observação: a API cria e popula o banco automaticamente na primeira
-- execução. Este script existe como referência/criação manual.
-- =====================================================================

IF DB_ID('TriarDb') IS NULL
    CREATE DATABASE TriarDb;
GO

USE TriarDb;
GO

IF OBJECT_ID('dbo.Usuarios') IS NULL
CREATE TABLE dbo.Usuarios (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Nome        NVARCHAR(120) NOT NULL,
    Email       NVARCHAR(180) NOT NULL,
    SenhaHash   NVARCHAR(500) NOT NULL,
    CriadoEm    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Usuarios_Email UNIQUE (Email)
);
GO

IF OBJECT_ID('dbo.TriagemModelos') IS NULL
CREATE TABLE dbo.TriagemModelos (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    Titulo           NVARCHAR(150) NOT NULL,
    PublicoAlvo      NVARCHAR(150) NOT NULL,
    Descricao        NVARCHAR(600) NOT NULL,
    Icone            NVARCHAR(16)  NOT NULL,
    CriadorUsuarioId INT NULL REFERENCES dbo.Usuarios(Id),
    Ativa            BIT NOT NULL DEFAULT 1,
    CriadoEm         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

IF OBJECT_ID('dbo.Perguntas') IS NULL
CREATE TABLE dbo.Perguntas (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TriagemModeloId INT NOT NULL REFERENCES dbo.TriagemModelos(Id) ON DELETE CASCADE,
    Texto           NVARCHAR(500) NOT NULL,
    Peso            INT NOT NULL DEFAULT 1,
    Ordem           INT NOT NULL DEFAULT 0
);
GO

IF OBJECT_ID('dbo.FaixasResultado') IS NULL
CREATE TABLE dbo.FaixasResultado (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TriagemModeloId INT NOT NULL REFERENCES dbo.TriagemModelos(Id) ON DELETE CASCADE,
    Titulo          NVARCHAR(120) NOT NULL,
    Recomendacao    NVARCHAR(600) NOT NULL,
    PontuacaoMin    INT NOT NULL,
    PontuacaoMax    INT NOT NULL,
    Cor             NVARCHAR(9) NOT NULL DEFAULT '#10B981',
    Ordem           INT NOT NULL DEFAULT 0
);
GO

IF OBJECT_ID('dbo.UsuarioTriagensHome') IS NULL
CREATE TABLE dbo.UsuarioTriagensHome (
    UsuarioId       INT NOT NULL REFERENCES dbo.Usuarios(Id) ON DELETE CASCADE,
    TriagemModeloId INT NOT NULL REFERENCES dbo.TriagemModelos(Id),
    Visivel         BIT NOT NULL DEFAULT 1,
    Ordem           INT NOT NULL DEFAULT 0,
    CONSTRAINT PK_UsuarioTriagensHome PRIMARY KEY (UsuarioId, TriagemModeloId)
);
GO

IF OBJECT_ID('dbo.TriagemResultados') IS NULL
CREATE TABLE dbo.TriagemResultados (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TriagemModeloId INT NOT NULL REFERENCES dbo.TriagemModelos(Id),
    UsuarioId       INT NOT NULL REFERENCES dbo.Usuarios(Id) ON DELETE CASCADE,
    NomePaciente    NVARCHAR(150) NOT NULL,
    Idade           INT NOT NULL,
    Sexo            NVARCHAR(30) NOT NULL,
    Pontuacao       INT NOT NULL,
    PontuacaoMaxima INT NOT NULL,
    Classificacao   NVARCHAR(120) NOT NULL,
    Recomendacao    NVARCHAR(600) NOT NULL,
    Cor             NVARCHAR(9) NOT NULL DEFAULT '#10B981',
    Data            DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TriagemResultados_Usuario_Modelo')
    CREATE INDEX IX_TriagemResultados_Usuario_Modelo
        ON dbo.TriagemResultados (UsuarioId, TriagemModeloId);
GO

IF OBJECT_ID('dbo.RespostasDadas') IS NULL
CREATE TABLE dbo.RespostasDadas (
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    TriagemResultadoId INT NOT NULL REFERENCES dbo.TriagemResultados(Id) ON DELETE CASCADE,
    PerguntaId         INT NOT NULL,
    Valor              BIT NOT NULL
);
GO
