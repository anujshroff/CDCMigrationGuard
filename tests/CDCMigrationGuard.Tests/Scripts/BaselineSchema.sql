-- =============================================================================
-- BaselineSchema.sql — Baseline Migration
-- Apply to BOTH source and destination to establish initial schema.
-- CDC enablement is handled separately — this script is pure DDL.
-- =============================================================================

-- --------------------------------------------------------------------------
-- Issue #1: Column Added to a Tracked Table
-- SourceMigrations.sql will ADD a new column to this table.
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Players
(
    PlayerId    INT            NOT NULL,
    Username    NVARCHAR(100)  NOT NULL,
    Email       NVARCHAR(200)  NOT NULL,
    CreatedAt   DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_Players PRIMARY KEY (PlayerId)
);

-- --------------------------------------------------------------------------
-- Issue #2: Column Dropped from a Tracked Table
-- SourceMigrations.sql will DROP the LegacyScore column (which is CDC-tracked).
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Scores
(
    ScoreId     INT            NOT NULL,
    PlayerId    INT            NOT NULL,
    GameScore   INT            NOT NULL,
    LegacyScore INT            NULL,
    RecordedAt  DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_Scores PRIMARY KEY (ScoreId)
);

-- --------------------------------------------------------------------------
-- Issue #3: Column Data Type Changed
-- SourceMigrations.sql will change Duration from INT to BIGINT.
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Matches
(
    MatchId     INT            NOT NULL,
    Player1Id   INT            NOT NULL,
    Player2Id   INT            NOT NULL,
    Duration    INT            NOT NULL,
    PlayedAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_Matches PRIMARY KEY (MatchId)
);

-- --------------------------------------------------------------------------
-- Issue #4: Column Renamed
-- SourceMigrations.sql will rename ItemCode to ItemSku via sp_rename.
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Inventory
(
    InventoryId INT            NOT NULL,
    PlayerId    INT            NOT NULL,
    ItemCode    NVARCHAR(50)   NOT NULL,
    Quantity    INT            NOT NULL DEFAULT 1,
    CONSTRAINT PK_Inventory PRIMARY KEY (InventoryId)
);

-- --------------------------------------------------------------------------
-- Issue #5: Primary Key Changed
-- SourceMigrations.sql will expand the PK from (RegionId) to (RegionId, CountryCode).
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Regions
(
    RegionId    INT            NOT NULL,
    CountryCode NVARCHAR(3)    NOT NULL,
    RegionName  NVARCHAR(100)  NOT NULL,
    IsActive    BIT            NOT NULL DEFAULT 1,
    CONSTRAINT PK_Regions PRIMARY KEY (RegionId)
);

-- --------------------------------------------------------------------------
-- Issue #6: Table Renamed
-- SourceMigrations.sql will rename this table to dbo.PlayerAchievements.
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Achievements
(
    AchievementId   INT            NOT NULL,
    AchievementName NVARCHAR(100)  NOT NULL,
    Description     NVARCHAR(500)  NULL,
    CONSTRAINT PK_Achievements PRIMARY KEY (AchievementId)
);

-- --------------------------------------------------------------------------
-- Issue #7: Table Dropped
-- SourceMigrations.sql will DROP this table entirely.
-- --------------------------------------------------------------------------
CREATE TABLE dbo.LegacyStats
(
    StatId      INT            NOT NULL,
    PlayerId    INT            NOT NULL,
    StatName    NVARCHAR(100)  NOT NULL,
    StatValue   DECIMAL(18,4)  NOT NULL,
    CONSTRAINT PK_LegacyStats PRIMARY KEY (StatId)
);

-- --------------------------------------------------------------------------
-- Issue #8: Unique Index Used by CDC Changed
-- CDC will be enabled with @index_name = 'UX_Sessions_Token'.
-- SourceMigrations.sql will modify this index's column composition.
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Sessions
(
    SessionId    INT            NOT NULL,
    PlayerId     INT            NOT NULL,
    SessionToken NVARCHAR(200)  NOT NULL,
    DeviceId     NVARCHAR(100)  NOT NULL,
    StartedAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_Sessions PRIMARY KEY (SessionId)
);

CREATE UNIQUE INDEX UX_Sessions_Token ON dbo.Sessions (SessionToken);

-- --------------------------------------------------------------------------
-- Issue #9: Schema (Namespace) Change
-- SourceMigrations.sql will transfer this table from dbo to a new 'game' schema.
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Leaderboards
(
    LeaderboardId   INT            NOT NULL,
    LeaderboardName NVARCHAR(100)  NOT NULL,
    Season          INT            NOT NULL,
    CONSTRAINT PK_Leaderboards PRIMARY KEY (LeaderboardId)
);

-- --------------------------------------------------------------------------
-- Issue #10: Column Nullability Changed
-- SourceMigrations.sql will change Bio from NULL to NOT NULL.
-- --------------------------------------------------------------------------
CREATE TABLE dbo.Profiles
(
    ProfileId   INT            NOT NULL,
    PlayerId    INT            NOT NULL,
    Bio         NVARCHAR(500)  NULL,
    AvatarUrl   NVARCHAR(300)  NULL,
    CONSTRAINT PK_Profiles PRIMARY KEY (ProfileId)
);

-- --------------------------------------------------------------------------
-- Control: CDC-tracked table that is UNCHANGED by SourceMigrations.sql
-- --------------------------------------------------------------------------
CREATE TABLE dbo.AuditLog
(
    LogId       INT            NOT NULL,
    TableName   NVARCHAR(200)  NOT NULL,
    Operation   NVARCHAR(10)   NOT NULL,
    ChangedAt   DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    ChangedBy   NVARCHAR(100)  NOT NULL,
    CONSTRAINT PK_AuditLog PRIMARY KEY (LogId)
);

-- --------------------------------------------------------------------------
-- Control: Non-CDC table that IS modified by SourceMigrations.sql
-- --------------------------------------------------------------------------
CREATE TABLE dbo.AppSettings
(
    SettingId    INT            NOT NULL,
    SettingKey   NVARCHAR(100)  NOT NULL,
    SettingValue NVARCHAR(500)  NULL,
    LastUpdated  DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_AppSettings PRIMARY KEY (SettingId)
);
