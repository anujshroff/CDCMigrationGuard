using AnujShroff.CDCMigrationGuard.Models;
using AnujShroff.CDCMigrationGuard.Services;
using Xunit;

namespace CDCMigrationGuard.Tests;

[Collection("Database")]
public class CompareTests(DatabaseFixture db)
{
    private readonly DatabaseFixture _db = db;

    private async Task<List<DiffResult>> RunCompareAsync()
    {
        var sourceReader = new SchemaReader(_db.SourceConnectionString);
        var destReader = new SchemaReader(_db.DestinationConnectionString);
        var cdcReader = new CdcMetadataReader(_db.DestinationConnectionString);

        var cdcTracking = await cdcReader.GetTrackedTablesAsync();
        var allSourceTables = await sourceReader.ReadAllTableNamesAsync();
        var trackedTableNames = cdcTracking.Select(c => c.FullName).Distinct().ToList();

        var sourceSchemas = await sourceReader.ReadSchemasAsync(trackedTableNames);
        var destSchemas = await destReader.ReadSchemasAsync(trackedTableNames);

        // Also read source schemas for tables that might have been renamed
        var trackedTableNamesOnly = cdcTracking.Select(c => c.TableName).Distinct().ToList();
        var additionalSourceTables = allSourceTables
            .Where(t => trackedTableNamesOnly.Any(tn =>
                t.Split('.').Last().Equals(tn, StringComparison.OrdinalIgnoreCase))
                && !trackedTableNames.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (additionalSourceTables.Count > 0)
        {
            var extra = await sourceReader.ReadSchemasAsync(additionalSourceTables);
            sourceSchemas.AddRange(extra);
        }

        var instanceCounts = await cdcReader.GetCaptureInstanceCountsAsync();

        // Read index info
        var sourceIndexes = new Dictionary<string, IndexInfo?>();
        var destIndexes = new Dictionary<string, IndexInfo?>();
        foreach (var cdc in cdcTracking.Where(c => c.CdcIndexName is not null))
        {
            var key = $"{cdc.FullName}|{cdc.CdcIndexName}";
            if (!sourceIndexes.ContainsKey(key))
                sourceIndexes[key] = await sourceReader.ReadIndexAsync(cdc.SchemaName, cdc.TableName, cdc.CdcIndexName!);
            if (!destIndexes.ContainsKey(key))
                destIndexes[key] = await destReader.ReadIndexAsync(cdc.SchemaName, cdc.TableName, cdc.CdcIndexName!);
        }

        var differ = new SchemaDiffer();
        var results = SchemaDiffer.Compare(cdcTracking, sourceSchemas, destSchemas, allSourceTables, instanceCounts);
        results.AddRange(SchemaDiffer.CompareIndexes(cdcTracking, sourceIndexes, destIndexes, results));

        return results;
    }

    [Fact]
    public async Task Detects_ColumnAdded()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.ColumnAdded
            && r.FullTableName == "dbo.Players");

        Assert.Equal(Severity.Low, match.Severity);
        Assert.Equal("DisplayName", match.ColumnName);
    }

    [Fact]
    public async Task Detects_ColumnDropped()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.ColumnDropped
            && r.FullTableName == "dbo.Scores");

        Assert.Equal(Severity.Critical, match.Severity);
        Assert.Equal("LegacyScore", match.ColumnName);
    }

    [Fact]
    public async Task Detects_ColumnTypeChanged()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.ColumnTypeChanged
            && r.FullTableName == "dbo.Matches");

        Assert.Equal(Severity.High, match.Severity);
        Assert.Equal("Duration", match.ColumnName);
    }

    [Fact]
    public async Task Detects_ColumnRenamed()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.ColumnRenamed
            && r.FullTableName == "dbo.Inventory");

        Assert.Equal(Severity.Critical, match.Severity);
        Assert.Equal("ItemCode", match.ColumnName);
    }

    [Fact]
    public async Task Detects_PrimaryKeyChanged()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.PrimaryKeyChanged
            && r.FullTableName == "dbo.Regions");

        Assert.Equal(Severity.Critical, match.Severity);
    }

    [Fact]
    public async Task Detects_TableDropped_Achievements()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.TableDropped
            && r.TableName == "Achievements");

        Assert.Equal(Severity.Critical, match.Severity);
    }

    [Fact]
    public async Task Detects_TableDropped_LegacyStats()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.TableDropped
            && r.TableName == "LegacyStats");

        Assert.Equal(Severity.Critical, match.Severity);
    }

    [Fact]
    public async Task Detects_CdcIndexChanged()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.CdcIndexChanged
            && r.FullTableName == "dbo.Sessions");

        Assert.Equal(Severity.Critical, match.Severity);
    }

    [Fact]
    public async Task Detects_SchemaChanged()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.SchemaChanged
            && r.TableName == "Leaderboards");

        Assert.Equal(Severity.Critical, match.Severity);
    }

    [Fact]
    public async Task Detects_NullabilityChanged()
    {
        var results = await RunCompareAsync();

        var match = results.Single(r =>
            r.Issue == IssueType.NullabilityChanged
            && r.FullTableName == "dbo.Profiles");

        Assert.Equal(Severity.Low, match.Severity);
        Assert.Equal("Bio", match.ColumnName);
    }

    [Fact]
    public async Task NoIssues_ForUnchangedCdcTable()
    {
        var results = await RunCompareAsync();

        var matches = results.Where(r => r.FullTableName == "dbo.AuditLog").ToList();
        Assert.Empty(matches);
    }

    [Fact]
    public async Task NoIssues_ForNonCdcTable()
    {
        var results = await RunCompareAsync();

        var matches = results.Where(r => r.FullTableName == "dbo.AppSettings").ToList();
        Assert.Empty(matches);
    }
}
