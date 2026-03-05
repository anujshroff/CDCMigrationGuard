using System.Text.RegularExpressions;
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

    private static string ShortenConnectionString(string connStr)
    {
        var server = Regex.Match(connStr, @"Server\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
        var db = Regex.Match(connStr, @"Database\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
        if (server.Success && db.Success)
            return $"{server.Groups[1].Value.Trim()}/{db.Groups[1].Value.Trim()}";
        return connStr.Length > 60 ? connStr[..57] + "..." : connStr;
    }

    public static void Render(string sourceName, string destName, List<DiffResult> results)
    {
        var shortSource = ShortenConnectionString(sourceName);
        var shortDest = ShortenConnectionString(destName);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]CDC Migration Diff[/]").LeftJustified());
        AnsiConsole.MarkupLine($"  [dim]Source:[/]  {Markup.Escape(shortSource)}");
        AnsiConsole.MarkupLine($"  [dim]Dest:[/]    {Markup.Escape(shortDest)}");
        AnsiConsole.WriteLine();

        if (results.Count == 0)
        {
            var panel = new Panel("[bold green]No CDC issues detected. Migration is safe.[/]")
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green),
                Padding = new Padding(2, 0)
            };
            AnsiConsole.Write(panel);
            return;
        }

        var grouped = results
            .OrderByDescending(r => r.Severity)
            .GroupBy(r => r.Severity);

        foreach (var group in grouped)
        {
            var (color, spectreColor) = group.Key switch
            {
                Severity.Critical => ("red", Color.Red),
                Severity.High => ("yellow", Color.Yellow),
                Severity.Low => ("blue", Color.Blue),
                _ => ("grey", Color.Grey)
            };

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[bold {color}] {group.Key.ToString().ToUpper()} ({group.Count()}) [/]").LeftJustified().RuleStyle(color));

            var table = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(spectreColor)
                .AddColumn(new TableColumn("[dim]Tag[/]").NoWrap())
                .AddColumn(new TableColumn("[dim]Target[/]").NoWrap())
                .AddColumn("[dim]Description[/]")
                .AddColumn("[dim]Action[/]");

            foreach (var r in group)
            {
                var target = r.ColumnName is not null
                    ? $"{r.FullTableName}.{r.ColumnName}"
                    : r.FullTableName;

                var desc = Markup.Escape(r.Description);
                if (!string.IsNullOrEmpty(r.Detail))
                {
                    var detailLines = r.Detail.Split('\n')
                        .Select(l => $"[dim]{Markup.Escape(l.TrimStart())}[/]");
                    desc += "\n" + string.Join("\n", detailLines);
                }

                table.AddRow(
                    $"[{color}]{Markup.Escape(r.Tag)}[/]",
                    $"[bold]{Markup.Escape(target)}[/]",
                    desc,
                    $"[italic]{Markup.Escape(r.Action)}[/]"
                );
            }

            AnsiConsole.Write(table);
        }

        // Summary
        var critical = results.Count(r => r.Severity == Severity.Critical);
        var high = results.Count(r => r.Severity == Severity.High);
        var low = results.Count(r => r.Severity == Severity.Low);
        var info = results.Count(r => r.Severity == Severity.Info);

        AnsiConsole.WriteLine();
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Summary[/]")
            .AddColumn(new TableColumn("[red]Critical[/]").Centered())
            .AddColumn(new TableColumn("[yellow]High[/]").Centered())
            .AddColumn(new TableColumn("[blue]Low[/]").Centered())
            .AddColumn(new TableColumn("[grey]Info[/]").Centered());

        summaryTable.AddRow(
            critical > 0 ? $"[bold red]{critical}[/]" : $"[dim]{critical}[/]",
            high > 0 ? $"[bold yellow]{high}[/]" : $"[dim]{high}[/]",
            low > 0 ? $"[bold blue]{low}[/]" : $"[dim]{low}[/]",
            info > 0 ? $"[grey]{info}[/]" : $"[dim]{info}[/]"
        );

        AnsiConsole.Write(summaryTable);

        if (critical > 0)
        {
            AnsiConsole.WriteLine();
            var warning = new Panel("[bold red]Migration WILL FAIL if CDC is not disabled first for critical items.[/]")
            {
                Border = BoxBorder.Heavy,
                BorderStyle = new Style(Color.Red),
                Padding = new Padding(2, 0)
            };
            AnsiConsole.Write(warning);
        }
    }
}
