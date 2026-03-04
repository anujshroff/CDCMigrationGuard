using AnujShroff.CDCMigrationGuard.Models;
using System.Text.Json;

namespace AnujShroff.CDCMigrationGuard.Output;

public class JsonFormatter : IReportFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Format(string sourceName, string destName, List<DiffResult> results)
    {
        var report = new
        {
            source = sourceName,
            destination = destName,
            summary = new
            {
                total = results.Count,
                critical = results.Count(r => r.Severity == Severity.Critical),
                high = results.Count(r => r.Severity == Severity.High),
                low = results.Count(r => r.Severity == Severity.Low),
                info = results.Count(r => r.Severity == Severity.Info)
            },
            issues = results
                .OrderByDescending(r => r.Severity)
                .Select(r => new
                {
                    tag = r.Tag,
                    severity = r.Severity.ToString().ToLower(),
                    table = r.FullTableName,
                    column = r.ColumnName,
                    description = r.Description,
                    detail = r.Detail,
                    action = r.Action
                })
        };

        return JsonSerializer.Serialize(report, Options);
    }
}
