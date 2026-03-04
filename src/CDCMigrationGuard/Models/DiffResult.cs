namespace AnujShroff.CDCMigrationGuard.Models;

public enum Severity
{
    Info,
    Low,
    High,
    Critical
}

public enum IssueType
{
    ColumnAdded,
    ColumnDropped,
    ColumnTypeChanged,
    ColumnRenamed,
    PrimaryKeyChanged,
    TableRenamed,
    TableDropped,
    CdcIndexChanged,
    SchemaChanged,
    NullabilityChanged,
    CaptureInstanceLimit
}

public class DiffResult
{
    public IssueType Issue { get; set; }
    public Severity Severity { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string? ColumnName { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string FullTableName => $"{SchemaName}.{TableName}";
}
