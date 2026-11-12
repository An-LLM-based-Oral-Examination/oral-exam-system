using System;
using System.IO;
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

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng Milestone 1 (Empirical Challenger M1_1).
/// Thực thi độc lập bởi Challenger 1 (challenger_m1_1).
/// 
/// Trọng tâm thử thách đối kháng:
/// 1. Kiểm tra Check Constraints và Unique Constraints của các bảng mới trong DbContext Model:
///    - InClassExamShift: ck_in_class_exam_shifts_status, ck_in_class_exam_shifts_time.
///    - InClassShiftParticipant: ck_in_class_shift_participants_status, uq_in_class_shift_participants_shift_student.
///    - InClassExamSubmission: ck_in_class_exam_submissions_status.
/// 2. Kiểm nghiệm hành vi OneWayLockInterceptor đối với InClassExamShift và Aggregate Children InClassExamSubmission khi IsLocked == true.
/// 3. Kiểm định việc TUYỆT ĐỐI KHÔNG TỒN TẠI audio_r2_url trong InClassExamSubmission (Cam kết 8).
/// 4. Kiểm tra sự đồng bộ giữa C# Entity, EF Core Model, Migration file DDL và Model Snapshot.
/// </summary>
public class AdversarialMilestone1InClassExamChallengerTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new OneWayLockInterceptor())
            .Options;

        return new OralExamDbContext(options);
    }

    #region 1. Check Constraints & Unique Constraints in EF Core Model

    [Fact(DisplayName = "ADV-M1-01: Model Metadata của InClassExamShift phải chứa đầy đủ 2 Check Constraints chuẩn xác")]
    public void InClassExamShift_Model_Must_Contain_Valid_Check_Constraints()
    {
        using var context = CreateDbContext();
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var entityType = designModel.FindEntityType(typeof(InClassExamShift));
        entityType.Should().NotBeNull("InClassExamShift phải được đăng ký trong Model");

        var constraints = entityType!.GetCheckConstraints().ToList();
        constraints.Should().HaveCountGreaterOrEqualTo(2);

        // 1. ck_in_class_exam_shifts_status
        var statusConstraint = constraints.FirstOrDefault(c => c.Name == "ck_in_class_exam_shifts_status");
        statusConstraint.Should().NotBeNull("ck_in_class_exam_shifts_status phải tồn tại");
        statusConstraint!.Sql.Should().Be("status IN ('SCHEDULED', 'IN_PROGRESS', 'FINALIZED', 'CLOSED')");

        // 2. ck_in_class_exam_shifts_time
        var timeConstraint = constraints.FirstOrDefault(c => c.Name == "ck_in_class_exam_shifts_time");
        timeConstraint.Should().NotBeNull("ck_in_class_exam_shifts_time phải tồn tại");
        timeConstraint!.Sql.Should().Be("end_time >= start_time");
    }

    [Fact(DisplayName = "ADV-M1-02: Model Metadata của InClassShiftParticipant phải chứa Check Constraint và Unique Constraint (ShiftId, StudentId)")]
    public void InClassShiftParticipant_Model_Must_Contain_Status_Check_And_Unique_Constraint()
    {
        using var context = CreateDbContext();
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var entityType = designModel.FindEntityType(typeof(InClassShiftParticipant));
        entityType.Should().NotBeNull("InClassShiftParticipant phải được đăng ký trong Model");

        // 1. Check constraint
        var statusConstraint = entityType!.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "ck_in_class_shift_participants_status");
        statusConstraint.Should().NotBeNull("ck_in_class_shift_participants_status phải tồn tại");
        statusConstraint!.Sql.Should().Be("status IN ('JOINED', 'IN_PROGRESS', 'SUBMITTED', 'GRADED', 'FINALIZED', 'ABSENT')");

        // 2. Unique Constraint (uq_in_class_shift_participants_shift_student)
        var runtimeEntityType = context.Model.FindEntityType(typeof(InClassShiftParticipant))!;
        var uniqueIndex = runtimeEntityType.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "uq_in_class_shift_participants_shift_student");

        uniqueIndex.Should().NotBeNull("uq_in_class_shift_participants_shift_student bắt buộc phải tồn tại");
        uniqueIndex!.IsUnique.Should().BeTrue("Index uq_in_class_shift_participants_shift_student phải có IsUnique = true");

        var propNames = uniqueIndex.Properties.Select(p => p.Name).ToList();
        propNames.Should().Equal(new[] { nameof(InClassShiftParticipant.ShiftId), nameof(InClassShiftParticipant.StudentId) });
    }

    [Fact(DisplayName = "ADV-M1-03: Model Metadata của InClassExamSubmission phải chứa Check Constraint status chuẩn xác")]
    public void InClassExamSubmission_Model_Must_Contain_Status_Check_Constraint()
    {
        using var context = CreateDbContext();
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var entityType = designModel.FindEntityType(typeof(InClassExamSubmission));
        entityType.Should().NotBeNull("InClassExamSubmission phải được đăng ký trong Model");

        var statusConstraint = entityType!.GetCheckConstraints()
            .FirstOrDefault(c => c.Name == "ck_in_class_exam_submissions_status");
        statusConstraint.Should().NotBeNull("ck_in_class_exam_submissions_status phải tồn tại");
        statusConstraint!.Sql.Should().Be("status IN ('SUBMITTED', 'AI_GRADED', 'AUDITED')");
    }

    [Fact(DisplayName = "ADV-M1-04: Migration CSDL chứa đúng các định nghĩa Check Constraint và Unique Constraint cho 4 bảng")]
    public void Migration_DDL_Must_Declare_CheckConstraints_And_UniqueIndex()
    {
        // Kiểm tra file Migration thực tế để đảm bảo DDL của PostgreSQL chứa đúng ràng buộc
        var migrationFiles = Directory.GetFiles(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src/Infrastructure/Migrations"),
            "*AddInClassExamShiftsAndNotifications3NF.cs");

        migrationFiles.Should().NotBeEmpty("File migration AddInClassExamShiftsAndNotifications3NF phải tồn tại");

        var migrationCode = File.ReadAllText(migrationFiles.First());

        // Ca thi được tạo ở migration gốc; status hiện hành là CLOSED, nằm ở migration nối tiếp.
        migrationCode.Should().Contain("ck_in_class_exam_shifts_status");

        var followUpFiles = Directory.GetFiles(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src/Infrastructure/Migrations"),
            "*AddClassJoinPasswordAndShiftExamWindow.cs");
        followUpFiles.Should().NotBeEmpty("File migration AddClassJoinPasswordAndShiftExamWindow phải tồn tại");
        var followUpCode = File.ReadAllText(followUpFiles.First());
        followUpCode.Should().Contain("status IN ('SCHEDULED', 'IN_PROGRESS', 'FINALIZED', 'CLOSED')");
        followUpCode.Should().Contain("join_password_plain");
        followUpCode.Should().Contain("ck_in_class_exam_shifts_exam_duration");

        // Check constraint ck_in_class_exam_shifts_time
        migrationCode.Should().Contain("ck_in_class_exam_shifts_time");
        migrationCode.Should().Contain("end_time >= start_time");

        // Check constraint ck_in_class_shift_participants_status
        migrationCode.Should().Contain("ck_in_class_shift_participants_status");
        migrationCode.Should().Contain("status IN ('JOINED', 'SUBMITTED', 'ABSENT')");

        // Check constraint ck_in_class_exam_submissions_status
        migrationCode.Should().Contain("ck_in_class_exam_submissions_status");
        migrationCode.Should().Contain("status IN ('SUBMITTED', 'AI_GRADED', 'AUDITED')");

        // Unique constraint uq_in_class_shift_participants_shift_student
        migrationCode.Should().Contain("uq_in_class_shift_participants_shift_student");
        migrationCode.Should().Contain("unique: true");
    }

    #endregion

    #region 2. One-Way Lock Verification on InClassExamShift & Submissions

    [Fact(DisplayName = "ADV-M1-05: OneWayLockInterceptor ném ngoại lệ khi cố tình sửa đổi bất kỳ thuộc tính nào của InClassExamShift đã bị khóa")]
    public async Task OneWayLock_Must_Block_Modification_Of_Locked_InClassExamShift()
    {
        using var context = CreateDbContext();

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Lab Viva PRN231",
            StartTime = DateTime.UtcNow.AddMinutes(-45),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "SECRET_1234567890_TOTP_KEY",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };

        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        // 1. Thử thay đổi ShiftName
        shift.ShiftName = "Hacked Shift Name";
        Func<Task> act1 = async () => await context.SaveChangesAsync();
        await act1.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*Không thể sửa hoặc xóa InClassExamShift vì điểm thi đã được khóa một chiều*");

        // 2. Thử thay đổi Status
        using var context2 = CreateDbContext();
        var shift2 = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Lab Viva 2",
            StartTime = DateTime.UtcNow.AddMinutes(-45),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "SECRET_1234567890_TOTP_KEY",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };
        context2.InClassExamShifts.Add(shift2);
        await context2.SaveChangesAsync();

        shift2.Status = "IN_PROGRESS";
        Func<Task> act2 = async () => await context2.SaveChangesAsync();
        await act2.Should().ThrowAsync<OneWayLockException>();

        // 3. Thử mở khóa IsLocked = false (Security Bypass Attack)
        using var context3 = CreateDbContext();
        var shift3 = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Lab Viva 3",
            StartTime = DateTime.UtcNow.AddMinutes(-45),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "SECRET_1234567890_TOTP_KEY",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };
        context3.InClassExamShifts.Add(shift3);
        await context3.SaveChangesAsync();

        shift3.IsLocked = false;
        Func<Task> act3 = async () => await context3.SaveChangesAsync();
        await act3.Should().ThrowAsync<OneWayLockException>();
    }

    [Fact(DisplayName = "ADV-M1-06: OneWayLockInterceptor ném ngoại lệ khi cố tình xóa InClassExamShift đã bị khóa")]
    public async Task OneWayLock_Must_Block_Deletion_Of_Locked_InClassExamShift()
    {
        using var context = CreateDbContext();

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Lab Viva Delete Target",
            StartTime = DateTime.UtcNow.AddMinutes(-45),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "SECRET_1234567890_TOTP_KEY",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };

        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        context.InClassExamShifts.Remove(shift);
        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*Không thể sửa hoặc xóa InClassExamShift vì điểm thi đã được khóa một chiều*");
    }

    [Fact(DisplayName = "ADV-M1-07: OneWayLockInterceptor bảo vệ Aggregate Children InClassExamSubmission khi ca thi InClassExamShift đã khóa")]
    public async Task OneWayLock_Must_Block_Submission_Changes_When_Parent_Shift_Is_Locked()
    {
        using var context = CreateDbContext();

        var shiftId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Quiz Finalized",
            StartTime = DateTime.UtcNow.AddMinutes(-60),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "TOTP_SECRET_KEY_987654321",
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
            Transcript = "Câu trả lời của sinh viên ban đầu",
            AiScore = 7.50m,
            FinalScore = 7.50m,
            LecturerNotes = "Chấm điểm chuẩn",
            Status = "AUDITED"
        };

        context.InClassExamShifts.Add(shift);
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        // 1. Thử thay đổi FinalScore của submission
        submission.FinalScore = 9.50m;
        Func<Task> actUpdateScore = async () => await context.SaveChangesAsync();
        await actUpdateScore.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*in-class exam grade aggregate*");

        // 2. Thử xóa submission của ca thi đã khóa
        using var context2 = CreateDbContext();
        var shift2Id = Guid.NewGuid();
        var shift2 = new InClassExamShift
        {
            Id = shift2Id,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Quiz Finalized 2",
            StartTime = DateTime.UtcNow.AddMinutes(-60),
            EndTime = DateTime.UtcNow,
            TotpSecretKey = "TOTP_SECRET_KEY_987654321",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };

        var submission2 = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift2Id,
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Câu trả lời của sinh viên",
            AiScore = 8.00m,
            FinalScore = 8.00m,
            Status = "AUDITED"
        };

        context2.InClassExamShifts.Add(shift2);
        context2.InClassExamSubmissions.Add(submission2);
        await context2.SaveChangesAsync();

        context2.InClassExamSubmissions.Remove(submission2);
        Func<Task> actDeleteSub = async () => await context2.SaveChangesAsync();
        await actDeleteSub.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*in-class exam grade aggregate*");
    }

    [Fact(DisplayName = "ADV-M1-08: OneWayLockInterceptor cho phép tạo và sửa submission khi InClassExamShift CHƯA bị khóa (IsLocked == false)")]
    public async Task OneWayLock_Allows_Modification_When_Shift_Is_Not_Locked()
    {
        using var context = CreateDbContext();

        var shiftId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Quiz In Progress",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(20),
            TotpSecretKey = "TOTP_SECRET_KEY_ACTIVE",
            Status = "IN_PROGRESS",
            IsLocked = false
        };

        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Câu trả lời của sinh viên",
            AiScore = 6.00m,
            FinalScore = 6.00m,
            Status = "SUBMITTED"
        };

        context.InClassExamShifts.Add(shift);
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        // Giảng viên chấm điểm và điều chỉnh khi chưa chốt
        submission.FinalScore = 7.50m;
        submission.LecturerNotes = "Cộng 1.5 điểm do trả lời bổ sung tốt";
        submission.Status = "AUDITED";

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().NotThrowAsync("Khi ca thi chưa chốt (IsLocked == false), giảng viên được phép sửa điểm");
    }

    #endregion

    #region 3. Invariant 8: Zero Audio R2 in InClassExamSubmission

    [Fact(DisplayName = "ADV-M1-09: InClassExamSubmission TUYỆT ĐỐI KHÔNG CÓ cột audio_r2_url hoặc bất kỳ thuộc tính âm thanh nào")]
    public void InClassExamSubmission_Must_Not_Contain_Audio_Properties()
    {
        var submissionType = typeof(InClassExamSubmission);
        var properties = submissionType.GetProperties().Select(p => p.Name).ToList();

        // 1. Kiểm tra reflection trên CLR Type
        var audioProps = properties
            .Where(p => p.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        p.IndexOf("r2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        p.IndexOf("recording", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        audioProps.Should().BeEmpty(
            "Cam kết Kỹ thuật Bất biến số 8: Ca thi trong lớp TUYỆT ĐỐI KHÔNG lưu audio lên Cloudflare R2");

        // 2. Kiểm tra EF Core Model Property mapping
        using var context = CreateDbContext();
        var entityType = context.Model.FindEntityType(submissionType)!;
        var efProperties = entityType.GetProperties().Select(p => p.Name).ToList();

        var efAudioProps = efProperties
            .Where(p => p.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        p.IndexOf("r2", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        efAudioProps.Should().BeEmpty();

        // 3. Kiểm tra Model Snapshot hoặc Migrations Designer
        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Infrastructure/Migrations/OralExamDbContextModelSnapshot.cs");
        var migrationDesignerPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Infrastructure/Migrations/20261105061650_AddInClassExamShiftsAndNotifications3NF.Designer.cs");
        var targetFile = File.Exists(snapshotPath) && File.ReadAllText(snapshotPath).Contains("InClassExamSubmission")
            ? snapshotPath
            : (File.Exists(migrationDesignerPath) ? migrationDesignerPath : null);

        if (targetFile != null)
        {
            var content = File.ReadAllText(targetFile);
            var idx = content.IndexOf("modelBuilder.Entity(\"OralExamination.Domain.Entities.InClassExamSubmission\"");
            if (idx >= 0)
            {
                var inClassSubmBlock = content.Substring(idx);
                var endIdx = inClassSubmBlock.IndexOf("});");
                if (endIdx > 0)
                {
                    inClassSubmBlock = inClassSubmBlock.Substring(0, endIdx);
                    inClassSubmBlock.Should().NotContain("audio");
                    inClassSubmBlock.Should().NotContain("r2");
                }
            }
        }
    }

    #endregion
}
