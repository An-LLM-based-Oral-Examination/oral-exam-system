using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

// Integration tests verifying refresh-token rotation under retrying and non-retrying EF Core strategies.
[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class RefreshTokenRetryDiagnosticTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentHandler_RetryConfigurationDeterminesTransactionFailure(bool retry)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database);
        await using var context = Context(database, retry);
        var issuer = new CountingIssuer();
        var handler = Handler(context, issuer);

        var result = await handler.Handle(new RefreshTokenCommand(raw), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        issuer.Calls.Should().Be(1);
        await AssertRotationAsync(database, userId, raw, result.Value.RefreshToken);
    }

    [Fact]
    public async Task WholeOperationStrategy_RotatesAndCommitsBeforeIssuing()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database);
        await using var context = Context(database);
        var issuer = new CountingIssuer(() =>
        {
            using var committed = database.CreateContext();
            var tokens = committed.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToList();
            tokens.Should().HaveCount(2, "rotation must be visible from another connection before issuing");
            tokens.Count(t => t.IsActive).Should().Be(1);
            tokens.Single(t => t.TokenHash == RefreshTokenCommandHandler.HashToken(raw)).IsRevoked.Should().BeTrue();
        });
        var result = await WrappedAsync(context, issuer, raw);
        result.IsSuccess.Should().BeTrue();
        issuer.Calls.Should().Be(1);
        await AssertRotationAsync(database, userId, raw, result.Value.RefreshToken);
        // A new context can refresh the issued replacement as well.
        await using var next = Context(database);
        (await WrappedAsync(next, new CountingIssuer(), result.Value.RefreshToken)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransientFailureBeforeSave_RetryRequiresClearingTrackedMutations(bool clearTracker)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database);
        var fault = new SaveFault(transient: true);
        await using var context = Context(database, interceptors: [fault]);
        var issuer = new CountingIssuer();
        var result = await WrappedAsync(context, issuer, raw, clearTracker);
        fault.Calls.Should().Be(clearTracker ? 2 : 1);
        if (clearTracker)
        {
            result.IsSuccess.Should().BeTrue();
            issuer.Calls.Should().Be(1);
            await AssertRotationAsync(database, userId, raw, result.Value.RefreshToken);
        }
        else
        {
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().StartWith("CONFLICT");
            issuer.Calls.Should().Be(0);
            await using var verify = database.CreateContext();
            (await verify.RefreshTokens.SingleAsync(t => t.UserId == userId)).RevokedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task PermanentSaveFailure_RollsBackWithoutIssuingOrRevoking()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database);
        await using var context = Context(database, interceptors: [new SaveFault(transient: false)]);
        var issuer = new CountingIssuer();
        var act = () => WrappedAsync(context, issuer, raw);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("diagnostic save failure");
        issuer.Calls.Should().Be(0);
        await using var verify = database.CreateContext();
        (await verify.RefreshTokens.SingleAsync(t => t.UserId == userId)).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task LostCommitAcknowledgement_CurrentCleanupMasksFailureAndLosesReplacementSecret()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database);
        var fault = new LostCommitAcknowledgement();
        await using var context = Context(database, interceptors: [fault]);
        var issuer = new CountingIssuer();
        var act = () => WrappedAsync(context, issuer, raw);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transaction has completed*");
        issuer.Calls.Should().Be(0);
        fault.Injected.Should().BeTrue();
        await using var verify = database.CreateContext();
        var tokens = await verify.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Count(t => t.IsActive).Should().Be(1);
        // The DB committed, but the client still has the old secret; raw replacement is not stored.
    }

    [Fact]
    public async Task CommitFailureBeforeCommit_RollsBackAndDoesNotIssue()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database);
        await using var context = Context(database, interceptors: [new CommitFault()]);
        var issuer = new CountingIssuer();
        var act = () => WrappedAsync(context, issuer, raw);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("diagnostic commit failure");
        issuer.Calls.Should().Be(0);
        await using var verify = database.CreateContext();
        (await verify.RefreshTokens.SingleAsync(t => t.UserId == userId)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentLock_RejectsContenderAndPreservesOwnerRotation()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database);
        await using var owner = Context(database);
        await using var contender = Context(database);
        var contenderIssuer = new CountingIssuer();
        await owner.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var scope = await owner.BeginRefreshTokenLockScopeAsync(RefreshTokenCommandHandler.HashToken(raw));
            scope.HasLock.Should().BeTrue();
            var result = await WrappedAsync(contender, contenderIssuer, raw);
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().StartWith("CONFLICT");
        });
        contenderIssuer.Calls.Should().Be(0);
        await using var next = Context(database);
        var rotation = await WrappedAsync(next, new CountingIssuer(), raw);
        rotation.IsSuccess.Should().BeTrue();
        await AssertRotationAsync(database, userId, raw, rotation.Value.RefreshToken);
        await using var repeated = Context(database);
        var duplicate = await WrappedAsync(repeated, new CountingIssuer(), raw);
        duplicate.Error.Should().StartWith("CONFLICT");
    }

    [Theory]
    [InlineData("expired", "hết hạn")]
    [InlineData("inactive", "bị khóa")]
    [InlineData("unknown", "không hợp lệ")]
    public async Task RejectedToken_DoesNotIssueOrMutate(string state, string expected)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database, state);
        await using var context = Context(database);
        var issuer = new CountingIssuer();
        var result = await WrappedAsync(context, issuer, state == "unknown" ? "missing-secret" : raw);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(expected);
        issuer.Calls.Should().Be(0);
        await using var verify = database.CreateContext();
        (await verify.RefreshTokens.SingleAsync(t => t.UserId == userId)).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReplayOutsideGrace_RevokesUserSessionsAndIncrementsVersionOnlyForThatUser()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var (userId, raw) = await SeedAsync(database);
        var (otherId, _) = await SeedAsync(database);
        await using (var context = Context(database))
        {
            (await WrappedAsync(context, new CountingIssuer(), raw)).IsSuccess.Should().BeTrue();
        }
        await using (var setup = database.CreateContext())
        {
            var old = await setup.RefreshTokens.SingleAsync(t => t.TokenHash == RefreshTokenCommandHandler.HashToken(raw));
            old.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
            await setup.SaveChangesAsync();
        }
        await using var replay = Context(database);
        var issuer = new CountingIssuer();
        var result = await WrappedAsync(replay, issuer, raw);
        result.Error.Should().Contain("vô hiệu hóa");
        issuer.Calls.Should().Be(0);
        await using var verify = database.CreateContext();
        (await verify.RefreshTokens.Where(t => t.UserId == userId).ToListAsync()).Should().OnlyContain(t => t.IsRevoked);
        (await verify.Users.SingleAsync(u => u.Id == userId)).AuthVersion.Should().Be(2);
        (await verify.Users.SingleAsync(u => u.Id == otherId)).AuthVersion.Should().Be(1);
        (await verify.RefreshTokens.SingleAsync(t => t.UserId == otherId)).IsActive.Should().BeTrue();
    }

    private static OralExamDbContext Context(TestDatabase db, bool retry = true, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseNpgsql(db.ConnectionString, p =>
        {
            if (retry) p.EnableRetryOnFailure(3, TimeSpan.Zero, null);
        }).AddInterceptors(interceptors).Options;
        return new OralExamDbContext(options);
    }

    private static RefreshTokenCommandHandler Handler(OralExamDbContext context, CountingIssuer issuer) =>
        new(context, issuer, new RefreshTokenCommandValidator());

    private static Task<Result<RefreshTokenResponse>> WrappedAsync(OralExamDbContext context, CountingIssuer issuer,
        string raw, bool clearTracker = true)
    {
        var attempt = 0;
        return context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0 && clearTracker) context.ChangeTracker.Clear();
            return await Handler(context, issuer).Handle(new RefreshTokenCommand(raw), CancellationToken.None);
        });
    }

    private static async Task<(Guid, string)> SeedAsync(TestDatabase db, string state = "valid")
    {
        await using var context = db.CreateContext();
        var user = new User { Email = $"refresh-{Guid.NewGuid():N}@example.test", FullName = "Refresh diagnostic",
            Role = "student", IsActive = state != "inactive", AuthVersion = 1 };
        var raw = RefreshTokenCommandHandler.GenerateRawToken();
        context.Users.Add(user);
        context.RefreshTokens.Add(new RefreshToken { User = user, TokenHash = RefreshTokenCommandHandler.HashToken(raw),
            ExpiresAt = state == "expired" ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(7) });
        await context.SaveChangesAsync();
        return (user.Id, raw);
    }

    private static async Task AssertRotationAsync(TestDatabase db, Guid userId, string oldRaw, string newRaw)
    {
        await using var verify = db.CreateContext();
        var tokens = await verify.RefreshTokens.Where(t => t.UserId == userId).ToListAsync();
        tokens.Should().HaveCount(2);
        var old = tokens.Single(t => t.TokenHash == RefreshTokenCommandHandler.HashToken(oldRaw));
        old.IsRevoked.Should().BeTrue();
        old.ReplacedByTokenHash.Should().Be(RefreshTokenCommandHandler.HashToken(newRaw));
        tokens.Single(t => t.TokenHash == old.ReplacedByTokenHash).IsActive.Should().BeTrue();
    }

    private sealed class CountingIssuer(Action? onIssue = null) : IAccessTokenIssuer
    {
        public int Calls { get; private set; }
        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false, int authVersion = 1)
        {
            onIssue?.Invoke();
            Calls++;
            return new IssuedAccessToken("diagnostic-access-token", 900);
        }
    }

    private sealed class SaveFault(bool transient) : SaveChangesInterceptor
    {
        public int Calls { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (++Calls == 1)
            {
                if (transient) throw new TimeoutException("diagnostic transient save failure");
                throw new InvalidOperationException("diagnostic save failure");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        public bool Injected { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (!Injected)
            {
                Injected = true;
                throw new TimeoutException("diagnostic acknowledgement lost after successful commit");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CommitFault : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("diagnostic commit failure");
    }
}
