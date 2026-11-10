using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Auth;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Identity;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class PasswordLoginEmailAndAliasTests
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

    [Fact(DisplayName = "Đăng nhập bằng Email sinh viên thành công")]
    public async Task Handle_LoginWithStudentEmail_Success()
    {
        await using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "haipndse181313@fpt.edu.vn",
            FullName = "Pham Nguyen Dang Hai",
            StudentCode = "SE181313",
            Username = "haise181313",
            Role = UserRole.Student,
            PasswordHash = _hasher.Hash("Haise181313"),
            MustChangePassword = false,
            IsActive = true
        };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var issuer = new RecordingIssuer();
        var handler = CreateLoginHandler(context, issuer);

        var result = await handler.Handle(new PasswordLoginCommand("haipndse181313@fpt.edu.vn", "Haise181313"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Id.Should().Be(student.Id);
        result.Value.User.Role.Should().Be(UserRole.Student);
        result.Value.MustChangePassword.Should().BeFalse();
    }

    [Fact(DisplayName = "Đăng nhập bằng Giảng viên lecture qua username và cả 2 domain email fe.edu.vn và fpt.edu.vn")]
    public async Task Handle_LoginWithLecturerEmail_BothDomains_Success()
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

        // 1. Đăng nhập bằng Username: lecture
        var resultByUsername = await handler.Handle(new PasswordLoginCommand("lecture", "Lecture-123"), CancellationToken.None);
        resultByUsername.IsSuccess.Should().BeTrue();
        resultByUsername.Value.User.Role.Should().Be(UserRole.Lecturer);

        // 2. Đăng nhập bằng Email: lecture@fe.edu.vn
        var resultByFeEmail = await handler.Handle(new PasswordLoginCommand("lecture@fe.edu.vn", "Lecture-123"), CancellationToken.None);
        resultByFeEmail.IsSuccess.Should().BeTrue();
        resultByFeEmail.Value.User.Id.Should().Be(lecturer.Id);

        // 3. Đăng nhập bằng Email: lecture@fpt.edu.vn (domain mapping)
        var resultByFptEmail = await handler.Handle(new PasswordLoginCommand("lecture@fpt.edu.vn", "Lecture-123"), CancellationToken.None);
        resultByFptEmail.IsSuccess.Should().BeTrue();
        resultByFptEmail.Value.User.Id.Should().Be(lecturer.Id);
    }

    [Fact(DisplayName = "Đăng nhập với khoảng trắng trong Username tự động chuẩn hóa")]
    public async Task Handle_LoginWithSpaceInUsername_Success()
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

        var handler = CreateLoginHandler(context, new RecordingIssuer());

        var result = await handler.Handle(new PasswordLoginCommand(" hoang se181212 ", "Hoangse181212"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.User.Id.Should().Be(student.Id);
        result.Value.User.StudentCode.Should().Be("SE181212");
    }

    [Fact(DisplayName = "Đăng nhập với khoảng trắng trong Mật khẩu tự động fallback")]
    public async Task Handle_LoginWithSpaceInPassword_Success()
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

        var handler = CreateLoginHandler(context, new RecordingIssuer());

        var result = await handler.Handle(new PasswordLoginCommand("hoangse181212", "Hoang se181212"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.User.Id.Should().Be(student.Id);
    }

    [Fact(DisplayName = "Đăng nhập với Alias totse18191414 và totse18191414@fpt.edu.vn")]
    public async Task Handle_LoginWithTotse18191414Alias_Success()
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

        var handler = CreateLoginHandler(context, new RecordingIssuer());

        // Alias username
        var resultAlias = await handler.Handle(new PasswordLoginCommand("totse18191414", "Totse181414"), CancellationToken.None);
        resultAlias.IsSuccess.Should().BeTrue();
        resultAlias.Value.User.Id.Should().Be(student.Id);
        resultAlias.Value.User.StudentCode.Should().Be("SE181414");

        // Alias email
        var resultAliasEmail = await handler.Handle(new PasswordLoginCommand("totse18191414@fpt.edu.vn", "Totse181414"), CancellationToken.None);
        resultAliasEmail.IsSuccess.Should().BeTrue();
        resultAliasEmail.Value.User.Id.Should().Be(student.Id);
    }

    private sealed class RecordingIssuer : IAccessTokenIssuer
    {
        public bool MustChangePassword { get; private set; }

        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false)
        {
            MustChangePassword = mustChangePassword;
            return new IssuedAccessToken("access-token-test", 900);
        }
    }
}
