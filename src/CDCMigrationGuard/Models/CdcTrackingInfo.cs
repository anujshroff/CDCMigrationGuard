namespace AnujShroff.CDCMigrationGuard.Models;

public class CdcTrackingInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string FullName => $"{SchemaName}.{TableName}";
    public string CaptureInstance { get; set; } = string.Empty;
    public bool SupportsNetChanges { get; set; }
    public string? CdcIndexName { get; set; }
    public List<string> TrackedColumns { get; set; } = [];
    public List<string> IndexColumns { get; set; } = [];
}

public class IndexInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
}
