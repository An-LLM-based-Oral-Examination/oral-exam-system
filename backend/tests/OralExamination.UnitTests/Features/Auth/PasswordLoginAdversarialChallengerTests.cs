using System;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Identity;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class PasswordLoginAdversarialChallengerTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private PasswordLoginCommandHandler CreateLoginHandler(OralExamDbContext context, RecordingIssuer issuer)
    {
        return new PasswordLoginCommandHandler(context, _hasher, issuer, new PasswordLoginCommandValidator());
    }

    [Theory(DisplayName = "Challenger 1.1: Đăng nhập Giảng viên lecture với nhiều biến thể username/email")]
    [InlineData("lecture", "Lecture-123")]
    [InlineData(" lecture ", "Lecture-123")]
    [InlineData("LECTURE", "Lecture-123")]
    [InlineData("lecture@fe.edu.vn", "Lecture-123")]
    [InlineData(" lecture@fe.edu.vn ", "Lecture-123")]
    [InlineData("LECTURE@FE.EDU.VN", "Lecture-123")]
    [InlineData("lecture@fpt.edu.vn", "Lecture-123")]
    [InlineData(" lecture@fpt.edu.vn ", "Lecture-123")]
    [InlineData("LECTURE@FPT.EDU.VN", "Lecture-123")]
    public async Task Challenger_Lecture_AllUsernameAndEmailVariants_Success(string usernameOrEmail, string password)
    {
        await using var context = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecture@fe.edu.vn",
            FullName = "Giang Vien Thu Nghiem",
            Username = "lecture",
            Role = UserRole.Lecturer,
            PasswordHash = _hasher.Hash("Lecture-123"),
            MustChangePassword = false,
            IsActive = true
        };
        context.Users.Add(lecturer);
        await context.SaveChangesAsync();

        var issuer = new RecordingIssuer();
        var handler = CreateLoginHandler(context, issuer);

        var result = await handler.Handle(new PasswordLoginCommand(usernameOrEmail, password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.User.Role.Should().Be(UserRole.Lecturer);
        result.Value.User.Id.Should().Be(lecturer.Id);
        result.Value.MustChangePassword.Should().BeFalse();
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Theory(DisplayName = "Challenger 1.2: Đăng nhập Sinh viên hoang se181212 với khoảng trắng trong username và password")]
    [InlineData("hoang se181212", "Hoang se181212")]
    [InlineData(" hoang   se181212 ", "Hoang se181212")]
    [InlineData("HOANG SE181212", "Hoang se181212")]
    [InlineData("hoangse181212", "Hoang se181212")]
    [InlineData("hoang se181212", "Hoangse181212")]
    [InlineData("hoanglvse181212@fpt.edu.vn", "Hoang se181212")]
    public async Task Challenger_Student_HoangSe181212_WhitespaceVariants_Success(string usernameOrEmail, string password)
    {
        await using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "hoanglvse181212@fpt.edu.vn",
            FullName = "Le Vu Hoang",
            StudentCode = "SE181212",
            Username = "hoangse181212",
            Role = UserRole.Student,
            PasswordHash = _hasher.Hash("Hoangse181212"),
            MustChangePassword = false,
            IsActive = true
        };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var issuer = new RecordingIssuer();
        var handler = CreateLoginHandler(context, issuer);

        var result = await handler.Handle(new PasswordLoginCommand(usernameOrEmail, password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.User.Role.Should().Be(UserRole.Student);
        result.Value.User.Id.Should().Be(student.Id);
        result.Value.User.StudentCode.Should().Be("SE181212");
    }

    [Theory(DisplayName = "Challenger 1.3: Đăng nhập với Alias totse18191414 và totse18191414@fpt.edu.vn")]
    [InlineData("totse18191414", "Totse181414")]
    [InlineData(" totse18191414 ", "Totse181414")]
    [InlineData("TOTSE18191414", "Totse181414")]
    [InlineData("totse18191414@fpt.edu.vn", "Totse181414")]
    [InlineData(" TOTSE18191414@FPT.EDU.VN ", "Totse181414")]
    [InlineData("totse181414", "Totse181414")]
    [InlineData("totntse181414@fpt.edu.vn", "Totse181414")]
    public async Task Challenger_Student_TotAlias_Variants_Success(string usernameOrEmail, string password)
    {
        await using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "totntse181414@fpt.edu.vn",
            FullName = "Nguyen Trong Tot",
            StudentCode = "SE181414",
            Username = "totse181414",
            Role = UserRole.Student,
            PasswordHash = _hasher.Hash("Totse181414"),
            MustChangePassword = false,
            IsActive = true
        };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var issuer = new RecordingIssuer();
        var handler = CreateLoginHandler(context, issuer);

        var result = await handler.Handle(new PasswordLoginCommand(usernameOrEmail, password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.User.Role.Should().Be(UserRole.Student);
        result.Value.User.Id.Should().Be(student.Id);
        result.Value.User.StudentCode.Should().Be("SE181414");
    }

    [Theory(DisplayName = "Challenger 1.4: Mật khẩu sai hoặc tài khoản không tồn tại không được ném 500, trả mã lỗi rõ ràng")]
    [InlineData("lecture", "Wrong-Password-123", AuthErrors.WrongPassword)]
    [InlineData("lecture@fe.edu.vn", "Wrong-Password-123", AuthErrors.WrongPassword)]
    [InlineData("nonexistent_user", "Lecture-123", AuthErrors.AccountNotFound)]
    [InlineData("unknown@fpt.edu.vn", "Lecture-123", AuthErrors.AccountNotFound)]
    [InlineData("random_student@fe.edu.vn", "Pass123", AuthErrors.AccountNotFound)]
    [InlineData("weird@@domain.com", "Pass123", AuthErrors.AccountNotFound)]
    public async Task Challenger_InvalidCredentials_ReturnsCleanFailure_NoException(string username, string password, string expectedError)
    {
        await using var context = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecture@fe.edu.vn",
            FullName = "Giang Vien Thu Nghiem",
            Username = "lecture",
            Role = UserRole.Lecturer,
            PasswordHash = _hasher.Hash("Lecture-123"),
            MustChangePassword = false,
            IsActive = true
        };
        context.Users.Add(lecturer);
        await context.SaveChangesAsync();

        var issuer = new RecordingIssuer();
        var handler = CreateLoginHandler(context, issuer);

        var result = await handler.Handle(new PasswordLoginCommand(username, password), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(expectedError);
    }

    [Theory(DisplayName = "Challenger 1.5: Input rỗng hoặc chỉ khoảng trắng kích hoạt validation failure sạch")]
    [InlineData("", "Password123")]
    [InlineData("   ", "Password123")]
    [InlineData("lecture", "")]
    [InlineData("lecture", "   ")]
    public async Task Challenger_EmptyOrWhitespaceInput_ReturnsValidationFailure(string username, string password)
    {
        await using var context = CreateDbContext();
        var issuer = new RecordingIssuer();
        var handler = CreateLoginHandler(context, issuer);

        var result = await handler.Handle(new PasswordLoginCommand(username, password), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Challenger 1.6: AuthController maps failures to RFC 7807 ProblemDetails correctly")]
    public async Task Challenger_AuthController_MapsErrorsToRfc7807ProblemDetails()
    {
        // 1. Wrong Password -> 401 ProblemDetails
        var mockSender401 = new Mock<ISender>();
        mockSender401
            .Setup(s => s.Send(It.IsAny<PasswordLoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PasswordLoginResponse>.Failure(AuthErrors.WrongPassword));

        var controller401 = new AuthController(mockSender401.Object);
        var actionResult401 = await controller401.PasswordLogin(new PasswordLoginRequest("lecture", "wrong"), CancellationToken.None);

        var objectResult401 = actionResult401 as ObjectResult;
        objectResult401.Should().NotBeNull();
        objectResult401!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem401 = objectResult401.Value as ProblemDetails;
        problem401.Should().NotBeNull();
        problem401!.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem401.Detail.Should().Be(AuthErrors.WrongPassword);
        problem401.Title.Should().Be("Không xác thực được.");
        problem401.Type.Should().Be("https://tools.ietf.org/html/rfc7231");

        // 2. Account Not Found -> 404 ProblemDetails
        var mockSender404 = new Mock<ISender>();
        mockSender404
            .Setup(s => s.Send(It.IsAny<PasswordLoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PasswordLoginResponse>.Failure(AuthErrors.AccountNotFound));

        var controller404 = new AuthController(mockSender404.Object);
        var actionResult404 = await controller404.PasswordLogin(new PasswordLoginRequest("ghost", "pass"), CancellationToken.None);

        var objectResult404 = actionResult404 as ObjectResult;
        objectResult404.Should().NotBeNull();
        objectResult404!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var problem404 = objectResult404.Value as ProblemDetails;
        problem404.Should().NotBeNull();
        problem404!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem404.Detail.Should().Be(AuthErrors.AccountNotFound);
        problem404.Title.Should().Be("Không tìm thấy tài khoản.");

        // 3. Account Locked -> 403 ProblemDetails
        var mockSender403 = new Mock<ISender>();
        mockSender403
            .Setup(s => s.Send(It.IsAny<PasswordLoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PasswordLoginResponse>.Failure(AuthErrors.AccountLocked));

        var controller403 = new AuthController(mockSender403.Object);
        var actionResult403 = await controller403.PasswordLogin(new PasswordLoginRequest("locked", "pass"), CancellationToken.None);

        var objectResult403 = actionResult403 as ObjectResult;
        objectResult403.Should().NotBeNull();
        objectResult403!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem403 = objectResult403.Value as ProblemDetails;
        problem403.Should().NotBeNull();
        problem403!.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem403.Detail.Should().Be(AuthErrors.AccountLocked);
        problem403.Title.Should().Be("Tài khoản bị khóa.");
    }

    private sealed class RecordingIssuer : IAccessTokenIssuer
    {
        public bool MustChangePassword { get; private set; }

        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false)
        {
            MustChangePassword = mustChangePassword;
            return new IssuedAccessToken("access-token-adversarial-test", 900);
        }
    }
}
