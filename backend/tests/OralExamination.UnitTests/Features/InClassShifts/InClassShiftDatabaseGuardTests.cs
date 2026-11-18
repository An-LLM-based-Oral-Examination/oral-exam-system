using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.InClassShifts;

public static class PostgresIntegrationTestDatabase
{
    private static readonly object MigrationSyncLock = new();
    private static readonly HashSet<string> MigratedDatabases = new(StringComparer.OrdinalIgnoreCase);

    public static string ValidateAndNormalizeTestConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "[TASK-002 GUARD] Chuỗi kết nối CSDL test không được để trống. Vui lòng cung cấp qua biến môi trường ORALEXAM_TEST_CONNECTION_STRING.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) ||
            !builder.Database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"[TASK-002 GUARD CHỐNG NHIỄM BẨN CSDL] Database '{builder.Database}' bắt buộc phải kết thúc bằng hậu tố '_test'. Tuyệt đối từ chối chạy trên database chính/production!");
        }

        return builder.ConnectionString;
    }

    public static string GetPostgresConnectionString()
    {
        var envConn = Environment.GetEnvironmentVariable("ORALEXAM_TEST_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(envConn))
        {
            var user = Environment.GetEnvironmentVariable("PGUSER")
                ?? Environment.GetEnvironmentVariable("POSTGRES_USER");
            var pass = Environment.GetEnvironmentVariable("PGPASSWORD")
                ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
            var host = Environment.GetEnvironmentVariable("PGHOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("PGPORT")
                ?? Environment.GetEnvironmentVariable("POSTGRES_PORT")
                ?? "5432";

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                var dotEnvValues = TryReadLocalDotEnvPostgresVariables();
                if (string.IsNullOrWhiteSpace(user) && dotEnvValues.TryGetValue("POSTGRES_USER", out var envFileUser))
                {
                    user = envFileUser;
                }

                if (string.IsNullOrWhiteSpace(pass) && dotEnvValues.TryGetValue("POSTGRES_PASSWORD", out var envFilePass))
                {
                    pass = envFilePass;
                }

                if (dotEnvValues.TryGetValue("POSTGRES_PORT", out var envFilePort) && !string.IsNullOrWhiteSpace(envFilePort))
                {
                    port = envFilePort;
                }
            }

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                throw new InvalidOperationException(
                    "[TASK-002 GUARD] Thiếu thông tin xác thực CSDL test! Hãy cung cấp chuỗi kết nối qua biến môi trường ORALEXAM_TEST_CONNECTION_STRING hoặc cặp biến (PGUSER, PGPASSWORD).");
            }

            var db = Environment.GetEnvironmentVariable("PGDATABASE_TEST") ?? "oralexam_integration_test";
            envConn = $"Host={host};Port={port};Database={db};Username={user};Password={pass};Include Error Detail=true;Timeout=5;";
        }

        var normalized = ValidateAndNormalizeTestConnectionString(envConn);
        EnsureTestDatabaseMigratedOnce(normalized);
        return normalized;
    }

    private static Dictionary<string, string> TryReadLocalDotEnvPostgresVariables()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
            {
                foreach (var rawLine in File.ReadAllLines(candidate))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var key = line[..separatorIndex].Trim();
                    if (!key.Equals("POSTGRES_USER", StringComparison.OrdinalIgnoreCase) &&
                        !key.Equals("POSTGRES_PASSWORD", StringComparison.OrdinalIgnoreCase) &&
                        !key.Equals("POSTGRES_PORT", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result[key] = value;
                    }
                }

                break;
            }

            current = current.Parent;
        }

        return result;
    }

    private static void EnsureTestDatabaseMigratedOnce(string validatedConnectionString)
    {
        var targetBuilder = new NpgsqlConnectionStringBuilder(validatedConnectionString);
        var targetDb = targetBuilder.Database!;

        lock (MigrationSyncLock)
        {
            if (MigratedDatabases.Contains(targetDb))
            {
                return;
            }

            var adminBuilder = new NpgsqlConnectionStringBuilder(validatedConnectionString)
            {
                Database = "postgres",
                Pooling = false
            };

            using (var adminConn = new NpgsqlConnection(adminBuilder.ConnectionString))
            {
                adminConn.Open();
                using var existsCmd = new NpgsqlCommand(
                    "SELECT 1 FROM pg_database WHERE datname = @dbName;",
                    adminConn);
                existsCmd.Parameters.AddWithValue("dbName", targetDb);
                var exists = existsCmd.ExecuteScalar() is not null;

                if (!exists)
                {
                    var escapedIdentifier = targetDb.Replace("\"", "\"\"");
                    using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{escapedIdentifier}\";", adminConn);
                    createCmd.ExecuteNonQuery();
                }
            }

            var options = new DbContextOptionsBuilder<OralExamDbContext>()
                .UseNpgsql(validatedConnectionString)
                .Options;

            using (var context = new OralExamDbContext(options))
            {
                PostgresSchemaReconciler.ReconcilePreMigrationAsync(context).GetAwaiter().GetResult();
                context.Database.Migrate();
                PostgresSchemaReconciler.ReconcilePostMigrationAsync(context).GetAwaiter().GetResult();
            }

            MigratedDatabases.Add(targetDb);
        }
    }
}

public sealed class InClassShiftDatabaseGuardTests
{
    [Fact(DisplayName = "TASK-002 Guard: Từ chối kết nối nếu tên database không kết thúc bằng _test")]
    public void DatabaseConnectionGuard_WhenDatabaseDoesNotEndWithTest_ThrowsInvalidOperationException()
    {
        const string connectionString =
            "Host=localhost;Port=5432;Database=oralexam_db;Username=oralexam;Password=oralexam_password;";

        Action act = () => PostgresIntegrationTestDatabase.ValidateAndNormalizeTestConnectionString(connectionString);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TASK-002 GUARD CHỐNG NHIỄM BẨN CSDL*");
    }
}
