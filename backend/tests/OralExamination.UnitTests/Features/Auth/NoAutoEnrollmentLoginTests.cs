using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Auth.Commands.GoogleLogin;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class NoAutoEnrollmentLoginTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "R4.2-01: GoogleLogin không tự động ghi danh sinh viên vào bất kỳ lớp học active nào")]
    public async Task GoogleLogin_ShouldNotAutoEnroll_StudentInActiveClasses()
    {
        await using var context = CreateDbContext();

        // Tạo 3 lớp học active có mật khẩu và join code riêng
        context.Classes.AddRange(
            new Class
            {
                Id = Guid.NewGuid(),
                Code = "SE1701",
                CourseId = Guid.NewGuid(),
                SemesterId = Guid.NewGuid(),
                IsActive = true,
                JoinCode = "SE1701_CODE",
                JoinPasswordHash = "HASH"
            },
            new Class
            {
                Id = Guid.NewGuid(),
                Code = "SE1702",
                CourseId = Guid.NewGuid(),
                SemesterId = Guid.NewGuid(),
                IsActive = true,
                JoinCode = "SE1702_CODE",
                JoinPasswordHash = "HASH"
            });
        await context.SaveChangesAsync();

        var google = new FakeGoogle
        {
            Identity = new GoogleUserIdentity("student.test@fpt.edu.vn", "Sinh Viên Test")
        };
        var issuer = new FakeIssuer();
        var handler = new GoogleLoginCommandHandler(context, google, issuer, new GoogleLoginCommandValidator());

        var result = await handler.Handle(new GoogleLoginCommand("valid-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Role.Should().Be(UserRole.Student);

        // Kiểm tra cam kết: ClassEnrollments BẮT BUỘC bằng 0 (không tự động ghi danh)
        var enrollmentsCount = await context.ClassEnrollments.CountAsync();
        enrollmentsCount.Should().Be(0, "Đăng nhập Google tuyệt đối không được tự động ghi danh sinh viên vào các lớp active");
    }

    [Fact(DisplayName = "R4.2-02: PasswordLogin không tự động ghi danh sinh viên vào bất kỳ lớp học active nào")]
    public async Task PasswordLogin_ShouldNotAutoEnroll_StudentInActiveClasses()
    {
        await using var context = CreateDbContext();
        var hasher = new Pbkdf2PasswordHasher();

        // Tạo 2 lớp học active
        context.Classes.AddRange(
            new Class
            {
                Id = Guid.NewGuid(),
                Code = "SWD392",
                CourseId = Guid.NewGuid(),
                SemesterId = Guid.NewGuid(),
                IsActive = true
            },
            new Class
            {
                Id = Guid.NewGuid(),
                Code = "PRN231",
                CourseId = Guid.NewGuid(),
                SemesterId = Guid.NewGuid(),
                IsActive = true
            });

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.pw@fpt.edu.vn",
            FullName = "Sinh Viên Password Login",
            StudentCode = "SE180123",
            Username = "totntse180123",
            Role = UserRole.Student,
            PasswordHash = hasher.Hash("StudentPass123!"),
            IsActive = true
        };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var issuer = new FakeIssuer();
        var handler = new PasswordLoginCommandHandler(
            context,
            hasher,
            issuer,
            new PasswordLoginCommandValidator());

        var result = await handler.Handle(
            new PasswordLoginCommand("totntse180123", "StudentPass123!"),
            CancellationToken.None);


        result.IsSuccess.Should().BeTrue();

        // Kiểm tra cam kết: ClassEnrollments BẮT BUỘC bằng 0 (không tự động ghi danh)
        var enrollmentsCount = await context.ClassEnrollments.CountAsync();
        enrollmentsCount.Should().Be(0, "Đăng nhập mật khẩu tuyệt đối không được tự động ghi danh sinh viên vào các lớp active");
    }

    private sealed class FakeGoogle : IGoogleIdTokenValidator
    {
        public GoogleUserIdentity? Identity { get; init; }

        public Task<GoogleUserIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(Identity!);
        }
    }

    private sealed class FakeIssuer : IAccessTokenIssuer
    {
        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false)
        {
            return new IssuedAccessToken("token-jwt", 900);
        }
    }
}
