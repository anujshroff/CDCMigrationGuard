using AnujShroff.CDCMigrationGuard.Models;
using Spectre.Console;

namespace AnujShroff.CDCMigrationGuard.Output;

public class TextFormatter : IReportFormatter
{
    public string Format(string sourceName, string destName, List<DiffResult> results)
    {
        // This builds a string but we also render to console directly for colors
        return string.Empty; // Not used — we render directly
    }

    public static void Render(string sourceName, string destName, List<DiffResult> results)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]CDC Migration Diff: {Markup.Escape(sourceName)} → {Markup.Escape(destName)}[/]").LeftJustified());
        AnsiConsole.WriteLine();

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No CDC issues detected. Migration is safe.[/]");
            return;
        }

        var grouped = results
            .OrderByDescending(r => r.Severity)
            .GroupBy(r => r.Severity);

        foreach (var group in grouped)
        {
            var color = group.Key switch
            {
                Severity.Critical => "red",
                Severity.High => "yellow",
                Severity.Low => "blue",
                _ => "grey"
            };

            AnsiConsole.MarkupLine($"[bold {color}]{group.Key.ToString().ToUpper()}[/]");
            AnsiConsole.MarkupLine($"[{color}]{new string('-', group.Key.ToString().Length + 4)}[/]");

            foreach (var r in group)
            {
                var target = r.ColumnName is not null
                    ? $"{r.FullTableName}.{r.ColumnName}"
                    : r.FullTableName;

                AnsiConsole.MarkupLine($"[{color}][[{Markup.Escape(r.Tag)}]][/] [bold]{Markup.Escape(target)}[/] — {Markup.Escape(r.Description)}");

                if (!string.IsNullOrEmpty(r.Detail))
                {
                    foreach (var line in r.Detail.Split('\n'))
                        AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(line.TrimStart())}[/]");
                }

                AnsiConsole.MarkupLine($"  [italic]Action: {Markup.Escape(r.Action)}[/]");
                AnsiConsole.WriteLine();
            }
        }

        // Summary
        var critical = results.Count(r => r.Severity == Severity.Critical);
        var high = results.Count(r => r.Severity == Severity.High);
        var low = results.Count(r => r.Severity == Severity.Low);
        var info = results.Count(r => r.Severity == Severity.Info);

        AnsiConsole.Write(new Rule().LeftJustified());
        AnsiConsole.MarkupLine(
            $"[bold]Summary:[/] [red]{critical} critical[/], [yellow]{high} high[/], [blue]{low} low[/], [grey]{info} info[/]");

        if (critical > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold red]⚠ Migration WILL FAIL on production if CDC is not disabled first for critical items.[/]");
        }
    }
}
