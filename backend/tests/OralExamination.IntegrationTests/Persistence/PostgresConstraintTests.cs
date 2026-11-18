using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OralExamination.Domain.Entities;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class PostgresConstraintTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("check", "23514")]
    [InlineData("foreign-key", "23503")]
    [InlineData("unique", "23505")]
    public async Task InvalidCloWrite_DatabaseRejectsIt_AndOriginalRowsRemain(string scenario, string sqlState)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var seed = await TestData.AcademicAsync(context);
        context.CourseClos.Add(new CourseClo
        {
            CourseId = scenario == "foreign-key" ? Guid.NewGuid() : seed.Course.Id,
            CloCode = scenario == "unique" ? "CLO1" : "CLO2", Description = "Rejected outcome",
            BloomLevel = scenario == "check" ? "INVALID" : "Understand"
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        ((PostgresException)exception.InnerException!).SqlState.Should().Be(sqlState);
        await using var verify = database.CreateContext();
        (await verify.CourseClos.CountAsync(clo => clo.CourseId == seed.Course.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ReferencedClo_DeleteIsRestricted_NotSilentlyDetached()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var seed = await TestData.AcademicAsync(context);
        var question = new PracticeQuestion
        {
            CourseId = seed.Course.Id, RubricId = seed.Rubric.Id, CloId = seed.Clo.Id,
            Title = "CLO reference", Content = "Explain referential integrity."
        };
        context.PracticeQuestions.Add(question);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.CourseClos.Remove(new CourseClo { Id = seed.Clo.Id });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        ((PostgresException)exception.InnerException!).SqlState.Should().Be("23503");
        await using var verify = database.CreateContext();
        (await verify.PracticeQuestions.SingleAsync(item => item.Id == question.Id)).CloId.Should().Be(seed.Clo.Id);
    }

    [Fact]
    public async Task UserDeletion_CascadesRefreshTokens_WithoutTouchingOtherUsers()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var user = new User { Email = "cascade@example.test", FullName = "Cascade User" };
        var otherUser = new User { Email = "keep-me@example.test", FullName = "Preserved User" };
        var token = new RefreshToken { User = user, TokenHash = new string('a', 64), ExpiresAt = DateTime.UtcNow.AddDays(1) };
        context.Users.Add(otherUser);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.Users.Remove(new User { Id = user.Id });
        await context.SaveChangesAsync();

        await using var verify = database.CreateContext();
        (await verify.RefreshTokens.AnyAsync(item => item.Id == token.Id)).Should().BeFalse();
        (await verify.Users.AnyAsync(item => item.Id == otherUser.Id)).Should().BeTrue("other users are outside this deletion");
    }

    [Fact]
    public async Task NullableUsername_UniqueIndexAllowsNullsButRejectsDuplicateValues()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        context.Users.AddRange(
            new User { Email = "null1@example.test", FullName = "Null One" },
            new User { Email = "null2@example.test", FullName = "Null Two" },
            new User { Email = "named1@example.test", FullName = "Named One", Username = "it_unique" });
        await context.SaveChangesAsync();
        context.Users.Add(new User { Email = "named2@example.test", FullName = "Named Two", Username = "it_unique" });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        ((PostgresException)exception.InnerException!).SqlState.Should().Be("23505");
        await using var verify = database.CreateContext();
        (await verify.Users.CountAsync(user => user.Email == "null1@example.test" || user.Email == "null2@example.test"))
            .Should().Be(2);
    }
}
