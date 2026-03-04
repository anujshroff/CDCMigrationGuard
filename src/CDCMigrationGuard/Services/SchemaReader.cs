using AnujShroff.CDCMigrationGuard.Models;
using Microsoft.Data.SqlClient;

namespace AnujShroff.CDCMigrationGuard.Services;

public class SchemaReader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<List<TableSchema>> ReadSchemasAsync(IEnumerable<string> tableFullNames)
    {
        var tables = new Dictionary<string, TableSchema>();
        var tableList = tableFullNames.ToList();

        if (tableList.Count == 0)
            return [];

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Build parameterized IN clause
        var paramNames = new List<string>();
        var cmd = new SqlCommand();
        for (int i = 0; i < tableList.Count; i++)
        {
            paramNames.Add($"@t{i}");
            cmd.Parameters.AddWithValue($"@t{i}", tableList[i]);
        }

        cmd.Connection = conn;
        cmd.CommandText = $@"
SELECT
    s.name AS schema_name,
    t.name AS table_name,
    c.name AS column_name,
    c.column_id,
    ty.name AS type_name,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    c.is_computed,
    kc.name AS pk_constraint_name,
    ic.key_ordinal AS pk_ordinal
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.index_columns ic ON ic.object_id = t.object_id
    AND ic.column_id = c.column_id
    AND ic.index_id = (
        SELECT i.index_id FROM sys.indexes i
        WHERE i.object_id = t.object_id AND i.is_primary_key = 1
    )
LEFT JOIN sys.key_constraints kc ON kc.parent_object_id = t.object_id
    AND kc.type = 'PK'
WHERE s.name + '.' + t.name IN ({string.Join(", ", paramNames)})
ORDER BY s.name, t.name, c.column_id";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var key = $"{schema}.{table}";

            if (!tables.TryGetValue(key, out var ts))
            {
                ts = new TableSchema
                {
                    SchemaName = schema,
                    TableName = table,
                    PkConstraintName = reader.IsDBNull(11) ? null : reader.GetString(11)
                };
                tables[key] = ts;
            }

            var col = new ColumnInfo
            {
                SchemaName = schema,
                TableName = table,
                ColumnName = reader.GetString(2),
                ColumnId = reader.GetInt32(3),
                TypeName = reader.GetString(4),
                MaxLength = reader.GetInt16(5),
                Precision = reader.GetByte(6),
                Scale = reader.GetByte(7),
                IsNullable = reader.GetBoolean(8),
                IsIdentity = reader.GetBoolean(9),
                IsComputed = reader.GetBoolean(10),
                PkConstraintName = reader.IsDBNull(11) ? null : reader.GetString(11),
                PkOrdinal = reader.IsDBNull(12) ? null : Convert.ToInt32(reader.GetValue(12))
            };
            ts.Columns.Add(col);

            if (col.PkOrdinal.HasValue)
                ts.PrimaryKeyColumns.Add(col.ColumnName);
        }

        return [.. tables.Values];
    }

    /// <summary>
    /// Read ALL tables from the database (for detecting renames/schema changes).
    /// Returns just schema.table names.
    /// </summary>
    public async Task<List<string>> ReadAllTableNamesAsync()
    {
        var result = new List<string>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
SELECT s.name, t.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name", conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add($"{reader.GetString(0)}.{reader.GetString(1)}");

        return result;
    }

    /// <summary>
    /// Read index columns for a specific index on a table.
    /// </summary>
    public async Task<IndexInfo?> ReadIndexAsync(string schemaName, string tableName, string indexName)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
SELECT c.name AS column_name
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @schema AND t.name = @table AND i.name = @index
ORDER BY ic.key_ordinal", conn);

        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@index", indexName);

        var cols = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            cols.Add(reader.GetString(0));

        if (cols.Count == 0)
            return null;

        return new IndexInfo
        {
            SchemaName = schemaName,
            TableName = tableName,
            IndexName = indexName,
            Columns = cols
        };
    }

    public async Task TestConnectionAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync();
    }
}
