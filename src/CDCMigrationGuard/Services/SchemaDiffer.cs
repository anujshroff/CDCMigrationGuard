using AnujShroff.CDCMigrationGuard.Models;

namespace AnujShroff.CDCMigrationGuard.Services;

public class SchemaDiffer
{
    /// <summary>
    /// Compare source (post-migration) vs destination (pre-migration, CDC-tracked).
    /// </summary>
    public static List<DiffResult> Compare(
        List<CdcTrackingInfo> cdcTracking,
        List<TableSchema> sourceSchemas,
        List<TableSchema> destSchemas,
        List<string> allSourceTableNames,
        Dictionary<string, int> captureInstanceCounts)
    {
        var results = new List<DiffResult>();
        var sourceLookup = sourceSchemas.ToDictionary(t => t.FullName, StringComparer.OrdinalIgnoreCase);
        var destLookup = destSchemas.ToDictionary(t => t.FullName, StringComparer.OrdinalIgnoreCase);
        var allSourceNamesSet = new HashSet<string>(allSourceTableNames, StringComparer.OrdinalIgnoreCase);

        // Group CDC tracking by table (use first capture instance per table for simplicity)
        var cdcByTable = cdcTracking
            .GroupBy(c => c.FullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (trackedFullName, cdc) in cdcByTable)
        {
            var hasSource = sourceLookup.TryGetValue(trackedFullName, out var srcTable);
            var hasDest = destLookup.TryGetValue(trackedFullName, out var dstTable);

            // ---- Issues #6, #7, #9: Tracked table missing from source ----
            if (!hasSource)
            {
                DetectMissingTable(results, cdc, allSourceNamesSet);
                continue;
            }

            if (!hasDest || srcTable is null || dstTable is null)
                continue;

            // ---- Issue #5: Primary key changed ----
            DetectPkChange(results, cdc, srcTable, dstTable);

            // ---- Issue #8: CDC index changed ----
            // (handled separately after this loop using index queries)

            // ---- Column-level diffs ----
            DetectColumnDiffs(results, cdc, srcTable, dstTable);

            // ---- Capture instance limit ----
            if (captureInstanceCounts.TryGetValue(trackedFullName, out var count))
            {
                results.Add(new DiffResult
                {
                    Issue = IssueType.CaptureInstanceLimit,
                    Severity = Severity.Info,
                    Tag = "INSTANCE-LIMIT",
                    SchemaName = cdc.SchemaName,
                    TableName = cdc.TableName,
                    Description = $"Already has {count} capture instances",
                    Detail = $"Instance count: {count}",
                    Action = "Any change requiring CDC recreation needs an instance dropped first."
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Detect CDC index changes (Issue #8). Call this with index data from both servers.
    /// </summary>
    public static List<DiffResult> CompareIndexes(
        List<CdcTrackingInfo> cdcTracking,
        Dictionary<string, IndexInfo?> sourceIndexes,
        Dictionary<string, IndexInfo?> destIndexes,
        List<DiffResult> existingResults)
    {
        var results = new List<DiffResult>();

        // Skip tables already flagged as missing/renamed/schema-changed
        var alreadyFlagged = existingResults
            .Where(r => r.Issue is IssueType.TableDropped or IssueType.TableRenamed or IssueType.SchemaChanged)
            .Select(r => r.FullTableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var cdc in cdcTracking.Where(c => c.CdcIndexName is not null && !alreadyFlagged.Contains(c.FullName)))
        {
            var key = $"{cdc.FullName}|{cdc.CdcIndexName}";
            sourceIndexes.TryGetValue(key, out var srcIdx);
            destIndexes.TryGetValue(key, out var dstIdx);

            if (dstIdx is null)
                continue; // Can't compare if destination index info is missing

            if (srcIdx is null)
            {
                results.Add(new DiffResult
                {
                    Issue = IssueType.CdcIndexChanged,
                    Severity = Severity.Critical,
                    Tag = "IDX-CHANGE",
                    SchemaName = cdc.SchemaName,
                    TableName = cdc.TableName,
                    Description = $"CDC index '{cdc.CdcIndexName}' missing or recreated in source",
                    Detail = $"Destination index columns: ({string.Join(", ", dstIdx.Columns)})",
                    Action = "Disable CDC → run migration → re-enable CDC with correct index."
                });
                continue;
            }

            var srcCols = string.Join(", ", srcIdx.Columns);
            var dstCols = string.Join(", ", dstIdx.Columns);

            if (!srcCols.Equals(dstCols, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new DiffResult
                {
                    Issue = IssueType.CdcIndexChanged,
                    Severity = Severity.Critical,
                    Tag = "IDX-CHANGE",
                    SchemaName = cdc.SchemaName,
                    TableName = cdc.TableName,
                    Description = $"CDC index '{cdc.CdcIndexName}' columns changed",
                    Detail = $"Destination: ({dstCols})\n  Source:      ({srcCols})",
                    Action = "Disable CDC → run migration → re-enable CDC with correct index."
                });
            }
        }

        return results;
    }

    private static void DetectMissingTable(
        List<DiffResult> results,
        CdcTrackingInfo cdc,
        HashSet<string> allSourceTableNames)
    {
        // Check if the table exists under a different schema in source (Issue #9)
        var sameNameDifferentSchema = allSourceTableNames
            .Where(n => n.Split('.').Last().Equals(cdc.TableName, StringComparison.OrdinalIgnoreCase)
                        && !n.Equals(cdc.FullName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sameNameDifferentSchema.Count > 0)
        {
            results.Add(new DiffResult
            {
                Issue = IssueType.SchemaChanged,
                Severity = Severity.Critical,
                Tag = "SCHEMA-CHANGE",
                SchemaName = cdc.SchemaName,
                TableName = cdc.TableName,
                Description = $"CDC-tracked table moved to different schema",
                Detail = $"Destination: {cdc.FullName}\n  Source:      {string.Join(", ", sameNameDifferentSchema)}",
                Action = "Disable CDC → run migration → re-enable CDC."
            });
            return;
        }

        // Check if it might be a rename — look for tables in source that
        // don't exist in destination with the same schema (Issue #6 vs #7)
        // For now, flag as missing and let user determine
        var isTableGone = !allSourceTableNames.Any(n =>
            n.Equals(cdc.FullName, StringComparison.OrdinalIgnoreCase));

        if (isTableGone)
        {
            // Try to detect rename: same schema, table with similar column structure
            // For POC, we flag as "missing — renamed or dropped?"
            results.Add(new DiffResult
            {
                Issue = IssueType.TableDropped,
                Severity = Severity.Critical,
                Tag = "TBL-MISSING",
                SchemaName = cdc.SchemaName,
                TableName = cdc.TableName,
                Description = "CDC-tracked table missing from source (dropped or renamed)",
                Detail = $"Capture instance: {cdc.CaptureInstance}\n  Tracked columns: {string.Join(", ", cdc.TrackedColumns)}",
                Action = "If dropped: Disable CDC → run migration. If renamed: Disable CDC → run migration → re-enable CDC."
            });
        }
    }

    private static void DetectPkChange(
        List<DiffResult> results,
        CdcTrackingInfo cdc,
        TableSchema srcTable,
        TableSchema dstTable)
    {
        var srcPk = string.Join(", ", srcTable.PrimaryKeyColumns);
        var dstPk = string.Join(", ", dstTable.PrimaryKeyColumns);

        if (!srcPk.Equals(dstPk, StringComparison.OrdinalIgnoreCase) && (srcPk.Length > 0 || dstPk.Length > 0))
        {
            results.Add(new DiffResult
            {
                Issue = IssueType.PrimaryKeyChanged,
                Severity = Severity.Critical,
                Tag = "PK-CHANGE",
                SchemaName = cdc.SchemaName,
                TableName = cdc.TableName,
                Description = "Primary key changed",
                Detail = $"Destination PK: ({dstPk})\n  Source PK:      ({srcPk})",
                Action = "Disable CDC → run migration → re-enable CDC. Alert downstream consumers."
            });
        }
    }

    private static void DetectColumnDiffs(
        List<DiffResult> results,
        CdcTrackingInfo cdc,
        TableSchema srcTable,
        TableSchema dstTable)
    {
        var srcCols = srcTable.Columns.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);
        var dstCols = dstTable.Columns.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);
        var trackedCols = new HashSet<string>(cdc.TrackedColumns, StringComparer.OrdinalIgnoreCase);

        // Columns in destination but NOT in source (dropped)
        var droppedCols = dstCols.Keys.Where(c => !srcCols.ContainsKey(c)).ToList();
        // Columns in source but NOT in destination (added)
        var addedCols = srcCols.Keys.Where(c => !dstCols.ContainsKey(c)).ToList();

        // ---- Issue #4: Detect possible rename (heuristic) ----
        var renames = DetectPossibleRenames(droppedCols, addedCols, srcCols, dstCols);

        // Remove renamed columns from add/drop lists
        foreach (var (oldName, newName) in renames)
        {
            droppedCols.Remove(oldName);
            addedCols.Remove(newName);

            results.Add(new DiffResult
            {
                Issue = IssueType.ColumnRenamed,
                Severity = trackedCols.Contains(oldName) ? Severity.Critical : Severity.High,
                Tag = "COL-RENAME",
                SchemaName = cdc.SchemaName,
                TableName = cdc.TableName,
                ColumnName = oldName,
                Description = $"Column possibly renamed: {oldName} → {newName}",
                Detail = $"Same data type and ordinal position detected.\n  Tracked by CDC: {trackedCols.Contains(oldName)}",
                Action = "Disable CDC → run migration → re-enable CDC."
            });
        }

        // ---- Issue #2: Column dropped ----
        foreach (var col in droppedCols)
        {
            var isTracked = trackedCols.Contains(col);
            results.Add(new DiffResult
            {
                Issue = IssueType.ColumnDropped,
                Severity = isTracked ? Severity.Critical : Severity.Low,
                Tag = "COL-DROP",
                SchemaName = cdc.SchemaName,
                TableName = cdc.TableName,
                ColumnName = col,
                Description = isTracked
                    ? $"Tracked column dropped in source"
                    : $"Untracked column dropped in source",
                Detail = $"Column in CDC capture instance: {isTracked}",
                Action = isTracked
                    ? "Disable CDC before migration, re-enable after."
                    : "No CDC action required (column not in capture instance)."
            });
        }

        // ---- Issue #1: Column added ----
        foreach (var col in addedCols)
        {
            results.Add(new DiffResult
            {
                Issue = IssueType.ColumnAdded,
                Severity = Severity.Low,
                Tag = "COL-ADD",
                SchemaName = cdc.SchemaName,
                TableName = cdc.TableName,
                ColumnName = col,
                Description = "New column on tracked table (not auto-tracked by CDC)",
                Detail = $"New column: {col} ({srcCols[col].TypeName})",
                Action = "If CDC tracking needed: Disable CDC → migrate → re-enable with updated column list."
            });
        }

        // ---- Columns in both: check type / nullability ----
        foreach (var colName in srcCols.Keys.Where(c => dstCols.ContainsKey(c)))
        {
            var src = srcCols[colName];
            var dst = dstCols[colName];
            var isTracked = trackedCols.Contains(colName);

            if (!isTracked)
                continue;

            // ---- Issue #3: Data type changed ----
            if (!src.TypeName.Equals(dst.TypeName, StringComparison.OrdinalIgnoreCase)
                || src.MaxLength != dst.MaxLength
                || src.Precision != dst.Precision
                || src.Scale != dst.Scale)
            {
                results.Add(new DiffResult
                {
                    Issue = IssueType.ColumnTypeChanged,
                    Severity = Severity.High,
                    Tag = "COL-TYPE",
                    SchemaName = cdc.SchemaName,
                    TableName = cdc.TableName,
                    ColumnName = colName,
                    Description = $"Type changed ({FormatType(dst)} → {FormatType(src)})",
                    Detail = $"Destination: {FormatType(dst)}\n  Source:      {FormatType(src)}",
                    Action = "Disable CDC → run migration → re-enable CDC."
                });
            }

            // ---- Issue #10: Nullability changed ----
            if (src.IsNullable != dst.IsNullable)
            {
                results.Add(new DiffResult
                {
                    Issue = IssueType.NullabilityChanged,
                    Severity = Severity.Low,
                    Tag = "COL-NULL",
                    SchemaName = cdc.SchemaName,
                    TableName = cdc.TableName,
                    ColumnName = colName,
                    Description = $"Nullability changed ({(dst.IsNullable ? "NULL" : "NOT NULL")} → {(src.IsNullable ? "NULL" : "NOT NULL")})",
                    Detail = $"Destination: {(dst.IsNullable ? "NULL" : "NOT NULL")}\n  Source:      {(src.IsNullable ? "NULL" : "NOT NULL")}",
                    Action = "Disable CDC → migrate → re-enable if downstream systems are schema-sensitive."
                });
            }
        }
    }

    private static List<(string oldName, string newName)> DetectPossibleRenames(
        List<string> droppedCols,
        List<string> addedCols,
        Dictionary<string, ColumnInfo> srcCols,
        Dictionary<string, ColumnInfo> dstCols)
    {
        var renames = new List<(string, string)>();
        var usedAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dropped in droppedCols.ToList())
        {
            var dstCol = dstCols[dropped];

            // Find an added column with same type and same ordinal position
            var match = addedCols.FirstOrDefault(added =>
            {
                if (usedAdded.Contains(added)) return false;
                var srcCol = srcCols[added];
                return srcCol.TypeName.Equals(dstCol.TypeName, StringComparison.OrdinalIgnoreCase)
                       && srcCol.MaxLength == dstCol.MaxLength
                       && srcCol.ColumnId == dstCol.ColumnId;
            });

            if (match is not null)
            {
                renames.Add((dropped, match));
                usedAdded.Add(match);
            }
        }

        return renames;
    }

    private static string FormatType(ColumnInfo col)
    {
        return col.TypeName.ToLower() switch
        {
            "nvarchar" or "varchar" or "nchar" or "char" or "varbinary" or "binary"
                => col.MaxLength == -1
                    ? $"{col.TypeName}(max)"
                    : col.TypeName.StartsWith("n", StringComparison.OrdinalIgnoreCase)
                        ? $"{col.TypeName}({col.MaxLength / 2})"
                        : $"{col.TypeName}({col.MaxLength})",
            "decimal" or "numeric"
                => $"{col.TypeName}({col.Precision},{col.Scale})",
            _ => col.TypeName
        };
    }
}
