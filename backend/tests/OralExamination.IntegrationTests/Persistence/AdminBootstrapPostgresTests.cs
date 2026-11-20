using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OralExamination.API.Configuration;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class AdminBootstrapPostgresTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditFailure_RollsBackAdminCreationOrReactivation(bool reactivate)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.ExecuteAsync("UPDATE users SET role = 'lecturer' WHERE role = 'admin'");
        await using var context = database.CreateContext();
        var retainedId = Guid.NewGuid();
        if (reactivate)
        {
            context.Users.Add(new OralExamination.Domain.Entities.User
            {
                Id = retainedId, Email = "rollback@example.test", Username = "rollback",
                FullName = "Retained admin", Role = "admin", IsActive = false,
                PasswordHash = "retained-hash", AuthVersion = 7
            });
            await context.SaveChangesAsync();
        }
        await database.ExecuteAsync("ALTER TABLE audit_logs ADD CONSTRAINT reject_bootstrap_audit CHECK (action NOT LIKE 'BOOTSTRAP_ADMIN_%')");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Email"] = "rollback@example.test",
            ["BootstrapAdmin:Username"] = "rollback",
            ["BootstrapAdmin:Password"] = "Unique-Bootstrap-Secret-89241!"
        }).Build();
        using var services = new ServiceCollection().BuildServiceProvider();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(context, services, configuration, NullLogger.Instance));

        await using var verify = database.CreateContext();
        (await verify.Users.AnyAsync(user => user.Role == "admin" && user.IsActive)).Should().BeFalse();
        if (reactivate)
        {
            var retained = await verify.Users.SingleAsync(user => user.Id == retainedId);
            retained.PasswordHash.Should().Be("retained-hash");
            retained.AuthVersion.Should().Be(7);
        }
        (await verify.AuditLogs.CountAsync(item => item.Action.StartsWith("BOOTSTRAP_ADMIN_"))).Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentBootstrap_CreatesExactlyOneAdminAndAudit_AndRestartPreservesForcedChange()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        // Only this disposable DB: preserve legacy identities/references but exercise no-admin bootstrap.
        await database.ExecuteAsync("UPDATE users SET role = 'lecturer' WHERE role = 'admin'");
        const string password = "Unique-Bootstrap-Secret-89241!";
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Email"] = "bootstrap@example.test",
            ["BootstrapAdmin:Username"] = "bootstrap",
            ["BootstrapAdmin:Password"] = password
        }).Build();
        using var services = new ServiceCollection().BuildServiceProvider();
        var hasher = new Pbkdf2PasswordHasher();
        var otherSettings = new ConfigurationBuilder().AddConfiguration(settings)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Email"] = "other-bootstrap@example.test",
                ["BootstrapAdmin:Username"] = "other-bootstrap"
            }).Build();
        // Match production DI: explicit transactions must run inside the retry strategy.
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(database.ConnectionString, postgres => postgres.EnableRetryOnFailure())
            .Options;
        await using var first = new OralExamDbContext(options);
        await using var second = new OralExamDbContext(options);

        await Task.WhenAll(
            DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(first, services, settings, NullLogger.Instance),
            DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(second, services, otherSettings, NullLogger.Instance));
        await using var verify = database.CreateContext();
        var admin = await verify.Users.SingleAsync(user => user.Role == "admin");
        admin.MustChangePassword.Should().BeTrue();
        hasher.Verify(password, admin.PasswordHash!).Should().BeTrue();
        (await verify.AuditLogs.CountAsync(item => item.Action == "BOOTSTRAP_ADMIN_CREATED")).Should().Be(1);

        await PostgresSchemaReconciler.ReconcilePostMigrationAsync(verify);
        await using var restarted = new OralExamDbContext(options);
        await DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(restarted, services, settings, NullLogger.Instance);
        (await restarted.Users.AsNoTracking().SingleAsync(user => user.Id == admin.Id)).MustChangePassword.Should().BeTrue();
    }
}
