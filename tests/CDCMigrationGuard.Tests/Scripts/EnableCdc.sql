-- =============================================================================
-- EnableCdc.sql — Enable CDC tracking on destination only.
-- Apply after BaselineSchema.sql.
-- =============================================================================
EXEC sys.sp_cdc_enable_db;
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Players',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Scores',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Matches',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Inventory',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Regions',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Achievements',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'LegacyStats',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Sessions',
    @role_name = NULL,
    @supports_net_changes = 1,
    @index_name = N'UX_Sessions_Token'
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Leaderboards',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'Profiles',
    @role_name = NULL,
    @supports_net_changes = 1
GO

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'AuditLog',
    @role_name = NULL,
    @supports_net_changes = 1
GO
