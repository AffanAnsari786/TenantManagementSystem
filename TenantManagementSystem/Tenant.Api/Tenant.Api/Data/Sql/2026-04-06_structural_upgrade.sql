-- ---------------------------------------------------------------------------
-- One-off SQL for the structural upgrade.
--
-- Use this if you are NOT running EF Core migrations yet. Once you adopt
-- migrations (recommended), this file can be deleted and the same changes
-- will be emitted by `dotnet ef migrations add StructuralUpgrade`.
--
-- Safe to run multiple times — each statement is guarded.
-- ---------------------------------------------------------------------------

-- 1. RefreshTokens table ----------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RefreshTokens')
BEGIN
    CREATE TABLE [dbo].[RefreshTokens] (
        [Id]                   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId]               INT              NOT NULL,
        [TokenHash]            NVARCHAR(128)    NOT NULL,
        [CreatedAt]            DATETIME2        NOT NULL,
        [ExpiresAt]            DATETIME2        NOT NULL,
        [RevokedAt]            DATETIME2        NULL,
        [ReplacedByTokenHash]  NVARCHAR(128)    NULL
    );
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [dbo].[RefreshTokens]([TokenHash]);
    CREATE INDEX        [IX_RefreshTokens_UserId]    ON [dbo].[RefreshTokens]([UserId]);
END
GO

-- 2. Performance indexes on existing tables --------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Entries_UserId' AND object_id = OBJECT_ID('dbo.Entries'))
    CREATE INDEX [IX_Entries_UserId] ON [dbo].[Entries]([UserId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Records_EntryId' AND object_id = OBJECT_ID('dbo.Records'))
    CREATE INDEX [IX_Records_EntryId] ON [dbo].[Records]([EntryId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SharedLinks_IsActive_ExpiryDate' AND object_id = OBJECT_ID('dbo.SharedLinks'))
    CREATE INDEX [IX_SharedLinks_IsActive_ExpiryDate] ON [dbo].[SharedLinks]([IsActive], [ExpiryDate]);
GO

-- 3. Records.ReceiptNumber max length (new column config)
IF COL_LENGTH('dbo.Records', 'ReceiptNumber') IS NULL
BEGIN
    ALTER TABLE [dbo].[Records] ADD [ReceiptNumber] NVARCHAR(50) NULL;
END
GO
