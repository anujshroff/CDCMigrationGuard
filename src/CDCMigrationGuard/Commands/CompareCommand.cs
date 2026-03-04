using AnujShroff.CDCMigrationGuard.Models;
using AnujShroff.CDCMigrationGuard.Output;
using AnujShroff.CDCMigrationGuard.Services;
using Spectre.Console;

namespace AnujShroff.CDCMigrationGuard.Commands;

public static class CompareCommand
{
    public static async Task<int> RunAsync(
        string sourceConnStr, string destConnStr, string format, string? output)
    {
        try
        {
            var sourceReader = new SchemaReader(sourceConnStr);
            var destReader = new SchemaReader(destConnStr);
            var cdcReader = new CdcMetadataReader(destConnStr);

            // Step 1: Check CDC is enabled on destination
            AnsiConsole.MarkupLine("[dim]Checking CDC status on destination...[/]");
            if (!await cdcReader.IsCdcEnabledAsync())
            {
                AnsiConsole.MarkupLine("[yellow]CDC is not enabled on the destination database.[/]");
                return 1;
            }

            // Step 2: Read CDC tracking info from destination
            AnsiConsole.MarkupLine("[dim]Reading CDC metadata from destination...[/]");
            var cdcTracking = await cdcReader.GetTrackedTablesAsync();
            if (cdcTracking.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No CDC-tracked tables found on destination.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[dim]Found {cdcTracking.Count} CDC capture instance(s) across " +
                $"{cdcTracking.Select(c => c.FullName).Distinct().Count()} table(s).[/]");

            // Step 3: Get all table names from source (for rename/schema detection)
            AnsiConsole.MarkupLine("[dim]Reading table list from source...[/]");
            var allSourceTables = await sourceReader.ReadAllTableNamesAsync();

            // Step 4: Read schemas for tracked tables from both source and destination
            var trackedTableNames = cdcTracking.Select(c => c.FullName).Distinct().ToList();

            AnsiConsole.MarkupLine("[dim]Reading schemas from source and destination...[/]");
            var sourceSchemas = await sourceReader.ReadSchemasAsync(trackedTableNames);
            var destSchemas = await destReader.ReadSchemasAsync(trackedTableNames);

            // Also read source schemas for tables that might have been renamed
            // (read schemas for all source tables that share a table name with tracked tables)
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

            // Step 5: Read capture instance counts
            var instanceCounts = await cdcReader.GetCaptureInstanceCountsAsync();

            // Step 6: Read index info for CDC-tracked indexes
            AnsiConsole.MarkupLine("[dim]Comparing CDC indexes...[/]");
            var sourceIndexes = new Dictionary<string, IndexInfo?>();
            var destIndexes = new Dictionary<string, IndexInfo?>();

            foreach (var cdc in cdcTracking.Where(c => c.CdcIndexName is not null))
            {
                var key = $"{cdc.FullName}|{cdc.CdcIndexName}";
                if (!sourceIndexes.ContainsKey(key))
                {
                    sourceIndexes[key] = await sourceReader.ReadIndexAsync(
                        cdc.SchemaName, cdc.TableName, cdc.CdcIndexName!);
                }
                if (!destIndexes.ContainsKey(key))
                {
                    destIndexes[key] = await destReader.ReadIndexAsync(
                        cdc.SchemaName, cdc.TableName, cdc.CdcIndexName!);
                }
            }

            // Step 7: Diff
            AnsiConsole.MarkupLine("[dim]Comparing schemas...[/]");
            var differ = new SchemaDiffer();
            var results = SchemaDiffer.Compare(cdcTracking, sourceSchemas, destSchemas, allSourceTables, instanceCounts);
            results.AddRange(SchemaDiffer.CompareIndexes(cdcTracking, sourceIndexes, destIndexes, results));

            // Sort by severity descending
            results = [.. results.OrderByDescending(r => r.Severity).ThenBy(r => r.FullTableName)];

            // Step 8: Output
            if (format.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                var formatter = new TextFormatter();
                TextFormatter.Render(sourceConnStr, destConnStr, results);
            }
            else
            {
                IReportFormatter formatter = format.ToLower() switch
                {
                    "markdown" => new MarkdownFormatter(),
                    "json" => new JsonFormatter(),
                    _ => new MarkdownFormatter()
                };

                var report = formatter.Format(sourceConnStr, destConnStr, results);

                if (output is not null)
                {
                    await File.WriteAllTextAsync(output, report);
                    AnsiConsole.MarkupLine($"[green]Report written to {Markup.Escape(output)}[/]");
                }
                else
                {
                    Console.WriteLine(report);
                }
            }

            return results.Any(r => r.Severity == Severity.Critical) ? 2 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
