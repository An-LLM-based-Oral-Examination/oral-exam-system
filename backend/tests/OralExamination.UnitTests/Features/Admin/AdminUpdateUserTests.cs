using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Admin.Commands.UpdateUser;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Admin;

public class AdminUpdateUserTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "R3-01: UpdateUser cập nhật thành công sinh viên và giữ nguyên Username khi bỏ trống hoặc giữ nguyên giá trị cũ")]
    public async Task UpdateUser_ForStudent_ShouldUpdateProfileAndPreserveUsername()
    {
        await using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "old.student@fpt.edu.vn",
            FullName = "Nguyễn Văn Cũ",
            StudentCode = "SE180001",
            Username = "cunvse180001",
            Role = UserRole.Student,
            PasswordHash = "pbkdf2-sha256.hash",
            MustChangePassword = false,
            IsActive = true,
            AuthVersion = 2
        };
        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            TokenHash = "active-token-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        context.Users.Add(student);
        context.RefreshTokens.Add(activeToken);
        await context.SaveChangesAsync();

        var handler = new UpdateUserCommandHandler(context);
        var result = await handler.Handle(new UpdateUserCommand(
            ActorAdminId: adminId,
            TargetUserId: student.Id,
            Email: "  NEW.STUDENT@fpt.edu.vn ",
            FullName: "  Nguyễn Văn Mới  ",
            Username: "CUNVSE180001", // unchanged ignoring case
            StudentCode: " se180999 "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("new.student@fpt.edu.vn");
        result.Value.FullName.Should().Be("Nguyễn Văn Mới");
        result.Value.StudentCode.Should().Be("SE180999");
        result.Value.Username.Should().Be("cunvse180001");
        result.Value.Role.Should().Be(UserRole.Student);
        result.Value.IsActive.Should().BeTrue();
        result.Value.MustChangePassword.Should().BeFalse();

        var updated = await context.Users.Include(u => u.RefreshTokens).FirstAsync(u => u.Id == student.Id);
        updated.AuthVersion.Should().Be(3);
        updated.PasswordHash.Should().Be("pbkdf2-sha256.hash");
        updated.RefreshTokens.Single().RevokedAt.Should().NotBeNull();

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == student.Id && a.Action == "USER_UPDATED");
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(adminId);
        audit.OldValues.Should().Contain("old.student@fpt.edu.vn");
        audit.NewValues.Should().Contain("new.student@fpt.edu.vn");
    }

    [Fact(DisplayName = "R3-02: UpdateUser cho phép non-student cập nhật hoặc xóa Username về null")]
    public async Task UpdateUser_ForStaff_ShouldSetAndClearUsername()
    {
        await using var context = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "Giảng Viên A",
            Username = "lect.a",
            Role = UserRole.Lecturer,
            IsActive = true,
            AuthVersion = 1
        };
        context.Users.Add(lecturer);
        await context.SaveChangesAsync();

        var handler = new UpdateUserCommandHandler(context);

        var setRes = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), lecturer.Id, "lecturer@fpt.edu.vn", "Giảng Viên A+", " LECT.B ", null),
            CancellationToken.None);
        setRes.IsSuccess.Should().BeTrue();
        setRes.Value.Username.Should().Be("lect.b");
        (await context.Users.FindAsync(lecturer.Id))!.AuthVersion.Should().Be(2);

        var clearRes = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), lecturer.Id, "lecturer@fpt.edu.vn", "Giảng Viên A+", "   ", null),
            CancellationToken.None);
        clearRes.IsSuccess.Should().BeTrue();
        clearRes.Value.Username.Should().BeNull();
        (await context.Users.FindAsync(lecturer.Id))!.AuthVersion.Should().Be(3);
    }

    [Fact(DisplayName = "R3-03: UpdateUser chỉ sửa FullName hoặc StudentCode không bump AuthVersion và không thu hồi RefreshToken")]
    public async Task UpdateUser_ChangingOnlyFullNameOrStudentCode_ShouldNotBumpAuthVersionOrRevokeTokens()
    {
        await using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "keep.email@fpt.edu.vn",
            FullName = "Old Name",
            StudentCode = "SE180100",
            Username = "namoldse180100",
            Role = UserRole.Student,
            IsActive = true,
            AuthVersion = 4
        };
        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = student.Id,
            TokenHash = "still-valid-token",
            ExpiresAt = DateTime.UtcNow.AddDays(5)
        };
        context.Users.Add(student);
        context.RefreshTokens.Add(activeToken);
        await context.SaveChangesAsync();

        var handler = new UpdateUserCommandHandler(context);
        var result = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(),
            student.Id,
            "KEEP.EMAIL@fpt.edu.vn", // same email case-insensitively
            "New Full Name",
            null, // omit username -> preserve
            "SE180200"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Users.Include(u => u.RefreshTokens).FirstAsync(u => u.Id == student.Id);
        updated.AuthVersion.Should().Be(4);
        updated.RefreshTokens.Single().RevokedAt.Should().BeNull();
        updated.Username.Should().Be("namoldse180100");
        updated.StudentCode.Should().Be("SE180200");
    }

    [Fact(DisplayName = "R3-04: UpdateUser không làm mất RevokedAt cũ khi thu hồi RefreshToken đang hoạt động")]
    public async Task UpdateUser_RevokingActiveTokens_ShouldLeaveAlreadyRevokedTokensUntouched()
    {
        await using var context = CreateDbContext();
        var alreadyRevokedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var staff = new User
        {
            Id = Guid.NewGuid(),
            Email = "staff.tokens@fpt.edu.vn",
            FullName = "Staff Tokens",
            Role = UserRole.Proctor,
            IsActive = true,
            AuthVersion = 1
        };
        var oldRevoked = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = staff.Id,
            TokenHash = "old-revoked",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            RevokedAt = alreadyRevokedAt
        };
        var active = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = staff.Id,
            TokenHash = "active",
            ExpiresAt = DateTime.UtcNow.AddDays(5)
        };
        context.Users.Add(staff);
        context.RefreshTokens.AddRange(oldRevoked, active);
        await context.SaveChangesAsync();

        var handler = new UpdateUserCommandHandler(context);
        var result = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), staff.Id, "staff.tokens.new@fpt.edu.vn", "Staff Tokens"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.RefreshTokens.FindAsync(oldRevoked.Id))!.RevokedAt.Should().Be(alreadyRevokedAt);
        (await context.RefreshTokens.FindAsync(active.Id))!.RevokedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "R3-05: UpdateUser trả về NOT_FOUND khi người dùng không tồn tại")]
    public async Task UpdateUser_WhenMissing_ShouldReturnNotFound()
    {
        await using var context = CreateDbContext();
        var handler = new UpdateUserCommandHandler(context);

        var result = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), Guid.NewGuid(), "nobody@fpt.edu.vn", "Nobody"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "R3-06: UpdateUser kiểm tra quy tắc VALIDATION theo vai trò và biên độ dài")]
    public async Task UpdateUser_ShouldEnforceRoleAndFieldValidation()
    {
        await using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv@fpt.edu.vn",
            FullName = "Sinh Viên",
            StudentCode = "SE180111",
            Username = "svse180111",
            Role = UserRole.Student,
            IsActive = true
        };
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "gv@fpt.edu.vn",
            FullName = "Giảng Viên",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.AddRange(student, lecturer);
        await context.SaveChangesAsync();

        var handler = new UpdateUserCommandHandler(context);

        // Non-student with StudentCode
        var staffWithCode = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), lecturer.Id, "gv@fpt.edu.vn", "Giảng Viên", null, "SE180999"), CancellationToken.None);
        staffWithCode.IsSuccess.Should().BeFalse();
        staffWithCode.Error.Should().StartWith("VALIDATION:");

        // Student without StudentCode
        var studentMissingCode = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), student.Id, "sv@fpt.edu.vn", "Sinh Viên", null, "   "), CancellationToken.None);
        studentMissingCode.IsSuccess.Should().BeFalse();
        studentMissingCode.Error.Should().StartWith("VALIDATION:");

        // Student changing Username manually
        var studentEditUsername = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), student.Id, "sv@fpt.edu.vn", "Sinh Viên", "hacked.username", "SE180111"), CancellationToken.None);
        studentEditUsername.IsSuccess.Should().BeFalse();
        studentEditUsername.Error.Should().StartWith("VALIDATION:");

        // Invalid username for staff
        var badUsername = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), lecturer.Id, "gv@fpt.edu.vn", "Giảng Viên", "bad user!", null), CancellationToken.None);
        badUsername.IsSuccess.Should().BeFalse();
        badUsername.Error.Should().StartWith("VALIDATION:");

        // Validator bounds
        var validator = new UpdateUserCommandValidator();
        validator.Validate(new UpdateUserCommand(Guid.NewGuid(), Guid.Empty, "gv@fpt.edu.vn", "Valid")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserCommand(Guid.NewGuid(), lecturer.Id, "bad-email", "Valid")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserCommand(Guid.NewGuid(), lecturer.Id, new string('a', 245) + "@fpt.edu.vn", "Valid")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserCommand(Guid.NewGuid(), lecturer.Id, "gv@fpt.edu.vn", "A")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserCommand(Guid.NewGuid(), lecturer.Id, "gv@fpt.edu.vn", new string('A', 151))).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserCommand(Guid.NewGuid(), student.Id, "sv@fpt.edu.vn", "Valid", null, new string('S', 21))).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserCommand(Guid.NewGuid(), lecturer.Id, "gv@fpt.edu.vn", "Valid", "ab")).IsValid.Should().BeFalse();
        validator.Validate(new UpdateUserCommand(Guid.NewGuid(), lecturer.Id, "gv@fpt.edu.vn", "Valid", new string('u', 33))).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "R3-07: UpdateUser trả về CONFLICT khi trùng Email/StudentCode/Username với user khác và cho phép giữ nguyên giá trị của chính mình")]
    public async Task UpdateUser_ShouldEnforceCaseInsensitiveUniqueness_ExcludingSelf()
    {
        await using var context = CreateDbContext();
        var userA = new User
        {
            Id = Guid.NewGuid(),
            Email = "usera@fpt.edu.vn",
            FullName = "User A",
            StudentCode = "SE180001",
            Username = "user.a",
            Role = UserRole.Student,
            IsActive = true
        };
        var userB = new User
        {
            Id = Guid.NewGuid(),
            Email = "userb@fpt.edu.vn",
            FullName = "User B",
            Username = "user.b",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var userC = new User
        {
            Id = Guid.NewGuid(),
            Email = "userc@fpt.edu.vn",
            FullName = "User C",
            StudentCode = "SE180003",
            Username = "user.c",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.AddRange(userA, userB, userC);
        await context.SaveChangesAsync();

        var handler = new UpdateUserCommandHandler(context);

        var dupEmail = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), userB.Id, "USERA@fpt.edu.vn", "User B"), CancellationToken.None);
        dupEmail.IsSuccess.Should().BeFalse();
        dupEmail.Error.Should().StartWith("CONFLICT:");

        var dupUsername = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), userB.Id, "userb@fpt.edu.vn", "User B", "USER.A"), CancellationToken.None);
        dupUsername.IsSuccess.Should().BeFalse();
        dupUsername.Error.Should().StartWith("CONFLICT:");

        var dupStudentCode = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), userC.Id, "userc@fpt.edu.vn", "User C", null, "se180001"), CancellationToken.None);
        dupStudentCode.IsSuccess.Should().BeFalse();
        dupStudentCode.Error.Should().StartWith("CONFLICT:");

        // Updating self without changing unique fields must succeed
        var selfOk = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), userB.Id, "USERB@fpt.edu.vn", "User B Updated", "USER.B"), CancellationToken.None);
        selfOk.IsSuccess.Should().BeTrue();
        selfOk.Value.FullName.Should().Be("User B Updated");
    }

    [Fact(DisplayName = "R3-08: UpdateUser bắt DbUpdateException PostgreSQL 23505 và trả về CONFLICT")]
    public async Task UpdateUser_WhenSaveThrows23505_ShouldReturnConflict()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new Conflict23505DbContext(options);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "race.update@fpt.edu.vn",
            FullName = "Race Update",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.Throw23505OnNextSave = true;
        var handler = new UpdateUserCommandHandler(context);
        var result = await handler.Handle(new UpdateUserCommand(
            Guid.NewGuid(), user.Id, "race.update.new@fpt.edu.vn", "Race Update", "race.u"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Theory(DisplayName = "R3-09: UpdateUser từ chối email có khoảng trắng nội bộ hoặc cú pháp Display Name <email>")]
    [InlineData("a b@fpt.edu.vn")]
    [InlineData("Display Name <other@fpt.edu.vn>")]
    [InlineData("DisplayName<other@fpt.edu.vn>")]
    [InlineData("<other@fpt.edu.vn>")]
    public async Task UpdateUser_WithNonCanonicalEmail_ShouldFailValidationInValidatorAndHandler(string invalidEmail)
    {
        await using var context = CreateDbContext();
        var target = new User
        {
            Id = Guid.NewGuid(),
            Email = "target@fpt.edu.vn",
            FullName = "Target User",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var other = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@fpt.edu.vn",
            FullName = "Other User",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.AddRange(target, other);
        await context.SaveChangesAsync();

        var command = new UpdateUserCommand(
            Guid.NewGuid(),
            target.Id,
            invalidEmail,
            "Target User");

        var validation = new UpdateUserCommandValidator().Validate(command);
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateUserCommand.Email));

        var handler = new UpdateUserCommandHandler(context);
        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("VALIDATION:");
    }

    private sealed class Conflict23505DbContext(DbContextOptions<OralExamDbContext> options) : OralExamDbContext(options)
    {
        public bool Throw23505OnNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (Throw23505OnNextSave)
            {
                Throw23505OnNextSave = false;
                throw new DbUpdateException(
                    "duplicate key value violates unique constraint",
                    new FakeSqlStateException("23505"));
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FakeSqlStateException(string sqlState) : Exception($"Simulated Postgres {sqlState}")
    {
        public string SqlState { get; } = sqlState;
    }

    [Fact(DisplayName = "M1-F2-01: UpdateUserRole student -> lecturer xóa StudentCode = null và tăng AuthVersion")]
    public async Task UpdateUserRole_StudentToLecturer_ClearsStudentCodeAndBumpsAuthVersion()
    {
        await using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv.promote@fpt.edu.vn",
            FullName = "Nguyễn Văn A",
            StudentCode = "SE184236",
            Username = "ANVSE184236",
            Role = UserRole.Student,
            IsActive = true,
            AuthVersion = 1
        };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var handler = new OralExamination.Application.Features.Admin.Commands.UpdateUserRole.UpdateUserRoleCommandHandler(context);
        var result = await handler.Handle(
            new OralExamination.Application.Features.Admin.Commands.UpdateUserRole.UpdateUserRoleCommand(
                adminId, student.Id, UserRole.Lecturer),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(UserRole.Lecturer);
        result.Value.StudentCode.Should().BeNull();

        var saved = await context.Users.FindAsync(student.Id);
        saved!.StudentCode.Should().BeNull();
        saved.AuthVersion.Should().Be(2);
    }

    [Fact(DisplayName = "M1-F2-02: UpdateUserRole lecturer -> student yêu cầu StudentCode, sinh StudentUsername và chặn trùng CONFLICT")]
    public async Task UpdateUserRole_LecturerToStudent_ValidatesStudentCode_GeneratesUsername_AndGuardsConflict()
    {
        await using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lec.demote@fpt.edu.vn",
            FullName = "Nguyễn Quang Thành",
            Role = UserRole.Lecturer,
            IsActive = true,
            AuthVersion = 1
        };
        var existingStudent = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing.sv@fpt.edu.vn",
            FullName = "Trần Văn B",
            StudentCode = "SE180999",
            Username = "BTVSE180999",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.AddRange(lecturer, existingStudent);
        await context.SaveChangesAsync();

        var handler = new OralExamination.Application.Features.Admin.Commands.UpdateUserRole.UpdateUserRoleCommandHandler(context);

        var missingCode = await handler.Handle(
            new OralExamination.Application.Features.Admin.Commands.UpdateUserRole.UpdateUserRoleCommand(
                adminId, lecturer.Id, UserRole.Student),
            CancellationToken.None);
        missingCode.IsSuccess.Should().BeFalse();
        missingCode.Error.Should().StartWith("VALIDATION:");

        var dupCode = await handler.Handle(
            new OralExamination.Application.Features.Admin.Commands.UpdateUserRole.UpdateUserRoleCommand(
                adminId, lecturer.Id, UserRole.Student, "se180999"),
            CancellationToken.None);
        dupCode.IsSuccess.Should().BeFalse();
        dupCode.Error.Should().StartWith("CONFLICT:");

        var ok = await handler.Handle(
            new OralExamination.Application.Features.Admin.Commands.UpdateUserRole.UpdateUserRoleCommand(
                adminId, lecturer.Id, UserRole.Student, "se184236"),
            CancellationToken.None);
        ok.IsSuccess.Should().BeTrue();
        ok.Value.Role.Should().Be(UserRole.Student);
        ok.Value.StudentCode.Should().Be("SE184236");
        ok.Value.Username.Should().Be("ThanhNQSE184236");
    }

    [Fact(DisplayName = "M1-F2-03: GetUsersQuery và GetAuditLogsQuery trả TotalPages = 1 khi TotalCount = 0")]
    public async Task GetUsersAndAuditLogs_WhenEmpty_ReturnTotalPagesOne()
    {
        await using var context = CreateDbContext();

        var usersRes = await new OralExamination.Application.Features.Admin.Queries.GetUsers.GetUsersQueryHandler(context)
            .Handle(new OralExamination.Application.Features.Admin.Queries.GetUsers.GetUsersQuery(), CancellationToken.None);
        usersRes.IsSuccess.Should().BeTrue();
        usersRes.Value.TotalCount.Should().Be(0);
        usersRes.Value.TotalPages.Should().Be(1);

        var logsRes = await new OralExamination.Application.Features.Admin.Queries.GetAuditLogs.GetAuditLogsQueryHandler(context)
            .Handle(new OralExamination.Application.Features.Admin.Queries.GetAuditLogs.GetAuditLogsQuery(), CancellationToken.None);
        logsRes.IsSuccess.Should().BeTrue();
        logsRes.Value.TotalCount.Should().Be(0);
        logsRes.Value.TotalPages.Should().Be(1);
    }

    [Fact(DisplayName = "M1-F2-04: GetAuditLogsQuery sắp xếp tất định theo Id giảm dần khi CreatedAt trùng nhau")]
    public async Task GetAuditLogsQuery_WhenCreatedAtTies_OrdersDeterministicallyByIdDescending()
    {
        await using var context = CreateDbContext();
        var tiedTimestamp = new DateTime(2026, 11, 25, 6, 0, 0, DateTimeKind.Utc);
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var highId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        context.AuditLogs.AddRange(
            new AuditLog
            {
                Id = lowId,
                Action = "TIE_LOW",
                EntityName = "users",
                CreatedAt = tiedTimestamp
            },
            new AuditLog
            {
                Id = highId,
                Action = "TIE_HIGH",
                EntityName = "users",
                CreatedAt = tiedTimestamp
            });
        await context.SaveChangesAsync();

        var handler = new OralExamination.Application.Features.Admin.Queries.GetAuditLogs.GetAuditLogsQueryHandler(context);

        var allRes = await handler.Handle(
            new OralExamination.Application.Features.Admin.Queries.GetAuditLogs.GetAuditLogsQuery(EntityName: "users", Page: 1, PageSize: 10),
            CancellationToken.None);
        allRes.IsSuccess.Should().BeTrue();
        allRes.Value.Items.Select(x => x.Id).Should().ContainInOrder(highId, lowId);

        var page1Res = await handler.Handle(
            new OralExamination.Application.Features.Admin.Queries.GetAuditLogs.GetAuditLogsQuery(EntityName: "users", Page: 1, PageSize: 1),
            CancellationToken.None);
        var page2Res = await handler.Handle(
            new OralExamination.Application.Features.Admin.Queries.GetAuditLogs.GetAuditLogsQuery(EntityName: "users", Page: 2, PageSize: 1),
            CancellationToken.None);

        page1Res.IsSuccess.Should().BeTrue();
        page2Res.IsSuccess.Should().BeTrue();
        page1Res.Value.Items.Should().ContainSingle(x => x.Id == highId);
        page2Res.Value.Items.Should().ContainSingle(x => x.Id == lowId);
    }
}
