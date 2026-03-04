namespace AnujShroff.CDCMigrationGuard.Models;

public class TableSchema
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string FullName => $"{SchemaName}.{TableName}";
    public List<ColumnInfo> Columns { get; set; } = [];
    public List<string> PrimaryKeyColumns { get; set; } = [];
    public string? PkConstraintName { get; set; }
}
