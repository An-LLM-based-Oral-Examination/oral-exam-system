using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.API.Middlewares;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth;
using OralExamination.Application.Features.Auth.Commands.ChangePassword;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public sealed class DesktopKioskAuthAndSubmissionTests
{
    private const string ValidDeviceId = "BYOD-DESKTOP-KIOSK-0001";
    private const string ValidKioskVersion = "desktop-kiosk-1.0.0";
    private const string KioskSessionToken = "kiosk-session-token-0001";
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void MustChangePasswordMiddleware_AllowsKioskChangePasswordAndRefreshOnly()
    {
        MustChangePasswordMiddleware.IsAllowed("/api/v1/auth/kiosk/change-password").Should().BeTrue();
        MustChangePasswordMiddleware.IsAllowed("/api/v1/auth/kiosk/refresh-token").Should().BeTrue();
        MustChangePasswordMiddleware.IsAllowed("/api/v1/official-exams/tickets/current").Should().BeFalse();
    }

    [Fact]
    public async Task KioskPasswordLogin_ValidStudent_ReturnsRefreshTokenInJsonAndSetsNoCookie()
    {
        await using var context = CreateDbContext();
        var student = await SeedUserAsync(context, "kiosksv01", UserRole.Student, "PassWord#123", mustChangePassword: false);
        var loginHandler = CreatePasswordLoginHandler(context);

        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<PasswordLoginCommand>(), It.IsAny<CancellationToken>()))
            .Returns<PasswordLoginCommand, CancellationToken>((cmd, ct) => loginHandler.Handle(cmd, ct));

        var controller = CreateAuthController(sender.Object, ValidDeviceId, ValidKioskVersion);

        var actionResult = await controller.KioskPasswordLogin(new PasswordLoginRequest("kiosksv01", "PassWord#123"), CancellationToken.None);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<PasswordLoginResponse>().Subject;
        payload.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.RefreshToken.Should().NotBeNullOrWhiteSpace();
        payload.User.Id.Should().Be(student.Id);
        payload.User.Role.Should().Be(UserRole.Student);
        controller.Response.Headers.SetCookie.Should().BeEmpty();
        (await context.RefreshTokens.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Lecturer)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.ExaminationOfficer)]
    [InlineData(UserRole.Proctor)]
    public async Task KioskPasswordLogin_NonStudentRole_Returns403WithoutPersistingRefreshToken(string nonStudentRole)
    {
        await using var context = CreateDbContext();
        await SeedUserAsync(context, "staffuser01", nonStudentRole, "PassWord#123", mustChangePassword: false);
        var loginHandler = CreatePasswordLoginHandler(context);

        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<PasswordLoginCommand>(), It.IsAny<CancellationToken>()))
            .Returns<PasswordLoginCommand, CancellationToken>((cmd, ct) => loginHandler.Handle(cmd, ct));

        var controller = CreateAuthController(sender.Object, ValidDeviceId, ValidKioskVersion);

        var actionResult = await controller.KioskPasswordLogin(new PasswordLoginRequest("staffuser01", "PassWord#123"), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["errorCode"].Should().Be("KIOSK_STUDENT_ONLY");
        controller.Response.Headers.SetCookie.Should().BeEmpty();
        (await context.RefreshTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task KioskPasswordLogin_NonStudentWithWrongPassword_Returns401PreventingRoleEnumeration()
    {
        await using var context = CreateDbContext();
        await SeedUserAsync(context, "adminuser01", UserRole.Admin, "RealPass#123", mustChangePassword: false);
        var loginHandler = CreatePasswordLoginHandler(context);

        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<PasswordLoginCommand>(), It.IsAny<CancellationToken>()))
            .Returns<PasswordLoginCommand, CancellationToken>((cmd, ct) => loginHandler.Handle(cmd, ct));

        var controller = CreateAuthController(sender.Object, ValidDeviceId, ValidKioskVersion);

        var actionResult = await controller.KioskPasswordLogin(new PasswordLoginRequest("adminuser01", "WrongPass#999"), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["errorCode"].Should().Be("INVALID_CREDENTIALS");
    }

    [Theory]
    [InlineData(null, ValidKioskVersion, "INVALID_DEVICE_ID")]
    [InlineData("short", ValidKioskVersion, "INVALID_DEVICE_ID")]
    [InlineData(ValidDeviceId, null, "INVALID_KIOSK_VERSION")]
    [InlineData(ValidDeviceId, "invalid version with spaces", "INVALID_KIOSK_VERSION")]
    public async Task KioskPasswordLogin_MissingOrInvalidKioskHeaders_Returns400WithoutCallingMediator(
        string? deviceId,
        string? kioskVersion,
        string expectedErrorCode)
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateAuthController(sender.Object, deviceId, kioskVersion);

        var actionResult = await controller.KioskPasswordLogin(new PasswordLoginRequest("sv01", "PassWord#123"), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["errorCode"].Should().Be(expectedErrorCode);
    }

    [Fact]
    public async Task KioskRefreshToken_DoesNotReadBrowserCookie_PreventsCookieToJsonDowngrade()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateAuthController(
            sender.Object,
            ValidDeviceId,
            ValidKioskVersion,
            cookieHeader: "oes_refresh_session=secret-browser-cookie-token");

        var actionResult = await controller.KioskRefreshToken(new RefreshTokenRequest(null), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["errorCode"].Should().Be("MISSING_REFRESH_TOKEN");
        controller.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task KioskRefreshToken_ValidStudentBodyToken_RotatesAndReturnsNewRefreshTokenWithoutCookie()
    {
        await using var context = CreateDbContext();
        var student = await SeedUserAsync(context, "kiosksv02", UserRole.Student, "PassWord#123", mustChangePassword: false);
        var initialRawToken = RefreshTokenCommandHandler.GenerateRawToken();
        context.RefreshTokens.Add(new OralExamination.Domain.Entities.RefreshToken
        {
            UserId = student.Id,
            TokenHash = RefreshTokenCommandHandler.HashToken(initialRawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var refreshHandler = new RefreshTokenCommandHandler(context, new StubAccessTokenIssuer(), new RefreshTokenCommandValidator());
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .Returns<RefreshTokenCommand, CancellationToken>((cmd, ct) => refreshHandler.Handle(cmd, ct));

        var controller = CreateAuthController(sender.Object, ValidDeviceId, ValidKioskVersion);

        var actionResult = await controller.KioskRefreshToken(new RefreshTokenRequest(initialRawToken), CancellationToken.None);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<RefreshTokenResponse>().Subject;
        payload.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.RefreshToken.Should().NotBeNullOrWhiteSpace();
        payload.RefreshToken.Should().NotBe(initialRawToken);
        controller.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task KioskRefreshToken_LecturerToken_Returns403AndDoesNotMutateOrRevokeToken()
    {
        await using var context = CreateDbContext();
        var lecturer = await SeedUserAsync(context, "lecturer01", UserRole.Lecturer, "PassWord#123", mustChangePassword: false);
        var initialRawToken = RefreshTokenCommandHandler.GenerateRawToken();
        var initialHash = RefreshTokenCommandHandler.HashToken(initialRawToken);
        context.RefreshTokens.Add(new OralExamination.Domain.Entities.RefreshToken
        {
            UserId = lecturer.Id,
            TokenHash = initialHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var refreshHandler = new RefreshTokenCommandHandler(context, new StubAccessTokenIssuer(), new RefreshTokenCommandValidator());
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .Returns<RefreshTokenCommand, CancellationToken>((cmd, ct) => refreshHandler.Handle(cmd, ct));

        var controller = CreateAuthController(sender.Object, ValidDeviceId, ValidKioskVersion);

        var actionResult = await controller.KioskRefreshToken(new RefreshTokenRequest(initialRawToken), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["errorCode"].Should().Be("KIOSK_STUDENT_ONLY");

        var storedTokens = await context.RefreshTokens.ToListAsync();
        storedTokens.Should().ContainSingle();
        storedTokens[0].TokenHash.Should().Be(initialHash);
        storedTokens[0].RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task KioskChangePassword_Student_ReturnsNewRefreshTokenWithoutCookie_AndRejectsNonStudent()
    {
        await using var context = CreateDbContext();
        var student = await SeedUserAsync(context, "kiosksv03", UserRole.Student, "OldPass#123", mustChangePassword: true);
        var lecturer = await SeedUserAsync(context, "lecturer02", UserRole.Lecturer, "OldPass#123", mustChangePassword: true);
        var originalLecturerHash = lecturer.PasswordHash;

        var changeHandler = new ChangePasswordCommandHandler(
            context,
            _hasher,
            new StubAccessTokenIssuer(),
            new ChangePasswordCommandValidator());
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ChangePasswordCommand>(), It.IsAny<CancellationToken>()))
            .Returns<ChangePasswordCommand, CancellationToken>((cmd, ct) => changeHandler.Handle(cmd, ct));

        var studentController = CreateAuthController(sender.Object, ValidDeviceId, ValidKioskVersion, authenticatedUserId: student.Id, role: UserRole.Student);
        var okResult = await studentController.KioskChangePassword(
            new ChangePasswordRequest("OldPass#123", "NewPass#456"),
            CancellationToken.None);

        var ok = okResult.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ChangePasswordResponse>().Subject;
        payload.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.RefreshToken.Should().NotBeNullOrWhiteSpace();
        payload.MustChangePassword.Should().BeFalse();
        studentController.Response.Headers.SetCookie.Should().BeEmpty();

        var lecturerResult = await changeHandler.Handle(
            new ChangePasswordCommand(lecturer.Id, "OldPass#123", "NewPass#456", UserRole.Student),
            CancellationToken.None);
        lecturerResult.IsSuccess.Should().BeFalse();
        lecturerResult.Error.Should().Be(AuthErrors.KioskStudentOnly);
        (await context.Users.SingleAsync(u => u.Id == lecturer.Id)).PasswordHash.Should().Be(originalLecturerHash);
    }

    [Fact]
    public async Task SubmitOfficialExam_FinalizePerTurnWithZeroSubmissions_CreatesEmptySubmissionsJobsAndLocksIdempotently()
    {
        await using var context = CreateDbContext();
        var (student, ticket, questions) = await SeedOfficialTicketAsync(context, questionCount: 3);
        var handler = new SubmitOfficialExamCommandHandler(context);

        var first = await handler.Handle(new SubmitOfficialExamCommand(
            student.Id,
            ticket.Id,
            "127.0.0.1",
            [],
            ValidDeviceId,
            KioskSessionToken,
            FinalizePerTurn: true), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        first.Value.Status.Should().Be(ExamTicketStatus.Submitted);
        first.Value.SubmissionCount.Should().Be(questions.Count);
        first.Value.IsKioskLocked.Should().BeTrue();

        var submissions = await context.ExamQuestionSubmissions.ToListAsync();
        submissions.Should().HaveCount(3);
        submissions.Should().OnlyContain(s => !s.IsFollowUp && s.FollowUpIndex == 0 && s.TranscriptWhisper == string.Empty && s.GradingStatus == "pending");
        (await context.GradingJobs.CountAsync()).Should().Be(3);

        var retry = await handler.Handle(new SubmitOfficialExamCommand(
            student.Id,
            ticket.Id,
            "127.0.0.1",
            [],
            ValidDeviceId,
            KioskSessionToken,
            FinalizePerTurn: true), CancellationToken.None);

        retry.IsSuccess.Should().BeTrue();
        retry.Value.Status.Should().Be(ExamTicketStatus.Submitted);
        (await context.ExamQuestionSubmissions.CountAsync()).Should().Be(3);
        (await context.GradingJobs.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task SubmitOfficialExam_LegacyEmptyWithoutFinalizePerTurnFlag_StillRejectsUnsealedAudio()
    {
        await using var context = CreateDbContext();
        var (student, ticket, _) = await SeedOfficialTicketAsync(context, questionCount: 2);

        var result = await new SubmitOfficialExamCommandHandler(context).Handle(new SubmitOfficialExamCommand(
            student.Id,
            ticket.Id,
            "127.0.0.1",
            [],
            ValidDeviceId,
            KioskSessionToken,
            FinalizePerTurn: false), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.AudioNotSealed);
        (await context.ExamQuestionSubmissions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitOfficialExam_FinalizePerTurnWithEmptyExamSet_ReturnsExamSetNoQuestions()
    {
        await using var context = CreateDbContext();
        var (student, ticket, _) = await SeedOfficialTicketAsync(context, questionCount: 0);

        var result = await new SubmitOfficialExamCommandHandler(context).Handle(new SubmitOfficialExamCommand(
            student.Id,
            ticket.Id,
            "127.0.0.1",
            [],
            ValidDeviceId,
            KioskSessionToken,
            FinalizePerTurn: true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.ExamSetNoQuestions);
    }

    [Fact]
    public async Task KioskRefreshToken_RevokedNonStudentTokenReplay_TriggersRevocationAndReturns401WithoutRoleLeak()
    {
        await using var context = CreateDbContext();
        var lecturer = await SeedUserAsync(context, "lecturer_replay", UserRole.Lecturer, "PassWord#123", mustChangePassword: false);
        var revokedRaw = RefreshTokenCommandHandler.GenerateRawToken();
        var activeRaw = RefreshTokenCommandHandler.GenerateRawToken();
        context.RefreshTokens.AddRange(
            new OralExamination.Domain.Entities.RefreshToken
            {
                UserId = lecturer.Id,
                TokenHash = RefreshTokenCommandHandler.HashToken(revokedRaw),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                RevokedAt = DateTime.UtcNow.AddMinutes(-5),
                ReplacedByTokenHash = RefreshTokenCommandHandler.HashToken(activeRaw)
            },
            new OralExamination.Domain.Entities.RefreshToken
            {
                UserId = lecturer.Id,
                TokenHash = RefreshTokenCommandHandler.HashToken(activeRaw),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
        await context.SaveChangesAsync();

        var refreshHandler = new RefreshTokenCommandHandler(context, new StubAccessTokenIssuer(), new RefreshTokenCommandValidator());
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .Returns<RefreshTokenCommand, CancellationToken>((cmd, ct) => refreshHandler.Handle(cmd, ct));

        var controller = CreateAuthController(sender.Object, ValidDeviceId, ValidKioskVersion);

        var actionResult = await controller.KioskRefreshToken(new RefreshTokenRequest(revokedRaw), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["errorCode"].Should().Be("INVALID_REFRESH_TOKEN");

        var updatedUser = await context.Users.SingleAsync(u => u.Id == lecturer.Id);
        updatedUser.AuthVersion.Should().Be(2);
        (await context.RefreshTokens.Where(t => t.UserId == lecturer.Id && t.RevokedAt == null).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task KioskRefreshToken_WithinGraceWindow_Returns409ConflictWithoutBumpingAuthVersion()
    {
        await using var context = CreateDbContext();
        var student = await SeedUserAsync(context, "kiosksv_grace", UserRole.Student, "PassWord#123", mustChangePassword: false);
        var justRotatedRaw = RefreshTokenCommandHandler.GenerateRawToken();
        var activeRaw = RefreshTokenCommandHandler.GenerateRawToken();
        context.RefreshTokens.AddRange(
            new OralExamination.Domain.Entities.RefreshToken
            {
                UserId = student.Id,
                TokenHash = RefreshTokenCommandHandler.HashToken(justRotatedRaw),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                RevokedAt = DateTime.UtcNow.AddSeconds(-2),
                ReplacedByTokenHash = RefreshTokenCommandHandler.HashToken(activeRaw)
            },
            new OralExamination.Domain.Entities.RefreshToken
            {
                UserId = student.Id,
                TokenHash = RefreshTokenCommandHandler.HashToken(activeRaw),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow.AddSeconds(-2)
            });
        await context.SaveChangesAsync();

        var refreshHandler = new RefreshTokenCommandHandler(context, new StubAccessTokenIssuer(), new RefreshTokenCommandValidator());
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .Returns<RefreshTokenCommand, CancellationToken>((cmd, ct) => refreshHandler.Handle(cmd, ct));

        var controller = CreateAuthController(sender.Object, ValidDeviceId, ValidKioskVersion);

        var actionResult = await controller.KioskRefreshToken(new RefreshTokenRequest(justRotatedRaw), CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["errorCode"].Should().Be("REFRESH_TOKEN_CONFLICT");

        var unchangedUser = await context.Users.SingleAsync(u => u.Id == student.Id);
        unchangedUser.AuthVersion.Should().Be(1);
    }

    [Theory]
    [InlineData(null, ValidKioskVersion, "INVALID_DEVICE_ID")]
    [InlineData(ValidDeviceId, null, "INVALID_KIOSK_VERSION")]
    public async Task KioskRefreshAndChangePassword_InvalidKioskHeaders_Return400WithoutCallingMediator(
        string? deviceId,
        string? kioskVersion,
        string expectedErrorCode)
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateAuthController(sender.Object, deviceId, kioskVersion, authenticatedUserId: Guid.NewGuid(), role: UserRole.Student);

        var refreshResult = await controller.KioskRefreshToken(new RefreshTokenRequest("some-token"), CancellationToken.None);
        var refreshObj = refreshResult.Should().BeOfType<ObjectResult>().Subject;
        refreshObj.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        refreshObj.Value.Should().BeOfType<ProblemDetails>().Subject.Extensions["errorCode"].Should().Be(expectedErrorCode);

        var changeResult = await controller.KioskChangePassword(new ChangePasswordRequest("OldPass#123", "NewPass#456"), CancellationToken.None);
        var changeObj = changeResult.Should().BeOfType<ObjectResult>().Subject;
        changeObj.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        changeObj.Value.Should().BeOfType<ProblemDetails>().Subject.Extensions["errorCode"].Should().Be(expectedErrorCode);
    }

    [Fact]
    public async Task SubmitOfficialExam_FinalizePerTurnWithNonEmptyAnswers_ReturnsBadRequestRejectingMixedMode()
    {
        await using var context = CreateDbContext();
        var (student, ticket, questions) = await SeedOfficialTicketAsync(context, questionCount: 2);

        var result = await new SubmitOfficialExamCommandHandler(context).Handle(new SubmitOfficialExamCommand(
            student.Id,
            ticket.Id,
            "127.0.0.1",
            [new OfficialExamAnswerSubmissionRequest(questions[0].Id, "Transcript", "[]", 30)],
            ValidDeviceId,
            KioskSessionToken,
            FinalizePerTurn: true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("BAD_REQUEST:");
        (await context.ExamQuestionSubmissions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OfficialExamSubmissionsController_SubmitWithNullBody_Returns400ProblemDetails()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = new OfficialExamSubmissionsController(sender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("sub", Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Role, UserRole.Student)
                    ], "test"))
                }
            }
        };

        var result = await controller.Submit(Guid.NewGuid(), null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private PasswordLoginCommandHandler CreatePasswordLoginHandler(OralExamDbContext context) =>
        new(
            context,
            _hasher,
            new StubAccessTokenIssuer(),
            new PasswordLoginCommandValidator(),
            new LoginAttemptGuard(new LoginProtectionOptions()));

    private static AuthController CreateAuthController(
        ISender sender,
        string? deviceId,
        string? kioskVersion,
        string? cookieHeader = null,
        Guid? authenticatedUserId = null,
        string? role = null)
    {
        var httpContext = new DefaultHttpContext();
        if (deviceId is not null)
        {
            httpContext.Request.Headers[OfficialExamDeviceGuard.DeviceIdHeaderName] = deviceId;
        }

        if (kioskVersion is not null)
        {
            httpContext.Request.Headers[OfficialExamDeviceGuard.KioskVersionHeaderName] = kioskVersion;
        }

        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            httpContext.Request.Headers.Cookie = cookieHeader;
        }

        if (authenticatedUserId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", authenticatedUserId.Value.ToString()),
                new Claim(ClaimTypes.Role, role ?? UserRole.Student)
            ], "test"));
        }

        return new AuthController(sender)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private async Task<User> SeedUserAsync(
        OralExamDbContext context,
        string username,
        string role,
        string rawPassword,
        bool mustChangePassword)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = $"{username}@fpt.edu.vn",
            FullName = $"Test {username}",
            StudentCode = role == UserRole.Student ? $"SE{Random.Shared.Next(170000, 199999)}" : null,
            Role = role,
            PasswordHash = _hasher.Hash(rawPassword),
            MustChangePassword = mustChangePassword,
            IsActive = true,
            AuthVersion = 1
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<(User Student, StudentExamTicket Ticket, List<ExamQuestion> Questions)> SeedOfficialTicketAsync(
        OralExamDbContext context,
        int questionCount)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Sinh viên Kiosk",
            StudentCode = "SE180999",
            Role = UserRole.Student,
            IsActive = true
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca thi Kiosk",
            RoomLab = "Lab 301",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(15),
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true
        };
        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            SetCode = $"KIOSK-{Guid.NewGuid():N}"[..14],
            IsActive = true
        };

        var questions = Enumerable.Range(1, questionCount).Select(i => new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = examSet.CourseId,
            RubricId = Guid.NewGuid(),
            Title = $"Câu hỏi {i}",
            Content = $"Nội dung câu hỏi {i}",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        }).ToList();

        context.Users.Add(student);
        context.RealExamSessionShifts.Add(shift);
        context.ExamSets.Add(examSet);
        context.ExamQuestions.AddRange(questions);
        context.ExamSetQuestions.AddRange(questions.Select((q, index) => new ExamSetQuestion
        {
            ExamSetId = examSet.Id,
            ExamQuestionId = q.Id,
            OrderIndex = index + 1
        }));

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress,
            DeviceId = ValidDeviceId,
            DeviceLockedAt = DateTime.UtcNow,
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(KioskSessionToken)
        };
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        return (student, ticket, questions);
    }

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private sealed class StubAccessTokenIssuer : IAccessTokenIssuer
    {
        public IssuedAccessToken Issue(
            Guid userId,
            string email,
            string fullName,
            string role,
            bool mustChangePassword = false,
            int authVersion = 1) =>
            new($"jwt-{userId:N}-{role}-{authVersion}", AuthTokenDefaults.AccessTokenMinutes * 60);
    }
}
