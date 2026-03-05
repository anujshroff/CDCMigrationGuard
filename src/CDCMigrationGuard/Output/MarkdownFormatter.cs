using AnujShroff.CDCMigrationGuard.Models;
using System.Text;

namespace AnujShroff.CDCMigrationGuard.Output;

public class MarkdownFormatter : IReportFormatter
{
    private static string EscapePipe(string text) => text.Replace("|", "\\|");

    public string Format(string sourceName, string destName, List<DiffResult> results)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# CDC Migration Diff");
        sb.AppendLine();
        sb.AppendLine($"- **Source:** {EscapePipe(sourceName)}");
        sb.AppendLine($"- **Dest:** {EscapePipe(destName)}");
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
            sb.AppendLine($"## {group.Key.ToString().ToUpper()} ({group.Count()})");
            sb.AppendLine();
            sb.AppendLine("| Tag | Target | Description | Action |");
            sb.AppendLine("|-----|--------|-------------|--------|");

            foreach (var r in group)
            {
                var target = r.ColumnName is not null
                    ? $"{r.FullTableName}.{r.ColumnName}"
                    : r.FullTableName;

                var desc = EscapePipe(r.Description);
                if (!string.IsNullOrEmpty(r.Detail))
                    desc += "<br>" + string.Join("<br>", r.Detail.Split('\n').Select(l => EscapePipe(l.TrimStart())));

                var action = string.Join("<br>", r.Action.Split('\n').Select(l => EscapePipe(l.TrimStart())));

                sb.AppendLine($"| {EscapePipe(r.Tag)} | {EscapePipe(target)} | {desc} | {action} |");
            }

            sb.AppendLine();
        }

        var critical = results.Count(r => r.Severity == Severity.Critical);
        var high = results.Count(r => r.Severity == Severity.High);
        var low = results.Count(r => r.Severity == Severity.Low);
        var info = results.Count(r => r.Severity == Severity.Info);

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"**Summary:** {critical} critical, {high} high, {low} low, {info} info");

        if (critical > 0)
        {
            sb.AppendLine();
            sb.AppendLine("> **Migration WILL FAIL if CDC is not disabled first for critical items.**");
        }

        return sb.ToString();
    }
}
