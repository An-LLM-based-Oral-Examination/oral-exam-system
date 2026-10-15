using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Auth;
using OralExamination.Application.Features.Auth.Commands.ChangePassword;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Identity;
using OralExamination.API.Middlewares;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class PasswordLoginCommandHandlerTests
{
    private const string SampleName = "Nguyen Trong Tot";
    private const string SampleCode = "SE180001";
    private const string SampleUsername = "totntse180001";

    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void MustChangePassword_AllowsOnlyChangePasswordSignOutHealthAndSwagger()
    {
        MustChangePasswordMiddleware.IsAllowed("/api/v1/auth/change-password").Should().BeTrue();
        MustChangePasswordMiddleware.IsAllowed("/api/v1/auth/sign-out").Should().BeTrue();
        MustChangePasswordMiddleware.IsAllowed("/health").Should().BeTrue();
        MustChangePasswordMiddleware.IsAllowed("/api/v1/practice/sessions").Should().BeFalse();
    }

    [Fact]
    public void Hash_DoesNotStorePlaintext_AndVerifies()
    {
        var hash = _hasher.Hash("ChangeMe-SE170001!");

        hash.Should().NotContain("ChangeMe-SE170001!");
        hash.Should().StartWith("pbkdf2-sha256.");
        _hasher.Verify("ChangeMe-SE170001!", hash).Should().BeTrue();
        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void SeedHashes_MatchKnownPasswords_AndAreNotPlaintext()
    {
        var hoangHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-SE170001!", "SE170001");
        var maiHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-SE170002!", "SE170002");

        hoangHash.Should().NotContain("ChangeMe-SE170001!");
        maiHash.Should().NotContain("ChangeMe-SE170002!");
        _hasher.Verify("ChangeMe-SE170001!", hoangHash).Should().BeTrue();
        _hasher.Verify("ChangeMe-SE170002!", maiHash).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidStudent_ReturnsTokensAndMustChangePassword()
    {
        await using var context = CreateDbContext();
        var user = await SeedStudent(context, "SE180001", "MatKhau-Tam-1!", mustChangePassword: true);
        var issuer = new RecordingIssuer();
        var handler = CreateLoginHandler(context, issuer);

        var result = await handler.Handle(new PasswordLoginCommand(" totntSE180001 ", "MatKhau-Tam-1!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MustChangePassword.Should().BeTrue();
        result.Value.User.Role.Should().Be(UserRole.Student);
        result.Value.User.Id.Should().Be(user.Id);
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        issuer.MustChangePassword.Should().BeTrue();
        (await context.RefreshTokens.CountAsync()).Should().Be(1);
        (await context.Users.SingleAsync()).PasswordHash.Should().NotContain("MatKhau-Tam-1!");
    }

    [Fact]
    public async Task Handle_UnknownUsername_ReturnsAccountNotFound()
    {
        await using var context = CreateDbContext();
        var result = await CreateLoginHandler(context, new RecordingIssuer())
            .Handle(new PasswordLoginCommand("totntse000000", "MatKhau-Tam-1!"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrors.AccountNotFound);
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsWrongPassword()
    {
        await using var context = CreateDbContext();
        await SeedStudent(context, "SE180001", "MatKhau-Tam-1!", mustChangePassword: true);

        var result = await CreateLoginHandler(context, new RecordingIssuer())
            .Handle(new PasswordLoginCommand(SampleUsername, "Sai-Mat-Khau-1!"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrors.WrongPassword);
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsLocked()
    {
        await using var context = CreateDbContext();
        await SeedStudent(context, "SE180001", "MatKhau-Tam-1!", mustChangePassword: true, isActive: false);

        var result = await CreateLoginHandler(context, new RecordingIssuer())
            .Handle(new PasswordLoginCommand(SampleUsername, "MatKhau-Tam-1!"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrors.AccountLocked);
    }

    [Fact]
    public async Task Handle_UserWithoutPassword_ReturnsPasswordNotSet()
    {
        await using var context = CreateDbContext();
        context.Users.Add(new User
        {
            Email = "totntse180009@fpt.edu.vn",
            FullName = SampleName,
            StudentCode = "SE180009",
            Username = "totntse180009",
            Role = UserRole.Student,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var result = await CreateLoginHandler(context, new RecordingIssuer())
            .Handle(new PasswordLoginCommand("totntse180009", "MatKhau-Tam-1!"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrors.PasswordNotSet);
    }

    [Fact]
    public async Task Handle_EmptyUsername_ReturnsValidationError()
    {
        await using var context = CreateDbContext();
        var result = await CreateLoginHandler(context, new RecordingIssuer())
            .Handle(new PasswordLoginCommand("  ", "MatKhau-Tam-1!"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Username");
    }

    [Fact]
    public async Task ChangePassword_ClearsFlag_AndNewPasswordLogsIn()
    {
        await using var context = CreateDbContext();
        var user = await SeedStudent(context, "SE180001", "MatKhau-Tam-1!", mustChangePassword: true);
        var change = new ChangePasswordCommandHandler(context, _hasher, new RecordingIssuer(), new ChangePasswordCommandValidator());

        var changed = await change.Handle(
            new ChangePasswordCommand(user.Id, "MatKhau-Tam-1!", "MatKhau-Moi-123!"),
            CancellationToken.None);

        changed.IsSuccess.Should().BeTrue();
        changed.Value.MustChangePassword.Should().BeFalse();
        (await context.Users.SingleAsync()).MustChangePassword.Should().BeFalse();

        var login = await CreateLoginHandler(context, new RecordingIssuer())
            .Handle(new PasswordLoginCommand(SampleUsername, "MatKhau-Moi-123!"), CancellationToken.None);
        login.IsSuccess.Should().BeTrue();
        login.Value.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Fails()
    {
        await using var context = CreateDbContext();
        var user = await SeedStudent(context, "SE180001", "MatKhau-Tam-1!", mustChangePassword: true);
        var change = new ChangePasswordCommandHandler(context, _hasher, new RecordingIssuer(), new ChangePasswordCommandValidator());

        var result = await change.Handle(
            new ChangePasswordCommand(user.Id, "Sai-Mat-Khau-1!", "MatKhau-Moi-123!"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrors.CurrentPasswordWrong);
    }

    [Fact]
    public async Task ChangePassword_TooShort_Fails()
    {
        await using var context = CreateDbContext();
        var user = await SeedStudent(context, "SE180001", "MatKhau-Tam-1!", mustChangePassword: true);
        var change = new ChangePasswordCommandHandler(context, _hasher, new RecordingIssuer(), new ChangePasswordCommandValidator());

        var result = await change.Handle(
            new ChangePasswordCommand(user.Id, "MatKhau-Tam-1!", "1234567"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ít nhất");
    }

    [Fact]
    public async Task Handle_StudentCodeOnly_ReturnsAccountNotFound()
    {
        await using var context = CreateDbContext();
        await SeedStudent(context, SampleCode, "MatKhau-Tam-1!", mustChangePassword: true);

        var result = await CreateLoginHandler(context, new RecordingIssuer())
            .Handle(new PasswordLoginCommand(SampleCode, "MatKhau-Tam-1!"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrors.AccountNotFound);
    }

    [Fact]
    public async Task Handle_WrongNamePrefix_ReturnsAccountNotFound()
    {
        await using var context = CreateDbContext();
        await SeedStudent(context, SampleCode, "MatKhau-Tam-1!", mustChangePassword: true);

        var result = await CreateLoginHandler(context, new RecordingIssuer())
            .Handle(new PasswordLoginCommand("hoalvse180001", "MatKhau-Tam-1!"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrors.AccountNotFound);
    }

    private async Task<User> SeedStudent(
        OralExamDbContext context,
        string studentCode,
        string password,
        bool mustChangePassword,
        bool isActive = true)
    {
        StudentUsername.TryCreate(SampleName, studentCode, out var username).Should().BeTrue();
        var user = new User
        {
            Email = $"{username}@fpt.edu.vn",
            FullName = SampleName,
            StudentCode = studentCode,
            Username = username,
            Role = UserRole.Student,
            PasswordHash = _hasher.Hash(password),
            MustChangePassword = mustChangePassword,
            IsActive = isActive
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private PasswordLoginCommandHandler CreateLoginHandler(OralExamDbContext context, RecordingIssuer issuer)
    {
        return new PasswordLoginCommandHandler(context, _hasher, issuer, new PasswordLoginCommandValidator());
    }

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private sealed class RecordingIssuer : IAccessTokenIssuer
    {
        public bool MustChangePassword { get; private set; }

        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false)
        {
            MustChangePassword = mustChangePassword;
            return new IssuedAccessToken("access-token", 900);
        }
    }
}
