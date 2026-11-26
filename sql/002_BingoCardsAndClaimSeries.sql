/*
  Julebanko — Bankoplader (next-year) schema delta

  Apply after 001_CreateJulebanko.sql / InitialCreate migration.
  Safe to re-run (IF NOT EXISTS / column checks).

  Also recorded in EF as BingoCardsAndClaimSeries migration —
  prefer `dotnet ef database update` when using EF Migrate on startup.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
USE [Julebanko];
GO

BEGIN TRANSACTION;

-- ---------------------------------------------------------------------------
-- BingoCardBatches
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.BingoCardBatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BingoCardBatches (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BingoCardBatches PRIMARY KEY,
        SeasonId           INT NOT NULL,
        CreatedAt          DATETIME2 NOT NULL,
        CreatedByAdminId   INT NOT NULL,
        PlateCount         INT NOT NULL,
        PlatesPerPage      INT NOT NULL,
        BannerText         NVARCHAR(200) NULL,
        Note               NVARCHAR(500) NULL,
        CONSTRAINT FK_BingoCardBatches_Seasons_SeasonId
            FOREIGN KEY (SeasonId) REFERENCES dbo.Seasons(Id) ON DELETE CASCADE,
        CONSTRAINT FK_BingoCardBatches_AdminUsers_CreatedByAdminId
            FOREIGN KEY (CreatedByAdminId) REFERENCES dbo.AdminUsers(Id)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BingoCardBatches_SeasonId' AND object_id = OBJECT_ID(N'dbo.BingoCardBatches'))
    CREATE INDEX IX_BingoCardBatches_SeasonId ON dbo.BingoCardBatches(SeasonId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BingoCardBatches_CreatedByAdminId' AND object_id = OBJECT_ID(N'dbo.BingoCardBatches'))
    CREATE INDEX IX_BingoCardBatches_CreatedByAdminId ON dbo.BingoCardBatches(CreatedByAdminId);

-- ---------------------------------------------------------------------------
-- BingoCards
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.BingoCards', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BingoCards (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BingoCards PRIMARY KEY,
        BatchId       INT NOT NULL,
        SeasonId      INT NOT NULL,
        SeriesNumber  INT NOT NULL,
        CellsJson     NVARCHAR(2000) NOT NULL,
        Fingerprint   NVARCHAR(120) NOT NULL,
        CreatedAt     DATETIME2 NOT NULL,
        CONSTRAINT FK_BingoCards_BingoCardBatches_BatchId
            FOREIGN KEY (BatchId) REFERENCES dbo.BingoCardBatches(Id) ON DELETE CASCADE,
        CONSTRAINT FK_BingoCards_Seasons_SeasonId
            FOREIGN KEY (SeasonId) REFERENCES dbo.Seasons(Id)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BingoCards_SeasonId_SeriesNumber' AND object_id = OBJECT_ID(N'dbo.BingoCards'))
    CREATE UNIQUE INDEX IX_BingoCards_SeasonId_SeriesNumber ON dbo.BingoCards(SeasonId, SeriesNumber);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BingoCards_SeasonId_Fingerprint' AND object_id = OBJECT_ID(N'dbo.BingoCards'))
    CREATE UNIQUE INDEX IX_BingoCards_SeasonId_Fingerprint ON dbo.BingoCards(SeasonId, Fingerprint);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BingoCards_BatchId' AND object_id = OBJECT_ID(N'dbo.BingoCards'))
    CREATE INDEX IX_BingoCards_BatchId ON dbo.BingoCards(BatchId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BingoCards_SeasonId' AND object_id = OBJECT_ID(N'dbo.BingoCards'))
    CREATE INDEX IX_BingoCards_SeasonId ON dbo.BingoCards(SeasonId);

-- ---------------------------------------------------------------------------
-- Claims: series + validation columns
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Claims', N'SeriesNumber') IS NULL
    ALTER TABLE dbo.Claims ADD SeriesNumber NVARCHAR(32) NULL;

IF COL_LENGTH(N'dbo.Claims', N'BingoCardId') IS NULL
    ALTER TABLE dbo.Claims ADD BingoCardId INT NULL;

IF COL_LENGTH(N'dbo.Claims', N'ValidationStatus') IS NULL
    ALTER TABLE dbo.Claims ADD ValidationStatus INT NOT NULL CONSTRAINT DF_Claims_ValidationStatus DEFAULT 0;

IF COL_LENGTH(N'dbo.Claims', N'AutoValidated') IS NULL
    ALTER TABLE dbo.Claims ADD AutoValidated BIT NOT NULL CONSTRAINT DF_Claims_AutoValidated DEFAULT 0;

IF COL_LENGTH(N'dbo.Claims', N'ValidationNote') IS NULL
    ALTER TABLE dbo.Claims ADD ValidationNote NVARCHAR(500) NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Claims_BingoCards_BingoCardId'
)
BEGIN
    ALTER TABLE dbo.Claims ADD CONSTRAINT FK_Claims_BingoCards_BingoCardId
        FOREIGN KEY (BingoCardId) REFERENCES dbo.BingoCards(Id) ON DELETE NO ACTION;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Claims_BingoCardId' AND object_id = OBJECT_ID(N'dbo.Claims'))
    CREATE INDEX IX_Claims_BingoCardId ON dbo.Claims(BingoCardId);

-- ---------------------------------------------------------------------------
-- Default AppSettings for bankoplader
-- ---------------------------------------------------------------------------
MERGE dbo.AppSettings AS t
USING (VALUES
    (N'BingoAutoValidate',      N'true'),
    (N'BingoPrizeOncePerType',  N'true'),
    (N'BingoBannerText',        N''),
    (N'BingoPrimaryColor',      N''),
    (N'BingoAccentColor',       N''),
    (N'BingoFestiveLook',       N'true'),
    (N'BingoPlateScheme',       N'crimson-gold'),
    (N'BingoPdfBackgroundColor',N'#FFFFFF'),
    (N'BingoFrameColor',        N'#8B1E1E'),
    (N'BingoBannerColor',       N'#8B1E1E')
) AS s([Key], Value)
ON t.[Key] = s.[Key]
WHEN NOT MATCHED THEN
    INSERT ([Key], Value) VALUES (s.[Key], s.Value);

-- Mark EF migration applied so app Migrate() is a no-op for this delta
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261124110047_BingoCardsAndClaimSeries')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261124110047_BingoCardsAndClaimSeries', N'10.0.15');
END

COMMIT TRANSACTION;
GO
