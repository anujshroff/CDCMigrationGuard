namespace AnujShroff.CDCMigrationGuard.Models;

public class ColumnInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public int ColumnId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public short MaxLength { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsComputed { get; set; }
    public string? PkConstraintName { get; set; }
    public int? PkOrdinal { get; set; }
}
