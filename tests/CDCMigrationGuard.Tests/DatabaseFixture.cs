using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Text.RegularExpressions;
using Testcontainers.MsSql;
using Xunit;

namespace CDCMigrationGuard.Tests;

public partial class DatabaseFixture : IAsyncLifetime
{
    private MsSqlContainer _sourceContainer = null!;
    private MsSqlContainer _destinationContainer = null!;

    public string SourceConnectionString { get; private set; } = null!;
    public string DestinationConnectionString { get; private set; } = null!;

    private const string DatabaseName = "CdcTestDb";

    public async ValueTask InitializeAsync()
    {
        _sourceContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        _destinationContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

        await Task.WhenAll(
            _sourceContainer.StartAsync(),
            _destinationContainer.StartAsync());

        // Create the test database on each container
        await CreateDatabaseAsync(_sourceContainer.GetConnectionString());
        await CreateDatabaseAsync(_destinationContainer.GetConnectionString());

        SourceConnectionString = SetDatabase(_sourceContainer.GetConnectionString(), DatabaseName);
        DestinationConnectionString = SetDatabase(_destinationContainer.GetConnectionString(), DatabaseName);

        // Source: baseline + migrations
        await ExecuteScriptAsync(SourceConnectionString, "BaselineSchema.sql");
        await ExecuteScriptAsync(SourceConnectionString, "SourceMigrations.sql");

        // Destination: baseline + CDC
        await ExecuteScriptAsync(DestinationConnectionString, "BaselineSchema.sql");
        await ExecuteScriptAsync(DestinationConnectionString, "EnableCdc.sql");
    }

    private static async Task CreateDatabaseAsync(string masterConnectionString)
    {
        await using var conn = new SqlConnection(masterConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand($"CREATE DATABASE [{DatabaseName}]", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string SetDatabase(string connectionString, string database)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database };
        return builder.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(
            _sourceContainer.DisposeAsync().AsTask(),
            _destinationContainer.DisposeAsync().AsTask());
    }

    private static async Task ExecuteScriptAsync(string connectionString, string scriptName)
    {
        var sql = ReadEmbeddedScript(scriptName);
        var batches = SplitOnGo(sql);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            await using var cmd = new SqlCommand(trimmed, conn);
            cmd.CommandTimeout = 30;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string ReadEmbeddedScript(string scriptName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith($".Scripts.{scriptName}", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static List<string> SplitOnGo(string sql)
    {
        return [.. GoPattern().Split(sql)];
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoPattern();
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
