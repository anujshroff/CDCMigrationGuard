using AnujShroff.CDCMigrationGuard.Commands;
using System.CommandLine;

// --- compare command ---
var sourceArg = new Argument<string>("source") { Description = "Source connection string" };
var destArg = new Argument<string>("destination") { Description = "Destination connection string" };
var formatOption = new Option<string>("--format") { Description = "Output format: text, markdown, json", DefaultValueFactory = _ => "text" };
var outputOption = new Option<string?>("--output") { Description = "Write report to file instead of stdout" };

var compareCmd = new Command("compare", "Compare source and destination schemas for CDC issues")
{
    sourceArg, destArg, formatOption, outputOption
};

compareCmd.SetAction(async (parseResult, ct) =>
{
    var source = parseResult.GetValue(sourceArg)!;
    var dest = parseResult.GetValue(destArg)!;
    var format = parseResult.GetValue(formatOption) ?? "text";
    var output = parseResult.GetValue(outputOption);
    Environment.ExitCode = await CompareCommand.RunAsync(source, dest, format, output);
});

// --- check command ---
var serverArg = new Argument<string>("server") { Description = "Connection string to check" };

var checkCmd = new Command("check", "Test connectivity and list CDC-tracked tables")
{
    serverArg
};

checkCmd.SetAction(async (parseResult, ct) =>
{
    var server = parseResult.GetValue(serverArg)!;
    Environment.ExitCode = await CheckCommand.RunAsync(server);
});

// --- root ---
var root = new RootCommand("CDC Migration Diff Tool — detect CDC issues before running migrations")
{
    compareCmd, checkCmd
};

var parseResult = root.Parse(args);
return await parseResult.InvokeAsync();
