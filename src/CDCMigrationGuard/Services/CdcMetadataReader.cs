using AnujShroff.CDCMigrationGuard.Models;
using Microsoft.Data.SqlClient;

namespace AnujShroff.CDCMigrationGuard.Services;

public class CdcMetadataReader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<bool> IsCdcEnabledAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT is_cdc_enabled FROM sys.databases WHERE name = DB_NAME()", conn);
        var result = await cmd.ExecuteScalarAsync();
        return result is true or 1;
    }

    public async Task<List<CdcTrackingInfo>> GetTrackedTablesAsync()
    {
        var tracking = new Dictionary<string, CdcTrackingInfo>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
SELECT
    s.name AS schema_name,
    t.name AS table_name,
    ct.capture_instance,
    ct.supports_net_changes,
    COL_NAME(ct.source_object_id, cc.column_id) AS tracked_column_name,
    cc.column_id,
    ct.index_name AS cdc_index_name
FROM cdc.change_tables ct
JOIN sys.tables t ON t.object_id = ct.source_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN cdc.captured_columns cc ON cc.object_id = ct.object_id
ORDER BY s.name, t.name, cc.column_id", conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var captureInstance = reader.GetString(2);
            var key = $"{schema}.{table}|{captureInstance}";

            if (!tracking.TryGetValue(key, out var info))
            {
                info = new CdcTrackingInfo
                {
                    SchemaName = schema,
                    TableName = table,
                    CaptureInstance = captureInstance,
                    SupportsNetChanges = reader.GetBoolean(3),
                    CdcIndexName = reader.IsDBNull(6) ? null : reader.GetString(6)
                };
                tracking[key] = info;
            }

            var colName = reader.IsDBNull(4) ? null : reader.GetString(4);
            if (colName is not null)
                info.TrackedColumns.Add(colName);
        }

        // Now read CDC index columns for each tracked table that has a named index
        // Use a separate connection to avoid MARS issues
        var tablesWithIndex = tracking.Values.Where(i => i.CdcIndexName is not null).ToList();
        foreach (var info in tablesWithIndex)
        {
            await using var conn2 = new SqlConnection(_connectionString);
            await conn2.OpenAsync();
            info.IndexColumns = await GetIndexColumnsAsync(conn2, info.SchemaName, info.TableName, info.CdcIndexName!);
        }

        return [.. tracking.Values];
    }

    private static async Task<List<string>> GetIndexColumnsAsync(
        SqlConnection conn, string schema, string table, string indexName)
    {
        var cols = new List<string>();
        await using var cmd = new SqlCommand(@"
SELECT c.name
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @schema AND t.name = @table AND i.name = @index
ORDER BY ic.key_ordinal", conn);

        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);
        cmd.Parameters.AddWithValue("@index", indexName);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            cols.Add(reader.GetString(0));

        return cols;
    }

    public async Task<Dictionary<string, int>> GetCaptureInstanceCountsAsync()
    {
        var counts = new Dictionary<string, int>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
SELECT
    OBJECT_SCHEMA_NAME(source_object_id) AS schema_name,
    OBJECT_NAME(source_object_id) AS table_name,
    COUNT(*) AS instance_count
FROM cdc.change_tables
GROUP BY source_object_id
HAVING COUNT(*) >= 2", conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            counts[key] = reader.GetInt32(2);
        }

        return counts;
    }
}
