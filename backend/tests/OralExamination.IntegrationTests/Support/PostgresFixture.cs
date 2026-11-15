using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Testcontainers.PostgreSql;
using Xunit;

namespace OralExamination.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Task05 PostgreSQL16";
}

/// <summary>No application connection strings/configuration are read by this fixture.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("oes_task05_control")
        .WithUsername("oes_task05")
        .WithPassword(Guid.NewGuid().ToString("N"))
        // Recovery tests must not inherit the module's performance-oriented fsync=off.
        .WithCommand("-c", "fsync=on", "-c", "full_page_writes=on", "-c", "synchronous_commit=on")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync()
    {
        try
        {
            var directory = Environment.GetEnvironmentVariable("OES_INTEGRATION_LOG_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                var (stdout, stderr) = await _container.GetLogsAsync();
                await File.WriteAllTextAsync(Path.Combine(directory, "postgres-container.log"), stdout + stderr);
            }
        }
        finally
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<TestDatabase> CreateDatabaseAsync(bool migrate = true)
    {
        var name = "oes_it_" + Guid.NewGuid().ToString("N");
        var control = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Pooling = false };
        await using (var connection = new NpgsqlConnection(control.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var database = new TestDatabase(control.ConnectionString, name);
        try
        {
            if (migrate)
            {
                await using var context = database.CreateContext();
                await context.Database.MigrateAsync();
            }
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }
}

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _controlConnectionString;
    private readonly string _name;
    private bool _disposed;

    internal TestDatabase(string controlConnectionString, string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^oes_it_[a-f0-9]{32}$"))
            throw new ArgumentException("Only generated test databases may be managed.", nameof(name));
        _controlConnectionString = controlConnectionString;
        _name = name;
        ConnectionString = new NpgsqlConnectionStringBuilder(controlConnectionString)
        {
            Database = name, Pooling = false, CommandTimeout = 30, IncludeErrorDetail = false
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public OralExamDbContext CreateContext(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<OralExamDbContext>()
        .UseNpgsql(ConnectionString, options => options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .AddInterceptors(new OneWayLockInterceptor())
        .AddInterceptors(interceptors)
        .Options);

    public async Task<int> ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("No SQL scalar returned."));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await using var connection = new NpgsqlConnection(_controlConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        _disposed = true;
    }
}
