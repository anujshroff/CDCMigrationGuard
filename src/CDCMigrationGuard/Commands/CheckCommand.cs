using AnujShroff.CDCMigrationGuard.Services;
using Spectre.Console;

namespace AnujShroff.CDCMigrationGuard.Commands;

public static class CheckCommand
{
    public static async Task<int> RunAsync(string connStr)
    {
        try
        {
            var schemaReader = new SchemaReader(connStr);
            var cdcReader = new CdcMetadataReader(connStr);

            // Test connectivity
            AnsiConsole.MarkupLine("[dim]Connecting...[/]");
            await schemaReader.TestConnectionAsync();
            AnsiConsole.MarkupLine("[green]Connection successful.[/]");

            // Check CDC
            var cdcEnabled = await cdcReader.IsCdcEnabledAsync();
            if (!cdcEnabled)
            {
                AnsiConsole.MarkupLine("[yellow]CDC is NOT enabled on this database.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine("[green]CDC is enabled.[/]");

            // List tracked tables
            var tracked = await cdcReader.GetTrackedTablesAsync();
            if (tracked.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No CDC-tracked tables found.[/]");
                return 0;
            }

            var table = new Table();
            table.AddColumn("Table");
            table.AddColumn("Capture Instance");
            table.AddColumn("CDC Index");
            table.AddColumn("Tracked Columns");

            foreach (var t in tracked)
            {
                table.AddRow(
                    t.FullName,
                    t.CaptureInstance,
                    t.CdcIndexName ?? "(PK)",
                    string.Join(", ", t.TrackedColumns));
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
