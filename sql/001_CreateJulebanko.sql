/*
  Julebanko — SQL Server schema (matches EF Core model / InitialCreate migration)

  Use ONE of:
    A) Run this script in SSMS / sqlcmd against your SQL Server instance, OR
    B) `dotnet ef database update` (app also runs Migrate on startup)

  If you use this script, it also records the EF migration in __EFMigrationsHistory
  so the app will not try to re-create tables.

  Admin users + default AppSettings + active Season are seeded below (idempotent).
  The app DbSeeder is also idempotent on startup — safe either way.

  Temp logins (change via Setup Admin after first login):
    bankoadmin  / BankoTemp2026!
    setupadmin  / SetupTemp2026!

  Enums (int columns):
    AdminRole: 1=BankoAdmin, 2=SetupAdmin
    WinType:   1=OneRow, 2=TwoRows, 3=FullBoard
    ClaimStatus: 0=Pending, 1=Approved, 2=Rejected
    MediaSlot: 1=PlayerHero, 2=PlayerBackground, 3=PlayerDecoration
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Optional: create database if missing
IF DB_ID(N'Julebanko') IS NULL
BEGIN
    CREATE DATABASE [Julebanko];
END
GO

USE [Julebanko];
GO

BEGIN TRANSACTION;

-- ---------------------------------------------------------------------------
-- Tables
-- ---------------------------------------------------------------------------

IF OBJECT_ID(N'dbo.AdminUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminUsers (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AdminUsers PRIMARY KEY,
        Username      NVARCHAR(64)  NOT NULL,
        PasswordHash  NVARCHAR(500) NOT NULL,
        Role          INT NOT NULL,
        CreatedAt     DATETIME2 NOT NULL
    );
END

IF OBJECT_ID(N'dbo.AppSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings (
        Id     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY,
        [Key]  NVARCHAR(100)  NOT NULL,
        Value  NVARCHAR(2000) NOT NULL
    );
END

IF OBJECT_ID(N'dbo.Seasons', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Seasons (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Seasons PRIMARY KEY,
        [Year]    INT NOT NULL,
        Title     NVARCHAR(200) NOT NULL,
        IsActive  BIT NOT NULL,
        CreatedAt DATETIME2 NOT NULL
    );
END

IF OBJECT_ID(N'dbo.MediaAssets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MediaAssets (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MediaAssets PRIMARY KEY,
        Slot               INT NOT NULL,
        FileName           NVARCHAR(260) NOT NULL,
        ContentType        NVARCHAR(100) NOT NULL,
        RelativePath       NVARCHAR(500) NOT NULL,
        UploadedAt         DATETIME2 NOT NULL,
        UploadedByAdminId  INT NULL,
        CONSTRAINT FK_MediaAssets_AdminUsers_UploadedByAdminId
            FOREIGN KEY (UploadedByAdminId) REFERENCES dbo.AdminUsers(Id) ON DELETE SET NULL
    );
END

IF OBJECT_ID(N'dbo.Claims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Claims (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Claims PRIMARY KEY,
        SeasonId           INT NOT NULL,
        PlayerName         NVARCHAR(120) NOT NULL,
        WinType            INT NOT NULL,
        Status             INT NOT NULL,
        CreatedAt          DATETIME2 NOT NULL,
        ReviewedAt         DATETIME2 NULL,
        ReviewedByAdminId  INT NULL,
        CONSTRAINT FK_Claims_Seasons_SeasonId
            FOREIGN KEY (SeasonId) REFERENCES dbo.Seasons(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Claims_AdminUsers_ReviewedByAdminId
            FOREIGN KEY (ReviewedByAdminId) REFERENCES dbo.AdminUsers(Id) ON DELETE SET NULL
    );
END

IF OBJECT_ID(N'dbo.DrawnNumbers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DrawnNumbers (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DrawnNumbers PRIMARY KEY,
        SeasonId     INT NOT NULL,
        Number       INT NOT NULL,
        DrawnAt      DATETIME2 NOT NULL,
        DrawBatchId  UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT FK_DrawnNumbers_Seasons_SeasonId
            FOREIGN KEY (SeasonId) REFERENCES dbo.Seasons(Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.SeasonBackups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SeasonBackups (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SeasonBackups PRIMARY KEY,
        SeasonId          INT NOT NULL,
        CreatedAt         DATETIME2 NOT NULL,
        CreatedByAdminId  INT NOT NULL,
        FileName          NVARCHAR(260) NOT NULL,
        RelativePath      NVARCHAR(500) NOT NULL,
        Note              NVARCHAR(500) NOT NULL,
        IsRestored        BIT NOT NULL,
        CONSTRAINT FK_SeasonBackups_Seasons_SeasonId
            FOREIGN KEY (SeasonId) REFERENCES dbo.Seasons(Id),
        CONSTRAINT FK_SeasonBackups_AdminUsers_CreatedByAdminId
            FOREIGN KEY (CreatedByAdminId) REFERENCES dbo.AdminUsers(Id)
    );
END

-- EF Core migrations history (so `dotnet ef database update` / Migrate is a no-op)
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.__EFMigrationsHistory (
        MigrationId    NVARCHAR(150) NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
        ProductVersion NVARCHAR(32)  NOT NULL
    );
END

-- ---------------------------------------------------------------------------
-- Indexes
-- ---------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AdminUsers_Username' AND object_id = OBJECT_ID(N'dbo.AdminUsers'))
    CREATE UNIQUE INDEX IX_AdminUsers_Username ON dbo.AdminUsers(Username);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppSettings_Key' AND object_id = OBJECT_ID(N'dbo.AppSettings'))
    CREATE UNIQUE INDEX IX_AppSettings_Key ON dbo.AppSettings([Key]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Seasons_Year' AND object_id = OBJECT_ID(N'dbo.Seasons'))
    CREATE INDEX IX_Seasons_Year ON dbo.Seasons([Year]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Claims_SeasonId' AND object_id = OBJECT_ID(N'dbo.Claims'))
    CREATE INDEX IX_Claims_SeasonId ON dbo.Claims(SeasonId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Claims_ReviewedByAdminId' AND object_id = OBJECT_ID(N'dbo.Claims'))
    CREATE INDEX IX_Claims_ReviewedByAdminId ON dbo.Claims(ReviewedByAdminId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DrawnNumbers_SeasonId_Number' AND object_id = OBJECT_ID(N'dbo.DrawnNumbers'))
    CREATE UNIQUE INDEX IX_DrawnNumbers_SeasonId_Number ON dbo.DrawnNumbers(SeasonId, Number);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MediaAssets_Slot' AND object_id = OBJECT_ID(N'dbo.MediaAssets'))
    CREATE UNIQUE INDEX IX_MediaAssets_Slot ON dbo.MediaAssets(Slot);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MediaAssets_UploadedByAdminId' AND object_id = OBJECT_ID(N'dbo.MediaAssets'))
    CREATE INDEX IX_MediaAssets_UploadedByAdminId ON dbo.MediaAssets(UploadedByAdminId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeasonBackups_SeasonId' AND object_id = OBJECT_ID(N'dbo.SeasonBackups'))
    CREATE INDEX IX_SeasonBackups_SeasonId ON dbo.SeasonBackups(SeasonId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeasonBackups_CreatedByAdminId' AND object_id = OBJECT_ID(N'dbo.SeasonBackups'))
    CREATE INDEX IX_SeasonBackups_CreatedByAdminId ON dbo.SeasonBackups(CreatedByAdminId);

-- ---------------------------------------------------------------------------
-- Seed data (idempotent)
-- Password hashes: ASP.NET Core Identity PasswordHasher (v3) for the temp passwords above.
-- If you prefer, leave AdminUsers empty and let the app DbSeeder create them on first start.
-- ---------------------------------------------------------------------------

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @Year INT = YEAR(@Now);

-- Banko Admin (Role = 1)
IF NOT EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE Username = N'bankoadmin')
BEGIN
    INSERT INTO dbo.AdminUsers (Username, PasswordHash, Role, CreatedAt)
    VALUES (
        N'bankoadmin',
        N'AQAAAAIAAYagAAAAEPOdyUR0r0r6EzpM/enG2qKZ8uoaKcjQ9qfQ35RZABggOyw1v4IVmrq0nqP4o0yLuQ==',
        1,
        @Now
    );
END

-- Setup Admin (Role = 2)
IF NOT EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE Username = N'setupadmin')
BEGIN
    INSERT INTO dbo.AdminUsers (Username, PasswordHash, Role, CreatedAt)
    VALUES (
        N'setupadmin',
        N'AQAAAAIAAYagAAAAEAwA5iu2CUYy5XBO+W9hTRwIzoincgyDG1ltVY7FWw/UOxw0BjhiJrZ1FDl/PvbYaA==',
        2,
        @Now
    );
END

-- Active season
IF NOT EXISTS (SELECT 1 FROM dbo.Seasons WHERE IsActive = 1)
BEGIN
    INSERT INTO dbo.Seasons ([Year], Title, IsActive, CreatedAt)
    VALUES (@Year, CONCAT(N'Julebanko ', @Year), 1, @Now);
END

-- Default AppSettings
MERGE dbo.AppSettings AS t
USING (VALUES
    (N'PlayerBgColor',      N'#8B1E1E'),
    (N'AdminBgColor',       N'#1B4332'),
    (N'AccentColor',        N'#D4A017'),
    (N'PlayerTextColor',    N'#FFF8F0'),
    (N'AdminTextColor',     N'#F0F7F2'),
    (N'ClaimNotifyTo',      N'banko@example.com'),
    (N'MailFrom',           N'julebanko@example.com'),
    (N'SeasonTitle',        CONCAT(N'Julebanko ', @Year)),
    (N'PlayerSnowEnabled',   N'true'),
    (N'PlayerSnowIntensity', N'1'),
    (N'PlayerNisseEnabled',  N'true')
) AS s([Key], Value)
ON t.[Key] = s.[Key]
WHEN NOT MATCHED THEN
    INSERT ([Key], Value) VALUES (s.[Key], s.Value);

-- Mark EF migration applied
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261117104829_InitialCreate')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261117104829_InitialCreate', N'8.0.11');
END

COMMIT TRANSACTION;
GO
