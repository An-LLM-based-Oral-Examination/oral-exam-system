using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Admin.Commands.CreateUser;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Admin;

public class AdminCreateUserTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private readonly Pbkdf2PasswordHasher _passwordHasher = new();

    [Theory(DisplayName = "R4.4-01: Admin tạo tài khoản nhà trường thành công cho 5 vai trò cán bộ với MustChangePassword = true")]
    [InlineData(UserRole.Lecturer, "lecturer.new@fpt.edu.vn", "ThS. Nguyen Van Lecturer")]
    [InlineData(UserRole.ExaminationOfficer, "officer.new@fpt.edu.vn", "Cán Bộ Khảo Thí New")]
    [InlineData(UserRole.DepartmentHead, "head.new@fpt.edu.vn", "Trưởng Bộ Môn New")]
    [InlineData(UserRole.Proctor, "proctor.new@fpt.edu.vn", "Giám Thị New")]
    [InlineData(UserRole.Admin, "admin.new@fpt.edu.vn", "Quản Trị Viên New")]
    public async Task CreateUser_ForStaffRoles_ShouldSucceedWithMustChangePassword(string role, string email, string fullName)
    {
        await using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var handler = new CreateUserCommandHandler(context, _passwordHasher);

        var command = new CreateUserCommand(
            ActorAdminId: adminId,
            Email: email,
            FullName: fullName,
            Password: "InitialPassword123!",
            Role: role);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be(email.ToLowerInvariant());
        result.Value.FullName.Should().Be(fullName);
        result.Value.Role.Should().Be(role);
        result.Value.MustChangePassword.Should().BeTrue("Tài khoản nhà trường cấp bắt buộc phải đổi mật khẩu lần đầu (Dual Auth)");
        result.Value.IsActive.Should().BeTrue();

        // Kiểm tra trong CSDL
        var createdUser = await context.Users.FirstAsync(u => u.Id == result.Value.Id);
        createdUser.Email.Should().Be(email.ToLowerInvariant());
        createdUser.PasswordHash.Should().NotBeNullOrWhiteSpace();
        createdUser.PasswordHash.Should().StartWith("pbkdf2-sha256.");
        _passwordHasher.Verify("InitialPassword123!", createdUser.PasswordHash!).Should().BeTrue();

        // Kiểm tra AuditLog
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "USER_CREATED" && a.EntityId == createdUser.Id);
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(adminId);
    }

    [Fact(DisplayName = "R4.4-02: Admin tạo tài khoản sinh viên thành công, yêu cầu StudentCode và tự sinh Username")]
    public async Task CreateUser_ForStudent_ShouldRequireStudentCode_AndGenerateUsername()
    {
        await using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var handler = new CreateUserCommandHandler(context, _passwordHasher);

        var command = new CreateUserCommand(
            ActorAdminId: adminId,
            Email: "totnt.student@fpt.edu.vn",
            FullName: "Nguyễn Trọng Tốt",
            Password: "StudentPassword123!",
            Role: UserRole.Student,
            StudentCode: "SE181863");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentCode.Should().Be("SE181863");
        result.Value.Username.Should().Be("totntse181863");
        result.Value.MustChangePassword.Should().BeTrue();

        var createdUser = await context.Users.FirstAsync(u => u.Id == result.Value.Id);
        createdUser.StudentCode.Should().Be("SE181863");
        createdUser.Username.Should().Be("totntse181863");
    }

    [Fact(DisplayName = "R4.4-03: CreateUser chặn khi thiếu StudentCode đối với vai trò student")]
    public async Task CreateUser_ForStudentWithoutStudentCode_ShouldFail()
    {
        await using var context = CreateDbContext();
        var handler = new CreateUserCommandHandler(context, _passwordHasher);

        var command = new CreateUserCommand(
            ActorAdminId: Guid.NewGuid(),
            Email: "student.nocode@fpt.edu.vn",
            FullName: "Sinh Viên Không Mã",
            Password: "Password123!",
            Role: UserRole.Student,
            StudentCode: null);

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("VALIDATION:");

        var validator = new CreateUserCommandValidator();
        var validationResult = validator.Validate(command);
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(CreateUserCommand.StudentCode));
    }

    [Fact(DisplayName = "R4.4-04: CreateUser chặn email đã tồn tại trong hệ thống (Conflict)")]
    public async Task CreateUser_WithExistingEmail_ShouldReturnConflict()
    {
        await using var context = CreateDbContext();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "duplicate@fpt.edu.vn",
            FullName = "Người Dùng Đã Có",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var handler = new CreateUserCommandHandler(context, _passwordHasher);

        var command = new CreateUserCommand(
            ActorAdminId: Guid.NewGuid(),
            Email: "DUPLICATE@fpt.edu.vn", // Kiểm tra không phân biệt hoa thường
            FullName: "Người Dùng Trùng Email",
            Password: "Password123!",
            Role: UserRole.Proctor);

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "R4.4-05: CreateUser kiểm tra 6 vai trò hợp lệ và độ dài mật khẩu >= 8")]
    public void CreateUserCommandValidator_ShouldEnforce6Roles_AndPasswordLength()
    {
        var validator = new CreateUserCommandValidator();

        // Role không hợp lệ
        var invalidRoleCmd = new CreateUserCommand(
            Guid.NewGuid(), "test@fpt.edu.vn", "Test User", "Password123!", "superadmin");
        var res1 = validator.Validate(invalidRoleCmd);
        res1.IsValid.Should().BeFalse();
        res1.Errors.Should().Contain(e => e.PropertyName == nameof(CreateUserCommand.Role));

        // Mật khẩu ngắn < 8 ký tự
        var shortPassCmd = new CreateUserCommand(
            Guid.NewGuid(), "test@fpt.edu.vn", "Test User", "12345", UserRole.Lecturer);
        var res2 = validator.Validate(shortPassCmd);
        res2.IsValid.Should().BeFalse();
        res2.Errors.Should().Contain(e => e.PropertyName == nameof(CreateUserCommand.Password));
    }

    [Fact(DisplayName = "R4.4-06: AdminController.CreateUser chỉ cho phép role admin")]
    public void AdminController_CreateUser_ShouldRequireAdminRole()
    {
        var controllerType = typeof(global::API.Controllers.v1.AdminController);
        var createMethod = controllerType.GetMethod(nameof(global::API.Controllers.v1.AdminController.CreateUser));
        createMethod.Should().NotBeNull();

        // Controller có attribute [Authorize(Roles = "admin")]
        var controllerAuth = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        controllerAuth.Should().NotBeNull();
        controllerAuth!.Roles.Should().Be("admin", "AdminController phải được bảo vệ nghiêm ngặt chỉ dành cho role admin");
    }
}
