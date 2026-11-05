using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OralExamination.Domain.Common;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace OralExamination.UnitTests.Domain;

public class InClassExamAndNotificationEntitiesTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new OneWayLockInterceptor())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "M1-01. InClassExamShift implements ILockableEntity and has valid 3NF schema mapping")]
    public void InClassExamShift_Implements_ILockableEntity_And_Has_Valid_Schema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var entityType = model.FindEntityType(typeof(InClassExamShift));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("in_class_exam_shifts");

        typeof(ILockableEntity).IsAssignableFrom(typeof(InClassExamShift)).Should().BeTrue();

        var props = entityType!.GetProperties().Select(p => p.Name).ToList();
        props.Should().Contain(nameof(InClassExamShift.Id));
        props.Should().Contain(nameof(InClassExamShift.ClassId));
        props.Should().Contain(nameof(InClassExamShift.LecturerId));
        props.Should().Contain(nameof(InClassExamShift.ShiftName));
        props.Should().Contain(nameof(InClassExamShift.StartTime));
        props.Should().Contain(nameof(InClassExamShift.EndTime));
        props.Should().Contain(nameof(InClassExamShift.TotpSecretKey));
        props.Should().Contain(nameof(InClassExamShift.Status));
        props.Should().Contain(nameof(InClassExamShift.IsLocked));
        props.Should().Contain(nameof(InClassExamShift.FinalizedAt));
        props.Should().Contain(nameof(InClassExamShift.CreatedAt));
        props.Should().Contain(nameof(InClassExamShift.UpdatedAt));

        // Check constraint ck_in_class_exam_shifts_status
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var designEntityType = designModel.FindEntityType(typeof(InClassExamShift))!;
        var statusConstraint = designEntityType.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "ck_in_class_exam_shifts_status");
        statusConstraint.Should().NotBeNull();
        statusConstraint!.Sql.Should().Be("status IN ('SCHEDULED', 'IN_PROGRESS', 'FINALIZED', 'CANCELLED')");
    }

    [Fact(DisplayName = "M1-02. InClassShiftPassword maps to in_class_shift_passwords with ESR index")]
    public void InClassShiftPassword_Maps_Correctly_With_ESR_Index()
    {
        using var context = CreateDbContext();
        var entityType = context.Model.FindEntityType(typeof(InClassShiftPassword));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("in_class_shift_passwords");

        var props = entityType!.GetProperties().Select(p => p.Name).ToList();
        props.Should().Contain(nameof(InClassShiftPassword.Id));
        props.Should().Contain(nameof(InClassShiftPassword.ShiftId));
        props.Should().Contain(nameof(InClassShiftPassword.PlainCode));
        props.Should().Contain(nameof(InClassShiftPassword.PasswordHash));
        props.Should().Contain(nameof(InClassShiftPassword.ValidFrom));
        props.Should().Contain(nameof(InClassShiftPassword.ValidTo));
        props.Should().Contain(nameof(InClassShiftPassword.CreatedAt));

        var esrIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 3
                                 && i.Properties.Any(p => p.Name == nameof(InClassShiftPassword.ShiftId))
                                 && i.Properties.Any(p => p.Name == nameof(InClassShiftPassword.ValidFrom))
                                 && i.Properties.Any(p => p.Name == nameof(InClassShiftPassword.ValidTo)));
        esrIndex.Should().NotBeNull("ESR Index trên (shift_id, valid_from, valid_to) bắt buộc tồn tại");
    }

    [Fact(DisplayName = "M1-03. InClassShiftParticipant maps to in_class_shift_participants with unique constraint")]
    public void InClassShiftParticipant_Maps_Correctly_With_Unique_Constraint()
    {
        using var context = CreateDbContext();
        var entityType = context.Model.FindEntityType(typeof(InClassShiftParticipant));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("in_class_shift_participants");

        var props = entityType!.GetProperties().Select(p => p.Name).ToList();
        props.Should().Contain(nameof(InClassShiftParticipant.Id));
        props.Should().Contain(nameof(InClassShiftParticipant.ShiftId));
        props.Should().Contain(nameof(InClassShiftParticipant.StudentId));
        props.Should().Contain(nameof(InClassShiftParticipant.JoinedAt));
        props.Should().Contain(nameof(InClassShiftParticipant.Status));

        // Unique constraint (shift_id, student_id)
        var uniqueIndex = entityType!.GetIndexes()
            .FirstOrDefault(i => i.IsUnique && i.Properties.Count == 2
                                 && i.Properties.Any(p => p.Name == nameof(InClassShiftParticipant.ShiftId))
                                 && i.Properties.Any(p => p.Name == nameof(InClassShiftParticipant.StudentId)));
        uniqueIndex.Should().NotBeNull("Mỗi sinh viên chỉ được ghi nhận tham gia 1 lần vào mỗi ca thi");

        // Check constraint status
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var designEntityType = designModel.FindEntityType(typeof(InClassShiftParticipant))!;
        var statusConstraint = designEntityType.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "ck_in_class_shift_participants_status");
        statusConstraint.Should().NotBeNull();
        statusConstraint!.Sql.Should().Be("status IN ('JOINED', 'SUBMITTED', 'ABSENT')");
    }

    [Fact(DisplayName = "M1-04. InClassExamSubmission maps to in_class_exam_submissions without audio_r2_url")]
    public void InClassExamSubmission_Maps_Correctly_And_Has_No_Audio_R2_Url()
    {
        using var context = CreateDbContext();
        var entityType = context.Model.FindEntityType(typeof(InClassExamSubmission));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("in_class_exam_submissions");

        var props = entityType!.GetProperties().Select(p => p.Name).ToList();
        props.Should().Contain(nameof(InClassExamSubmission.Id));
        props.Should().Contain(nameof(InClassExamSubmission.ShiftId));
        props.Should().Contain(nameof(InClassExamSubmission.StudentId));
        props.Should().Contain(nameof(InClassExamSubmission.QuestionId));
        props.Should().Contain(nameof(InClassExamSubmission.Transcript));
        props.Should().Contain(nameof(InClassExamSubmission.AiScore));
        props.Should().Contain(nameof(InClassExamSubmission.AiFeedback));
        props.Should().Contain(nameof(InClassExamSubmission.FinalScore));
        props.Should().Contain(nameof(InClassExamSubmission.LecturerNotes));
        props.Should().Contain(nameof(InClassExamSubmission.Status));
        props.Should().Contain(nameof(InClassExamSubmission.SubmittedAt));
        props.Should().Contain(nameof(InClassExamSubmission.GradedAt));

        // Cam kết Bất biến 8: TUYỆT ĐỐI KHÔNG CÓ cột audio_r2_url
        props.Any(p => p.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0).Should().BeFalse(
            "Ca thi trong lớp TUYỆT ĐỐI KHÔNG lưu audio lên Cloudflare R2 (Cam kết Bất biến 8)");

        // Check constraint status
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var designEntityType = designModel.FindEntityType(typeof(InClassExamSubmission))!;
        var statusConstraint = designEntityType.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "ck_in_class_exam_submissions_status");
        statusConstraint.Should().NotBeNull();
        statusConstraint!.Sql.Should().Be("status IN ('SUBMITTED', 'AI_GRADED', 'AUDITED')");
    }

    [Fact(DisplayName = "M1-05. Notification maps to notifications with ESR index IX_notifications_user_id_is_read_created_at")]
    public void Notification_Maps_Correctly_With_Explicit_ESR_Index()
    {
        using var context = CreateDbContext();
        var entityType = context.Model.FindEntityType(typeof(Notification));
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("notifications");

        var props = entityType!.GetProperties().Select(p => p.Name).ToList();
        props.Should().Contain(nameof(Notification.Id));
        props.Should().Contain(nameof(Notification.UserId));
        props.Should().Contain(nameof(Notification.Title));
        props.Should().Contain(nameof(Notification.Message));
        props.Should().Contain(nameof(Notification.Type));
        props.Should().Contain(nameof(Notification.IsRead));
        props.Should().Contain(nameof(Notification.CreatedAt));
        props.Should().Contain(nameof(Notification.ReadAt));
        props.Should().Contain(nameof(Notification.RelatedEntityName));
        props.Should().Contain(nameof(Notification.RelatedEntityId));
        props.Should().Contain(nameof(Notification.MetadataJson));

        // ESR Index: Equality (UserId, IsRead) -> Sort (CreatedAt DESC)
        var esrIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "IX_notifications_user_id_is_read_created_at");
        esrIndex.Should().NotBeNull("Index IX_notifications_user_id_is_read_created_at bắt buộc phải được cấu hình chuẩn ESR");
    }

    [Fact(DisplayName = "M1-06. OneWayLockInterceptor blocks modification or deletion of finalized InClassExamShift")]
    public async Task OneWayLock_Enforces_Locked_InClassExamShift()
    {
        using var context = CreateDbContext();

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Quiz 1 - PRN231",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "TEST_SECRET_KEY_1234567890",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };

        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        // Cố tình sửa đổi shift đã khóa
        shift.ShiftName = "Hacked Shift Name";
        Func<Task> actModify = async () => await context.SaveChangesAsync();
        await actModify.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*Không thể sửa hoặc xóa InClassExamShift vì điểm thi đã được khóa một chiều*");

        // Cố tình xóa shift đã khóa
        using var context2 = CreateDbContext();
        var shift2 = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Quiz 2 - PRN231",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "TEST_SECRET_KEY_1234567890",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };
        context2.InClassExamShifts.Add(shift2);
        await context2.SaveChangesAsync();

        context2.InClassExamShifts.Remove(shift2);
        Func<Task> actDelete = async () => await context2.SaveChangesAsync();
        await actDelete.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*Không thể sửa hoặc xóa InClassExamShift vì điểm thi đã được khóa một chiều*");
    }

    [Fact(DisplayName = "M1-07. OneWayLockInterceptor blocks modification of submission when parent InClassExamShift is locked")]
    public async Task OneWayLock_Enforces_Locked_InClassExamSubmission_Children()
    {
        using var context = CreateDbContext();

        var shiftId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Quiz 1 - PRN231",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "TEST_SECRET_KEY_1234567890",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };

        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Bài làm vấn đáp sinh viên",
            AiScore = 8.50m,
            FinalScore = 8.50m,
            Status = "AUDITED"
        };

        context.InClassExamShifts.Add(shift);
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        // Cố tình sửa điểm bài nộp của ca thi đã khóa
        submission.FinalScore = 10.00m;
        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*in-class exam grade aggregate*");
    }
}
