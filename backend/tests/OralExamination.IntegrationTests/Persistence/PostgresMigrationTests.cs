using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class PostgresMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task LegacyUpdateDbAlreadyApplied_UpgradeRepairsMissingSchema_AndPreservesData()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using (var legacy = database.CreateContext())
        {
            await legacy.GetService<IMigrator>().MigrateAsync("20261108141117_UpdateDB");
        }

        await database.ExecuteAsync("CREATE TABLE upgrade_probe (value integer); INSERT INTO upgrade_probe VALUES (42)");
        await using (var current = database.CreateContext())
        {
            await current.Database.MigrateAsync();
            await current.Database.MigrateAsync();
            (await current.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }

        (await database.ScalarAsync<int>("SELECT value FROM upgrade_probe")).Should().Be(42);
        (await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='classes' AND column_name='join_code'"))
            .Should().Be(1);
        (await database.ScalarAsync<long>("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='courses' AND column_name='mock_exam_duration_minutes'"))
            .Should().Be(1);
    }

    [Fact]
    public async Task EmptyDatabase_AllProductionMigrationsApply_AndSecondApplyIsIdempotent()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using var context = database.CreateContext();
        var expected = context.Database.GetMigrations().ToArray();
        expected.Should().NotBeEmpty();

        await context.Database.MigrateAsync();
        (await context.Database.GetAppliedMigrationsAsync()).Should().Equal(expected);
        await context.Database.MigrateAsync();

        (await context.Database.GetAppliedMigrationsAsync()).Should().Equal(expected);
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await database.ScalarAsync<string>("SHOW server_version")).Should().StartWith("16.");
    }

    [Fact]
    public async Task SeparateTestDatabases_DoNotShareRecords()
    {
        await using var first = await fixture.CreateDatabaseAsync(migrate: false);
        await using var second = await fixture.CreateDatabaseAsync(migrate: false);
        await first.ExecuteAsync("CREATE TABLE isolation_probe (value integer); INSERT INTO isolation_probe VALUES (42)");

        (await first.ScalarAsync<long>("SELECT count(*) FROM isolation_probe")).Should().Be(1);
        (await second.ScalarAsync<long>("SELECT count(*) FROM information_schema.tables WHERE table_name = 'isolation_probe'"))
            .Should().Be(0);
    }

    [Fact]
    public async Task AppealWorkflowMigration_MapsLegacyStates_AndEnforcesNewConstraint()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using (var legacy = database.CreateContext())
        {
            await legacy.GetService<IMigrator>().MigrateAsync("20261114030000_AddGradePublicationTime");
        }

        (await database.ScalarAsync<string>("SELECT status FROM appeal_requests LIMIT 1"))
            .Should().Be("PENDING");

        await using (var current = database.CreateContext())
        {
            await current.Database.MigrateAsync();
        }

        (await database.ScalarAsync<string>("SELECT status FROM appeal_requests LIMIT 1"))
            .Should().Be("SUBMITTED_TO_EXAM_OFFICE");
        (await database.ScalarAsync<int>("SELECT character_maximum_length FROM information_schema.columns WHERE table_name='appeal_requests' AND column_name='status'"))
            .Should().Be(40);

        var invalidWrite = async () => await database.ExecuteAsync(
            "UPDATE appeal_requests SET status='PENDING' WHERE id=(SELECT id FROM appeal_requests LIMIT 1)");
        (await invalidWrite.Should().ThrowAsync<PostgresException>()).Which.SqlState
            .Should().Be(PostgresErrorCodes.CheckViolation);
    }
}
