using System.Text.RegularExpressions;
using AnujShroff.CDCMigrationGuard.Models;
using AnujShroff.CDCMigrationGuard.Output;
using AnujShroff.CDCMigrationGuard.Services;
using Spectre.Console;

namespace AnujShroff.CDCMigrationGuard.Commands;

public static class CompareCommand
{
    private static string ShortenConnectionString(string connStr)
    {
        var server = Regex.Match(connStr, @"Server\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
        var db = Regex.Match(connStr, @"Database\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
        if (server.Success && db.Success)
            return $"{server.Groups[1].Value.Trim()}/{db.Groups[1].Value.Trim()}";
        return connStr.Length > 60 ? connStr[..57] + "..." : connStr;
    }

    public static async Task<int> RunAsync(
        string sourceConnStr, string destConnStr, string format, string? output)
    {
        try
        {
            var sourceReader = new SchemaReader(sourceConnStr);
            var destReader = new SchemaReader(destConnStr);
            var cdcReader = new CdcMetadataReader(destConnStr);

            List<CdcTrackingInfo> cdcTracking = null!;
            List<string> allSourceTables = null!;
            List<TableSchema> sourceSchemas = null!;
            List<TableSchema> destSchemas = null!;
            Dictionary<string, int> instanceCounts = null!;
            Dictionary<string, IndexInfo?> sourceIndexes = null!;
            Dictionary<string, IndexInfo?> destIndexes = null!;
            List<DiffResult> results = null!;

            var earlyExit = false;
            var earlyExitCode = 0;

            await AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync("Checking CDC status on destination...", async ctx =>
            {
                if (!await cdcReader.IsCdcEnabledAsync())
                {
                    AnsiConsole.MarkupLine("[yellow]CDC is not enabled on the destination database.[/]");
                    earlyExit = true;
                    earlyExitCode = 1;
                    return;
                }

                ctx.Status("Reading CDC metadata from destination...");
                cdcTracking = await cdcReader.GetTrackedTablesAsync();
                if (cdcTracking.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No CDC-tracked tables found on destination.[/]");
                    earlyExit = true;
                    return;
                }

                var tableCount = cdcTracking.Select(c => c.FullName).Distinct().Count();
                ctx.Status($"Found {cdcTracking.Count} capture instance(s) across {tableCount} table(s). Reading schemas...");

                allSourceTables = await sourceReader.ReadAllTableNamesAsync();
                var trackedTableNames = cdcTracking.Select(c => c.FullName).Distinct().ToList();
                sourceSchemas = await sourceReader.ReadSchemasAsync(trackedTableNames);
                destSchemas = await destReader.ReadSchemasAsync(trackedTableNames);

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

                instanceCounts = await cdcReader.GetCaptureInstanceCountsAsync();

                ctx.Status("Comparing CDC indexes...");
                sourceIndexes = new Dictionary<string, IndexInfo?>();
                destIndexes = new Dictionary<string, IndexInfo?>();
                foreach (var cdc in cdcTracking.Where(c => c.CdcIndexName is not null))
                {
                    var key = $"{cdc.FullName}|{cdc.CdcIndexName}";
                    if (!sourceIndexes.ContainsKey(key))
                        sourceIndexes[key] = await sourceReader.ReadIndexAsync(
                            cdc.SchemaName, cdc.TableName, cdc.CdcIndexName!);
                    if (!destIndexes.ContainsKey(key))
                        destIndexes[key] = await destReader.ReadIndexAsync(
                            cdc.SchemaName, cdc.TableName, cdc.CdcIndexName!);
                }

                ctx.Status("Comparing schemas...");
                results = SchemaDiffer.Compare(cdcTracking, sourceSchemas, destSchemas, allSourceTables, instanceCounts);
                results.AddRange(SchemaDiffer.CompareIndexes(cdcTracking, sourceIndexes, destIndexes, results));
                results = [.. results.OrderByDescending(r => r.Severity).ThenBy(r => r.FullTableName)];
            });

            if (earlyExit)
                return earlyExitCode;

            // Sort by severity descending (already done above, kept for clarity)

            // Step 8: Output
            if (format.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                var formatter = new TextFormatter();
                var shortSource = ShortenConnectionString(sourceConnStr);
                var shortDest = ShortenConnectionString(destConnStr);
                TextFormatter.Render(shortSource, shortDest, results);
            }
            else
            {
                IReportFormatter formatter = format.ToLower() switch
                {
                    "markdown" => new MarkdownFormatter(),
                    "json" => new JsonFormatter(),
                    _ => new MarkdownFormatter()
                };

                var shortSrc = ShortenConnectionString(sourceConnStr);
                var shortDst = ShortenConnectionString(destConnStr);
                var report = formatter.Format(shortSrc, shortDst, results);

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
