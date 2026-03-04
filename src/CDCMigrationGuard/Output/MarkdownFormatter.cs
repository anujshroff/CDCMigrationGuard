using AnujShroff.CDCMigrationGuard.Models;
using System.Text;

namespace AnujShroff.CDCMigrationGuard.Output;

public class MarkdownFormatter : IReportFormatter
{
    public string Format(string sourceName, string destName, List<DiffResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# CDC Migration Diff: {sourceName} → {destName}");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("> No CDC issues detected. Migration is safe.");
            return sb.ToString();
        }

        var grouped = results
            .OrderByDescending(r => r.Severity)
            .GroupBy(r => r.Severity);

        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key.ToString().ToUpper()}");
            sb.AppendLine();

            foreach (var r in group)
            {
                var target = r.ColumnName is not null
                    ? $"{r.FullTableName}.{r.ColumnName}"
                    : r.FullTableName;

                sb.AppendLine($"### [{r.Tag}] {target}");
                sb.AppendLine($"**{r.Description}**");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(r.Detail))
                {
                    sb.AppendLine("```");
                    sb.AppendLine(r.Detail);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }

                sb.AppendLine($"**Action:** {r.Action}");
                sb.AppendLine();
            }
        }

        var critical = results.Count(r => r.Severity == Severity.Critical);
        var high = results.Count(r => r.Severity == Severity.High);
        var low = results.Count(r => r.Severity == Severity.Low);
        var info = results.Count(r => r.Severity == Severity.Info);

        sb.AppendLine("---");
        sb.AppendLine($"**Summary:** {critical} critical, {high} high, {low} low, {info} info");

        return sb.ToString();
    }
}
