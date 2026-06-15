using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using WebHomestay.Data;
using Xunit;

namespace WebHomestay.Tests.Infrastructure;

public sealed class PostgresIntegrationFixture : IAsyncLifetime
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=web_homestay_ai_tests;Username=postgres;Password=1510";

    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("WEBHOMESTAY_TEST_POSTGRES")
        ?? DefaultConnectionString;

    public bool IsAvailable { get; private set; } = true;
    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            await EnsureDatabaseExistsAsync();

            await using var context = CreateContext();
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector;");
            IsAvailable = true;
            UnavailableReason = null;
        }
        catch (PostgresException ex) when (ex.MessageText.Contains("extension \"vector\" is not available", StringComparison.OrdinalIgnoreCase))
        {
            IsAvailable = false;
            UnavailableReason = "PostgreSQL is reachable, but the pgvector extension is not installed on this machine.";
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            UnavailableReason = $"PostgreSQL integration fixture is unavailable: {ex.Message}";
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, options => options.UseVector())
            .Options;

        return new ApplicationDbContext(options);
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        var databaseName = string.IsNullOrWhiteSpace(builder.Database)
            ? "web_homestay_ai_tests"
            : builder.Database;
        builder.Database = "postgres";

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var checkCommand = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
        checkCommand.Parameters.AddWithValue("name", databaseName);
        var exists = await checkCommand.ExecuteScalarAsync();
        if (exists is not null)
        {
            return;
        }

        await using var createCommand = new NpgsqlCommand($@"CREATE DATABASE ""{databaseName}""", connection);
        await createCommand.ExecuteNonQueryAsync();
    }

    public bool EnsureAvailable()
    {
        if (!IsAvailable)
        {
            return false;
        }

        return true;
    }
}
