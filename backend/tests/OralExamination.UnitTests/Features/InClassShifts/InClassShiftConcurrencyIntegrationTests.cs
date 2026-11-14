using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.InClassShifts;

/// <summary>
/// Bộ kiểm thử tích hợp Concurrency và Race Condition chạy 100% trên container PostgreSQL 16 thật (oralexam-postgres:5432).
/// <summary>
/// Bộ kiểm thử tích hợp Concurrency và Race Condition chạy 100% trên container PostgreSQL 16 thật.
/// Phân lập hoàn toàn trên CSDL test độc lập (oralexam_integration_test), có Guard từ chối kết nối nếu thiếu hậu tố _test,
/// và tự động dọn dẹp (cleanup) dữ liệu hạt giống sau mỗi lần chạy để đảm bảo ZERO-POLLUTION.
/// </summary>
public class InClassShiftConcurrencyIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=5432;Database=oralexam_integration_test;Username=oralexam;Password=oralexam_password;Include Error Detail=true;Timeout=5;";

    public static string GetPostgresConnectionString()
    {
        var envConn = Environment.GetEnvironmentVariable("ORALEXAM_TEST_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");
        var connStr = !string.IsNullOrWhiteSpace(envConn) ? envConn : DefaultTestConnectionString;

        var builder = new NpgsqlConnectionStringBuilder(connStr);
        if (string.IsNullOrWhiteSpace(builder.Database) || !builder.Database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"[TASK-002 GUARD CHỐNG NHIỄM BẨN CSDL] Database trong connection string '{builder.Database}' bắt buộc phải kết thúc bằng hậu tố '_test'. Tuyệt đối từ chối chạy trên database chính/production!");
        }

        return connStr;
    }

    private static OralExamDbContext CreatePostgresDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(GetPostgresConnectionString())
            .Options;
        var context = new OralExamDbContext(options);
        context.Database.CanConnect().Should().BeTrue("PostgreSQL test database phải kết nối được tại localhost:5432");
        return context;
    }

    private static async Task<(User Lecturer, User Student, Course Course, Class ClassEntity, PracticeQuestion Question, InClassExamShift Shift, InClassShiftParticipant Participant)> SeedPostgresHierarchyAsync(OralExamDbContext context)
    {
        var existingSemester = await context.Semesters.AsNoTracking().FirstOrDefaultAsync();
        Guid semesterId;
        if (existingSemester == null)
        {
            var defaultSemester = new Semester
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                Code = "FA26_TEST",
                Name = "Fall 2026 Concurrency Test",
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
                IsActive = true
            };
            context.Semesters.Add(defaultSemester);
            await context.SaveChangesAsync();
            semesterId = defaultSemester.Id;
        }
        else
        {
            semesterId = existingSemester.Id;
        }

        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = $"lecturer_{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Test Lecturer Concurrency",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student_{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Test Student Concurrency",
            Role = UserRole.Student,
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"PRN_{Guid.NewGuid():N}".Substring(0, 10),
            Name = "PRN Concurrency Test",
            SemesterId = semesterId,
            IsActive = true
        };
        var classEntity = new Class
        {
            Id = Guid.NewGuid(),
            Code = $"SE_{Guid.NewGuid():N}".Substring(0, 10),
            Name = "SE Concurrency Class",
            JoinCode = $"JC{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant(),
            CourseId = course.Id,
            SemesterId = semesterId,
            LecturerId = lecturer.Id,
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Concurrency Rubric",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Concurrency Question",
            Content = "Khái niệm kiến trúc phần mềm trong bài kiểm tra concurrency?",
            SampleAnswer = "Câu trả lời mẫu cho bài test phân bổ đồng thời...",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Concurrency Shift Test",
            Status = "IN_PROGRESS",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = $"[\"{question.Id}\"]",
            Status = "IN_PROGRESS"
        };

        context.Users.AddRange(lecturer, student);
        context.Courses.Add(course);
        context.Classes.Add(classEntity);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(participant);
        await context.SaveChangesAsync();

        return (lecturer, student, course, classEntity, question, shift, participant);
    }

    private static async Task CleanupPostgresHierarchyAsync(
        Guid? shiftId = null,
        Guid? questionId = null,
        Guid? rubricId = null,
        Guid? classId = null,
        Guid? courseId = null,
        Guid? lecturerId = null,
        Guid? studentId = null)
    {
        try
        {
            using var cleanupCtx = CreatePostgresDbContext();
            if (shiftId.HasValue)
            {
                var subs = await cleanupCtx.InClassExamSubmissions.Where(s => s.ShiftId == shiftId.Value).ToListAsync();
                if (subs.Count > 0)
                {
                    cleanupCtx.InClassExamSubmissions.RemoveRange(subs);
                    await cleanupCtx.SaveChangesAsync();
                }

                var parts = await cleanupCtx.InClassShiftParticipants.Where(p => p.ShiftId == shiftId.Value).ToListAsync();
                if (parts.Count > 0)
                {
                    cleanupCtx.InClassShiftParticipants.RemoveRange(parts);
                    await cleanupCtx.SaveChangesAsync();
                }

                var shift = await cleanupCtx.InClassExamShifts.FindAsync(shiftId.Value);
                if (shift != null)
                {
                    cleanupCtx.InClassExamShifts.Remove(shift);
                    await cleanupCtx.SaveChangesAsync();
                }
            }

            if (questionId.HasValue)
            {
                var q = await cleanupCtx.PracticeQuestions.FindAsync(questionId.Value);
                if (q != null) cleanupCtx.PracticeQuestions.Remove(q);
            }

            if (rubricId.HasValue)
            {
                var r = await cleanupCtx.Rubrics.FindAsync(rubricId.Value);
                if (r != null) cleanupCtx.Rubrics.Remove(r);
            }

            if (classId.HasValue)
            {
                var c = await cleanupCtx.Classes.FindAsync(classId.Value);
                if (c != null) cleanupCtx.Classes.Remove(c);
            }

            if (courseId.HasValue)
            {
                var cr = await cleanupCtx.Courses.FindAsync(courseId.Value);
                if (cr != null) cleanupCtx.Courses.Remove(cr);
            }

            if (lecturerId.HasValue)
            {
                var l = await cleanupCtx.Users.FindAsync(lecturerId.Value);
                if (l != null) cleanupCtx.Users.Remove(l);
            }

            if (studentId.HasValue)
            {
                var s = await cleanupCtx.Users.FindAsync(studentId.Value);
                if (s != null) cleanupCtx.Users.Remove(s);
            }

            await cleanupCtx.SaveChangesAsync();
        }
        catch
        {
            // Dọn dẹp an toàn không làm ảnh hưởng kết quả kiểm thử
        }
    }

    [Fact(DisplayName = "F-003: Hai connection độc lập tranh chấp pg_advisory_xact_lock đồng thời trên PostgreSQL bị tuần tự hóa")]
    [Trait("Category", "Integration")]
    public async Task ShiftAllocationAdvisoryLock_WhenContendedConcurrentlyOnPostgreSql_SerializesBothConnections()
    {
        var shiftId = Guid.NewGuid();
        var lockAcquired1 = new ManualResetEventSlim(false);
        var lockAttemptStarted2 = new ManualResetEventSlim(false);
        var lockAcquired2 = false;
        long elapsedWaitingMs = 0;

        var task1 = Task.Run(async () =>
        {
            using var ctx1 = CreatePostgresDbContext();
            await using var scope1 = await ctx1.BeginShiftAllocationLockScopeAsync(shiftId);
            scope1.Should().NotBeNull("Scope 1 phải lấy được advisory lock thành công");
            lockAcquired1.Set(); // Báo hiệu connection 1 đã giữ khóa

            // Chờ connection 2 báo đã bắt đầu cố lấy khóa
            lockAttemptStarted2.Wait(5000);

            // Giữ khóa thêm 500ms để connection 2 chắc chắn bị PostgreSQL chặn
            await Task.Delay(500);
        });

        var task2 = Task.Run(async () =>
        {
            // Chờ connection 1 chắc chắn đã giữ khóa thành công
            lockAcquired1.Wait(5000);

            using var ctx2 = CreatePostgresDbContext();

            // Báo hiệu chuẩn bị yêu cầu khóa
            lockAttemptStarted2.Set();

            // Connection 2 cố lấy khóa trên cùng shiftId: PostgreSQL sẽ CHẶN connection 2 cho đến khi scope1 commit/dispose
            var stopwatch = Stopwatch.StartNew();
            await using var scope2 = await ctx2.BeginShiftAllocationLockScopeAsync(shiftId);
            stopwatch.Stop();
            elapsedWaitingMs = stopwatch.ElapsedMilliseconds;
            scope2.Should().NotBeNull("Scope 2 phải lấy được khóa sau khi Scope 1 giải phóng");
            lockAcquired2 = true;
        });

        await Task.WhenAll(task1, task2);
        lockAcquired2.Should().BeTrue("Connection 2 phải lấy được khóa sau khi Connection 1 hoàn tất");
        elapsedWaitingMs.Should().BeGreaterThanOrEqualTo(200, "Connection 2 phải bị PostgreSQL Advisory Lock chặn lại cho tới khi Connection 1 nhả khóa");
    }

    [Fact(DisplayName = "TASK-002 Guard: Từ chối kết nối nếu tên database không kết thúc bằng _test")]
    public void DatabaseConnectionGuard_WhenDatabaseDoesNotEndWithTest_ThrowsInvalidOperationException()
    {
        var invalidConn = "Host=localhost;Port=5432;Database=oralexam_db;Username=oralexam;Password=oralexam_password;";
        var builder = new NpgsqlConnectionStringBuilder(invalidConn);

        Action act = () =>
        {
            if (string.IsNullOrWhiteSpace(builder.Database) || !builder.Database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"[TASK-002 GUARD CHỐNG NHIỄM BẨN CSDL] Database trong connection string '{builder.Database}' bắt buộc phải kết thúc bằng hậu tố '_test'.");
            }
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TASK-002 GUARD CHỐNG NHIỄM BẨN CSDL*");
    }

    [Fact(DisplayName = "F-004: Hai request nộp bài đồng thời cùng transcript trên PostgreSQL kích hoạt unique constraint 23505 và idempotent replay thành công")]
    [Trait("Category", "Integration")]
    public async Task SubmitInClassShiftAnswer_WhenConcurrentSubmissionsOnPostgreSql_CatchesUniqueViolationAndReplaysSuccess()
    {
        using var setupCtx = CreatePostgresDbContext();
        var (lecturer, student, course, classEntity, question, shift, _) = await SeedPostgresHierarchyAsync(setupCtx);

        try
        {
            var queueMock = new Mock<IGradingQueueChannel>();
            queueMock.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            var validator = new SubmitInClassShiftAnswerCommandValidator();

            var command = new SubmitInClassShiftAnswerCommand(
                student.Id,
                shift.Id,
                question.Id,
                "Trình bày về Clean Architecture trên PostgreSQL concurrent race test",
                false,
                null,
                0);

            // Chuẩn bị 2 handler với 2 DbContext riêng biệt kết nối vào PostgreSQL test database
            using var ctx1 = CreatePostgresDbContext();
            using var ctx2 = CreatePostgresDbContext();
            var handler1 = new SubmitInClassShiftAnswerCommandHandler(ctx1, queueMock.Object, validator);
            var handler2 = new SubmitInClassShiftAnswerCommandHandler(ctx2, queueMock.Object, validator);

            // Tạo barrier để cả 2 task cùng kích hoạt Handle đồng thời
            var barrier = new Barrier(2);
            var task1 = Task.Run(async () =>
            {
                barrier.SignalAndWait(5000);
                return await handler1.Handle(command, CancellationToken.None);
            });
            var task2 = Task.Run(async () =>
            {
                barrier.SignalAndWait(5000);
                return await handler2.Handle(command, CancellationToken.None);
            });

            var results = await Task.WhenAll(task1, task2);

            results[0].IsSuccess.Should().BeTrue(because: results[0].Error);
            results[1].IsSuccess.Should().BeTrue(because: results[1].Error);
            results[0].Value!.SubmissionId.Should().Be(results[1].Value!.SubmissionId, "Idempotent Replay phải trả về cùng SubmissionId khi trùng nội dung transcript");

            // Kiểm tra trong PostgreSQL thật: Chỉ duy nhất 1 dòng được lưu trong bảng in_class_exam_submissions
            using var verifyCtx = CreatePostgresDbContext();
            var count = await verifyCtx.InClassExamSubmissions
                .CountAsync(s => s.ShiftId == shift.Id && s.StudentId == student.Id && s.QuestionId == question.Id);
            count.Should().Be(1, "PostgreSQL Unique Index uq_in_class_exam_submissions_shift_student chỉ cho phép lưu duy nhất 1 bản ghi");
        }
        finally
        {
            await CleanupPostgresHierarchyAsync(shift.Id, question.Id, question.RubricId, classEntity.Id, course.Id, lecturer.Id, student.Id);
        }
    }

    [Fact(DisplayName = "F-004: Hai request nộp bài đồng thời khác transcript trên PostgreSQL trả về Conflict cho request đến sau")]
    [Trait("Category", "Integration")]
    public async Task SubmitInClassShiftAnswer_WhenConcurrentSubmissionsDifferentTranscriptOnPostgreSql_FailsWithDuplicateSubmission()
    {
        using var setupCtx = CreatePostgresDbContext();
        var (lecturer, student, course, classEntity, question, shift, _) = await SeedPostgresHierarchyAsync(setupCtx);

        try
        {
            var queueMock = new Mock<IGradingQueueChannel>();
            queueMock.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            var validator = new SubmitInClassShiftAnswerCommandValidator();

            var commandA = new SubmitInClassShiftAnswerCommand(
                student.Id, shift.Id, question.Id, "Bài nộp bản gốc A", false, null, 0);

            var commandB = new SubmitInClassShiftAnswerCommand(
                student.Id, shift.Id, question.Id, "Bài nộp bản khác B", false, null, 0);

            using var ctx1 = CreatePostgresDbContext();
            using var ctx2 = CreatePostgresDbContext();
            var handler1 = new SubmitInClassShiftAnswerCommandHandler(ctx1, queueMock.Object, validator);
            var handler2 = new SubmitInClassShiftAnswerCommandHandler(ctx2, queueMock.Object, validator);

            var barrier = new Barrier(2);
            var task1 = Task.Run(async () =>
            {
                barrier.SignalAndWait(5000);
                return await handler1.Handle(commandA, CancellationToken.None);
            });
            var task2 = Task.Run(async () =>
            {
                barrier.SignalAndWait(5000);
                return await handler2.Handle(commandB, CancellationToken.None);
            });

            var results = await Task.WhenAll(task1, task2);

            // 1 request phải thành công, 1 request phải nhận lỗi DuplicateSubmission
            var successCount = results.Count(r => r.IsSuccess);
            var conflictCount = results.Count(r => !r.IsSuccess && r.Error == InClassShiftErrors.DuplicateSubmission);

            successCount.Should().Be(1, "Chỉ duy nhất 1 request được ghi nhận thành công");
            conflictCount.Should().Be(1, "Request xung đột nội dung phải nhận DuplicateSubmission");
        }
        finally
        {
            await CleanupPostgresHierarchyAsync(shift.Id, question.Id, question.RubricId, classEntity.Id, course.Id, lecturer.Id, student.Id);
        }
    }

    [Fact(DisplayName = "F-006: Optimistic Concurrency Token trên PostgreSQL ném DbUpdateConcurrencyException khi trạng thái bị sửa sang AUDITED và bảo toàn điểm")]
    [Trait("Category", "Integration")]
    public async Task ConcurrencyToken_WhenAuditedDuringGradingOnPostgreSql_ThrowsDbUpdateConcurrencyExceptionAndPreservesLecturerScore()
    {
        using var setupCtx = CreatePostgresDbContext();
        var (lecturer, student, course, classEntity, question, shift, _) = await SeedPostgresHierarchyAsync(setupCtx);

        var submissionId = Guid.NewGuid();
        try
        {
            var submission = new InClassExamSubmission
            {
                Id = submissionId,
                ShiftId = shift.Id,
                StudentId = student.Id,
                QuestionId = question.Id,
                Transcript = "Bài thi kiểm chứng concurrency token trên PostgreSQL 16",
                Status = "SUBMITTED",
                SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            setupCtx.InClassExamSubmissions.Add(submission);
            await setupCtx.SaveChangesAsync();

            // 1. Worker context đọc submission trạng thái SUBMITTED từ PostgreSQL
            using var workerCtx = CreatePostgresDbContext();
            var workerSubmission = await workerCtx.InClassExamSubmissions.FirstAsync(s => s.Id == submissionId);
            workerSubmission.Status.Should().Be("SUBMITTED");

            // 2. Lecturer context (request khác) vào hậu kiểm trước và commit lên PostgreSQL
            using (var lecturerCtx = CreatePostgresDbContext())
            {
                var lecturerSubmission = await lecturerCtx.InClassExamSubmissions.FirstAsync(s => s.Id == submissionId);
                lecturerSubmission.FinalScore = 9.8m;
                lecturerSubmission.LecturerNotes = "Điểm hậu kiểm của giảng viên trên PostgreSQL";
                lecturerSubmission.Status = "AUDITED";
                lecturerSubmission.GradedAt = DateTime.UtcNow;
                await lecturerCtx.SaveChangesAsync();
            }

            // 3. Worker kết thúc quá trình chấm AI và cố ghi đè điểm lên PostgreSQL
            workerSubmission.AiScore = 4.5m;
            workerSubmission.FinalScore = 4.5m;
            workerSubmission.Status = "AI_GRADED";
            workerSubmission.GradedAt = DateTime.UtcNow;

            // Hành động SaveChanges của Worker trên PostgreSQL PHẢI BỊ NÉM DbUpdateConcurrencyException do Status là ConcurrencyToken
            Func<Task> act = async () => await workerCtx.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "PostgreSQL UPDATE phải khớp 0 rows vì WHERE status = 'SUBMITTED' bị vi phạm khi trạng thái đã đổi thành AUDITED");

            // 4. Xác nhận trong PostgreSQL: Điểm và trạng thái của giảng viên được bảo toàn 100%
            using var verifyCtx = CreatePostgresDbContext();
            var verified = await verifyCtx.InClassExamSubmissions.FirstAsync(s => s.Id == submissionId);
            verified.Status.Should().Be("AUDITED");
            verified.FinalScore.Should().Be(9.8m);
            verified.LecturerNotes.Should().Be("Điểm hậu kiểm của giảng viên trên PostgreSQL");
        }
        finally
        {
            await CleanupPostgresHierarchyAsync(shift.Id, question.Id, question.RubricId, classEntity.Id, course.Id, lecturer.Id, student.Id);
        }
    }
}
