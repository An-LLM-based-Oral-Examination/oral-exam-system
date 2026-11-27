using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OralExamination.Application.Features.Admin.Commands.ImportUsers;
using OralExamination.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Identity;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class PostPr42HardeningPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task BulkUserImport_LongStudentUsername33To50Chars_PersistsCleanlyOnPostgres16()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin.bulk.pg@fpt.edu.vn",
            FullName = "Admin Bulk PG",
            Role = UserRole.Admin,
            IsActive = true
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        // Tên dài tạo username sinh viên > 32 ký tự (33..50 ký tự) -> từng gây lỗi PostgresException 22001 ở VARCHAR(32)
        const string longFullName = "Nguyễn Hoàng Minh Quang AlexandriaConstantinople";
        const string studentCode = "SE189876";
        StudentUsername.TryCreate(longFullName, studentCode, out var expectedUsername).Should().BeTrue();
        expectedUsername.Length.Should().BeGreaterThan(32).And.BeLessThanOrEqualTo(StudentUsername.MaxLength);

        var csv = $"Email,FullName,Role,StudentCode\nlongname.pg@fpt.edu.vn,\"{longFullName}\",student,{studentCode}\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var handler = new CommitAdminUsersImportCommandHandler(
            context,
            new Pbkdf2PasswordHasher(),
            new AdminUserSpreadsheetService());

        var result = await handler.Handle(
            new CommitAdminUsersImportCommand(admin.Id, stream, "long_students.csv", stream.Length, "TempPass#2026"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.CreatedCount.Should().Be(1);

        await using var verify = database.CreateContext();
        var saved = await verify.Users.AsNoTracking().SingleAsync(u => u.StudentCode == studentCode);
        saved.Username.Should().Be(expectedUsername);
        saved.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task ExamSetActiveSetCodePartialUniqueIndex_BlocksActiveDuplicate_AndAllowsReuseAfterSoftDelete()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var academic = await TestData.AcademicAsync(context);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = academic.Course.Id,
            CreatedBy = academic.Lecturer.Id,
            Name = "PG Unique SetCode Struct",
            TotalQuestions = 1,
            IsActive = true
        };
        context.ExamStructures.Add(structure);

        var set1 = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = academic.Course.Id,
            SetCode = "PG-UNIQ-01",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };
        context.ExamSets.Add(set1);
        await context.SaveChangesAsync();

        // Chèn bộ đề active trùng (course_id, set_code) -> phải nổ 23505 từ uq_exam_sets_active_course_set_code
        await using (var dupCtx = database.CreateContext())
        {
            dupCtx.ExamSets.Add(new ExamSet
            {
                Id = Guid.NewGuid(),
                StructureId = structure.Id,
                CourseId = academic.Course.Id,
                SetCode = "PG-UNIQ-01",
                Status = ExamSetStatus.Draft,
                IsActive = true
            });

            var act = async () => await dupCtx.SaveChangesAsync();
            var ex = await act.Should().ThrowAsync<DbUpdateException>();
            ex.Which.InnerException.Should().BeOfType<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        // Xóa mềm bộ đề 1 (is_active = false) -> cho phép tái sử dụng SetCode hợp lệ
        set1.IsActive = false;
        await context.SaveChangesAsync();

        var set2 = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = academic.Course.Id,
            SetCode = "PG-UNIQ-01",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };
        context.ExamSets.Add(set2);
        await context.SaveChangesAsync();

        (await context.ExamSets.AsNoTracking().CountAsync(es => es.CourseId == academic.Course.Id && es.SetCode == "PG-UNIQ-01"))
            .Should().Be(2);
    }

    [Fact]
    public async Task MarkAllNotificationsRead_UsesExecuteUpdateAsyncOnRealPostgres16()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var academic = await TestData.AcademicAsync(context);

        context.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), UserId = academic.Student.Id, Title = "N1", Message = "M1", Type = "INFO", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { Id = Guid.NewGuid(), UserId = academic.Student.Id, Title = "N2", Message = "M2", Type = "INFO", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { Id = Guid.NewGuid(), UserId = academic.Lecturer.Id, Title = "N3", Message = "M3", Type = "INFO", IsRead = false, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var result = await new MarkAllNotificationsReadCommandHandler(context)
            .Handle(new MarkAllNotificationsReadCommand(academic.Student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        await using var verify = database.CreateContext();
        (await verify.Notifications.CountAsync(n => n.UserId == academic.Student.Id && !n.IsRead)).Should().Be(0);
        (await verify.Notifications.CountAsync(n => n.UserId == academic.Student.Id && n.ReadAt != null)).Should().Be(2);
        (await verify.Notifications.CountAsync(n => n.UserId == academic.Lecturer.Id && !n.IsRead)).Should().Be(1);
    }

    [Fact]
    public async Task DevelopmentDataSeeder_OnFreshMigratedPostgres16_SeedsCompleteWithoutFk23503()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();

        // Lần 1: Chạy trên DB PostgreSQL 16 mới tinh -> KHÔNG được văng FK 23503 ở ExamSetPrn231Set02Id (001a)
        await DevelopmentDataSeeder.SeedAsync(context, CancellationToken.None);

        // Lần 2: Xác minh tính Idempotent 100%
        await DevelopmentDataSeeder.SeedAsync(context, CancellationToken.None);

        await using var verify = database.CreateContext();
        var set02 = await verify.ExamSets.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == DevelopmentDataSeeder.ExamSetPrn231Set02Id);
        set02.Should().NotBeNull();
        set02!.SetCode.Should().Be("PRN231-SET02");

        var ticketMai = await verify.StudentExamTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == DevelopmentDataSeeder.TicketMaiId);
        ticketMai.Should().NotBeNull();
        ticketMai!.ExamSetId.Should().Be(DevelopmentDataSeeder.ExamSetPrn231Set02Id);

        var readyTicketThanh = await verify.StudentExamTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == DevelopmentDataSeeder.TicketReadyToTestThanhId);
        readyTicketThanh.Should().NotBeNull();
        readyTicketThanh!.ExamSetId.Should().Be(DevelopmentDataSeeder.ExamSetPrn231Set02Id);

        var submissionsCount = await verify.ExamQuestionSubmissions.AsNoTracking()
            .CountAsync(s => s.TicketId == DevelopmentDataSeeder.TicketHoangId || s.TicketId == DevelopmentDataSeeder.TicketMaiId);
        submissionsCount.Should().BeGreaterThanOrEqualTo(2);
    }
}
