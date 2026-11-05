using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

public class ChallengerM1AdversarialTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new OneWayLockInterceptor())
            .Options;

        return new OralExamDbContext(options);
    }

    // =========================================================================
    // SECTION 1: ADVERSARIAL STRESS TEST TRÊN BẢNG NOTIFICATIONS 3NF
    // =========================================================================

    [Fact(DisplayName = "CHALLENGE-M1-01: Notifications ESR Index phải có đúng thứ tự cột: Equality (UserId, IsRead) -> Sort (CreatedAt)")]
    public void Notifications_ESR_Index_Strict_Column_Order_Check()
    {
        using var context = CreateDbContext();
        var entityType = context.Model.FindEntityType(typeof(Notification));
        entityType.Should().NotBeNull();

        var esrIndex = entityType!.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "IX_notifications_user_id_is_read_created_at");

        esrIndex.Should().NotBeNull("Index IX_notifications_user_id_is_read_created_at bắt buộc phải tồn tại trong DbContext");

        // ESR Order Check:
        // Cột 0: UserId (Equality)
        // Cột 1: IsRead (Equality)
        // Cột 2: CreatedAt (Sort)
        esrIndex!.Properties.Count.Should().Be(3, "Composite ESR index phải chứa đúng 3 thuộc tính");
        esrIndex.Properties[0].Name.Should().Be(nameof(Notification.UserId), "Cột đầu tiên trong ESR index phải là Equality: UserId");
        esrIndex.Properties[1].Name.Should().Be(nameof(Notification.IsRead), "Cột thứ hai trong ESR index phải là Equality: IsRead");
        esrIndex.Properties[2].Name.Should().Be(nameof(Notification.CreatedAt), "Cột thứ ba trong ESR index phải là Sort: CreatedAt");
    }

    [Fact(DisplayName = "CHALLENGE-M1-02: Notification 3NF Anti-Column-Stuffing: Không chứa thuộc tính mảng, enum collection, hoặc chuỗi nhồi")]
    public void Notification_Entity_Strict_1NF_3NF_Compliance()
    {
        var properties = typeof(Notification).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // Kiểm tra không có mảng hoặc generic collection (trừ navigation object đơn)
            if (prop.PropertyType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
            {
                prop.PropertyType.Should().NotBeAssignableTo<System.Collections.IEnumerable>(
                    $"Thuộc tính {prop.Name} không được là mảng hoặc collection để đảm bảo chuẩn 1NF");
            }

            // Kiểm tra tên không chứa dấu hiệu column-stuffing
            prop.Name.ToLowerInvariant().Should().NotContain("tags", "Bảng notifications không dùng cột tags nhồi chuỗi");
            prop.Name.ToLowerInvariant().Should().NotContain("csv", "Bảng notifications không dùng cột CSV");
        }

        // Kiểm tra quan hệ khóa ngoại User chuẩn xác
        var userNavProp = properties.FirstOrDefault(p => p.Name == nameof(Notification.User));
        userNavProp.Should().NotBeNull();
        userNavProp!.PropertyType.Should().Be(typeof(User));
    }

    // =========================================================================
    // SECTION 2: ADVERSARIAL STRESS TEST TRÊN INCLASSEXAMSUBMISSION & INVARIANT 8
    // =========================================================================

    [Fact(DisplayName = "CHALLENGE-M1-03: Cam kết Bất biến 8: InClassExamSubmission và toàn bộ thực thể ca thi lớp TUYỆT ĐỐI KHÔNG chứa cột Audio")]
    public void InClassExamSubmission_Zero_Audio_Strict_Audit()
    {
        var targetTypes = new[]
        {
            typeof(InClassExamShift),
            typeof(InClassShiftPassword),
            typeof(InClassShiftParticipant),
            typeof(InClassExamSubmission)
        };

        var forbiddenTerms = new[] { "audio", "recording", "voiceurl", "voice_url", "r2url", "r2_url", "mediaurl", "sound" };

        foreach (var type in targetTypes)
        {
            var propNames = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(p => p.Name.ToLowerInvariant())
                .ToList();

            foreach (var term in forbiddenTerms)
            {
                propNames.Should().NotContain(name => name.Contains(term),
                    $"Thực thể {type.Name} vi phạm Cam kết Bất biến 8: Chứa thuộc tính liên quan đến '{term}'");
            }
        }
    }

    // =========================================================================
    // SECTION 3: ADVERSARIAL STRESS TEST TRÊN ONEWAYLOCKINTERCEPTOR
    // =========================================================================

    [Fact(DisplayName = "CHALLENGE-M1-04: Cố tình bẻ khóa IsLocked (true -> false) trên InClassExamShift: Bắt buộc bị chặn ném OneWayLockException")]
    public async Task OneWayLock_Tampering_IsLocked_Flag_Directly_Throws_Exception()
    {
        using var context = CreateDbContext();

        var shiftId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "PRN231 Progress Test",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            TotpSecretKey = "TEST_SECRET_KEY_1234567890",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };

        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        // Kịch bản tấn công: Giảng viên cố tình set IsLocked = false để mở lại ca thi sửa điểm lậu
        shift.IsLocked = false;
        Func<Task> act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*Không thể sửa hoặc xóa InClassExamShift vì điểm thi đã được khóa một chiều*");
    }

    [Fact(DisplayName = "CHALLENGE-M1-05: Cố tình xóa bài nộp (DELETE InClassExamSubmission) thuộc ca thi đã khóa: Bắt buộc bị chặn ném OneWayLockException")]
    public async Task OneWayLock_Delete_Submission_Of_Locked_Shift_Throws_Exception()
    {
        using var context = CreateDbContext();

        var shiftId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "PRN231 Quiz",
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            EndTime = DateTime.UtcNow.AddMinutes(-10),
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
            Transcript = "Bài làm hợp lệ của sinh viên",
            AiScore = 7.00m,
            FinalScore = 7.00m,
            Status = "AUDITED"
        };

        context.InClassExamShifts.Add(shift);
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        // Kịch bản tấn công: Kẻ gian xóa bài làm của sinh viên sau khi ca thi đã finalize
        context.InClassExamSubmissions.Remove(submission);
        Func<Task> act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*in-class exam grade aggregate*");
    }

    [Fact(DisplayName = "CHALLENGE-M1-06: Sửa đổi bài nộp khi ca thi CHƯA khóa (IsLocked == false): Phải thành công bình thường")]
    public async Task OneWayLock_Allows_Modification_When_Shift_Is_Not_Locked()
    {
        using var context = CreateDbContext();

        var shiftId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "PRN231 In-Progress Shift",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = "TEST_SECRET_KEY_1234567890",
            Status = "IN_PROGRESS",
            IsLocked = false
        };

        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Bài làm đang được chấm",
            AiScore = 6.00m,
            FinalScore = 6.00m,
            Status = "SUBMITTED"
        };

        context.InClassExamShifts.Add(shift);
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        // Giảng viên hậu kiểm sửa điểm khi ca thi chưa chốt -> HỢP LỆ
        submission.FinalScore = 8.50m;
        submission.Status = "AUDITED";
        submission.LecturerNotes = "Sinh viên giải thích tốt thuật toán";

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().NotThrowAsync();

        var updated = await context.InClassExamSubmissions.FindAsync(submission.Id);
        updated!.FinalScore.Should().Be(8.50m);
        updated.Status.Should().Be("AUDITED");
    }

    [Fact(DisplayName = "CHALLENGE-M1-07: Batch update submissions với 1 submission thuộc ca đã khóa: Toàn bộ transaction phải bị chặn")]
    public async Task OneWayLock_Batch_Update_With_One_Locked_Shift_Aborts_Entire_Transaction()
    {
        using var context = CreateDbContext();

        var unlockedShiftId = Guid.NewGuid();
        var lockedShiftId = Guid.NewGuid();

        var unlockedShift = new InClassExamShift
        {
            Id = unlockedShiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "PRN231 Unlocked Shift",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = "TEST_SECRET_KEY_1234567890",
            Status = "IN_PROGRESS",
            IsLocked = false
        };

        var lockedShift = new InClassExamShift
        {
            Id = lockedShiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "PRN231 Locked Shift",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            TotpSecretKey = "TEST_SECRET_KEY_1234567890",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };

        var sub1 = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = unlockedShiftId,
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Bài làm 1",
            AiScore = 5.0m,
            FinalScore = 5.0m,
            Status = "SUBMITTED"
        };

        var sub2 = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = lockedShiftId,
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Bài làm 2",
            AiScore = 5.0m,
            FinalScore = 5.0m,
            Status = "AUDITED"
        };

        context.InClassExamShifts.AddRange(unlockedShift, lockedShift);
        context.InClassExamSubmissions.AddRange(sub1, sub2);
        await context.SaveChangesAsync();

        // Cố tình sửa cả 2 bài trong 1 batch SaveChanges
        sub1.FinalScore = 9.0m;
        sub2.FinalScore = 9.0m; // Thuộc lockedShift

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*in-class exam grade aggregate*");
    }
}
