-- =============================================================================
-- SourceMigrations.sql — Schema Changes Migration
-- Apply to source ONLY. Destination stays at BaselineSchema.sql.
-- The tool should detect all 10 CDC issues.
-- =============================================================================

-- Issue #1: Column Added
ALTER TABLE dbo.Players ADD DisplayName NVARCHAR(100) NULL;
GO

-- Issue #2: Column Dropped
ALTER TABLE dbo.Scores DROP COLUMN LegacyScore;
GO

-- Issue #3: Column Data Type Changed
ALTER TABLE dbo.Matches ALTER COLUMN Duration BIGINT NOT NULL;
GO

-- Issue #4: Column Renamed
EXEC sp_rename 'dbo.Inventory.ItemCode', 'ItemSku', 'COLUMN';
GO

-- Issue #5: Primary Key Changed
ALTER TABLE dbo.Regions DROP CONSTRAINT PK_Regions;
ALTER TABLE dbo.Regions ADD CONSTRAINT PK_Regions PRIMARY KEY (RegionId, CountryCode);
GO

-- Issue #6: Table Renamed
EXEC sp_rename 'dbo.Achievements', 'PlayerAchievements';
GO

-- Issue #7: Table Dropped
DROP TABLE dbo.LegacyStats;
GO

-- Issue #8: Unique Index Changed
DROP INDEX UX_Sessions_Token ON dbo.Sessions;
CREATE UNIQUE INDEX UX_Sessions_Token ON dbo.Sessions (SessionToken, DeviceId);
GO

-- Issue #9: Schema Change
CREATE SCHEMA game;
GO
ALTER SCHEMA game TRANSFER dbo.Leaderboards;
GO

-- Issue #10: Column Nullability Changed
ALTER TABLE dbo.Profiles ALTER COLUMN Bio NVARCHAR(500) NOT NULL;
GO

-- Control: Non-CDC table modification (should NOT trigger any CDC warnings)
ALTER TABLE dbo.AppSettings ADD Description NVARCHAR(200) NULL;
