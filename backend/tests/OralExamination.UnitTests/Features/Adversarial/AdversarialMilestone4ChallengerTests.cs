using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using API.Controllers.v1;
using MediatR;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử đối kháng thực nghiệm (Adversarial Stress & Edge Cases Tests) cho Milestone 4.
/// Được thiết kế và thực thi độc lập bởi Challenger 1 (teamwork_preview_challenger_m4_1).
/// 
/// Nội dung kiểm chứng:
/// 1. Ca biên PublishGradesCommandHandler:
///    - ADV-M4-01: Ca thi không có vé thi nào (fail).
///    - ADV-M4-02: Ca thi có 5 vé thi, 4 vé có FinalScore, 1 vé FinalScore = null (BẮT BUỘC FAIL).
///    - ADV-M4-03: Ca thi có 5 vé thi, 100% có FinalScore hợp lệ (BẮT BUỘC PASS, 5 vé IsLocked = true).
///    - ADV-M4-04: Thử sửa đổi vé thi đã khóa -> OneWayLockInterceptor ném InvalidOperationException.
///    - ADV-M4-05: Thử xóa vé thi đã khóa -> OneWayLockInterceptor ném InvalidOperationException.
///    - ADV-M4-06: Thử sửa đổi hoặc xóa LecturerAudit đã khóa -> OneWayLockInterceptor ném InvalidOperationException.
///    - ADV-M4-07: Ca thi 5 vé, vé 5 có 2 câu hỏi (1 có điểm, 1 thiếu điểm) -> BẮT BUỘC FAIL.
///    - ADV-M4-08: Ca thi 5 vé, vé 5 chỉ có AiScore (FinalScore null) -> BẮT BUỘC PASS, đồng bộ FinalScore.
/// 
/// 2. Ca biên GetAuditEvidenceQueryHandler:
///    - ADV-M4-09: Xác minh đầy đủ các trường AudioR2Url, AudioHashSha256, TranscriptWhisper, CotTrace, AiScore, FinalScore, ConfidenceScore, IsSuspicious.
///    - ADV-M4-10: Sinh viên có nhiều submissions -> Transcript và CoT được ghép nối chuẩn xác "\n\n---\n\n".
///    - ADV-M4-11: Ranh giới AI Doubt Guard theo ConfidenceScore (0.69m vs 0.70m).
///    - ADV-M4-12: Ranh giới AI Doubt Guard theo độ lệch điểm Giảng viên vs AI (2.00m vs 1.99m).
///    - ADV-M4-13: Vé thi không có submission (sinh viên vắng) -> trả về an toàn, không crash.
///    - ADV-M4-14: Ca thi không tồn tại -> trả về Failure.
/// 
/// 3. Rà soát Controller Security & Error Handling:
///    - ADV-M4-15: OfficialExamsController.PublishGrades với request null & no auth -> trả về 422 an toàn.
///    - ADV-M4-16: OfficialExamsController.GetAuditEvidence với ca thi không tồn tại -> trả về 404 RFC 7807.
/// </summary>
public class AdversarialMilestone4ChallengerTests
{
    private OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var databaseName = dbName ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .AddInterceptors(new OneWayLockInterceptor())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Lecturer, RealExamSessionShift Shift, List<StudentExamTicket> Tickets)> SeedShiftWith5TicketsAsync(
        OralExamDbContext context,
        bool fifthStudentHasScore = true,
        bool fifthStudentHasOnlyAiScore = false,
        bool fifthStudentHasPartialSubmissions = false)
    {
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = $"lecturer.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "TS. Giảng Viên Trưởng Ban Chấm",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 1,
            HasFollowUp = true,
            IsActive = true
        };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = Guid.NewGuid(),
            Title = "Kỳ thi Vấn đáp Chính thức Lab PRN231",
            ExamDate = new DateOnly(2026, 10, 20),
            Status = "in_progress"
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 - Sáng (Phòng Lab 301)",
            RoomLab = "Lab 301",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "in_progress"
        };

        context.Users.Add(lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);

        var tickets = new List<StudentExamTicket>();

        // Tạo đúng 5 vé thi cho 5 sinh viên (Seats 1 -> 5)
        for (int i = 1; i <= 5; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"student{i}_{Guid.NewGuid():N}@fpt.edu.vn",
                FullName = $"Sinh Viên Thí Sinh Số {i}",
                StudentCode = $"SE1700{i:D2}",
                Role = UserRole.Student,
                IsActive = true
            };
            context.Users.Add(student);

            var ticket = new StudentExamTicket
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = student.Id,
                SeatNumber = i,
                IpAddress = $"192.168.1.{100 + i}",
                Status = "AUDITED",
                IsLocked = false
            };

            if (i < 5)
            {
                // Vé 1 -> 4: 100% có FinalScore hợp lệ
                var submission = new ExamQuestionSubmission
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticket.Id,
                    ExamQuestionId = Guid.NewGuid(),
                    AudioR2Url = $"https://pub-r2.oralexam.fpt.edu.vn/exams/{shift.Id}/{i:D2}_{student.StudentCode}.webm",
                    AudioHashSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    TranscriptWhisper = $"Thí sinh {i} trả lời mạch lạc về ASP.NET Core và PostgreSQL.",
                    AiScore = 8.0m + (i * 0.2m),
                    FinalScore = 8.0m + (i * 0.2m),
                    GradingStatus = "audited"
                };
                ticket.Submissions.Add(submission);
                context.ExamQuestionSubmissions.Add(submission);
            }
            else
            {
                // Vé số 5: Xử lý theo kịch bản kiểm thử
                if (fifthStudentHasPartialSubmissions)
                {
                    // 1 câu có điểm, 1 câu thiếu điểm
                    var sub1 = new ExamQuestionSubmission
                    {
                        Id = Guid.NewGuid(),
                        TicketId = ticket.Id,
                        ExamQuestionId = Guid.NewGuid(),
                        AiScore = 7.5m,
                        FinalScore = 7.5m,
                        GradingStatus = "audited"
                    };
                    var sub2 = new ExamQuestionSubmission
                    {
                        Id = Guid.NewGuid(),
                        TicketId = ticket.Id,
                        ExamQuestionId = Guid.NewGuid(),
                        AiScore = null,
                        FinalScore = null,
                        GradingStatus = "pending"
                    };
                    ticket.Submissions.Add(sub1);
                    ticket.Submissions.Add(sub2);
                    context.ExamQuestionSubmissions.AddRange(sub1, sub2);
                }
                else if (fifthStudentHasOnlyAiScore)
                {
                    // Có AiScore nhưng FinalScore = null
                    var submission = new ExamQuestionSubmission
                    {
                        Id = Guid.NewGuid(),
                        TicketId = ticket.Id,
                        ExamQuestionId = Guid.NewGuid(),
                        AudioR2Url = $"https://pub-r2.oralexam.fpt.edu.vn/exams/{shift.Id}/05_{student.StudentCode}.webm",
                        AudioHashSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        TranscriptWhisper = "Thí sinh 5 trả lời đang chờ thẩm định.",
                        AiScore = 7.8m,
                        FinalScore = null,
                        GradingStatus = "graded"
                    };
                    ticket.Submissions.Add(submission);
                    context.ExamQuestionSubmissions.Add(submission);
                }
                else if (fifthStudentHasScore)
                {
                    // Có FinalScore đầy đủ
                    var submission = new ExamQuestionSubmission
                    {
                        Id = Guid.NewGuid(),
                        TicketId = ticket.Id,
                        ExamQuestionId = Guid.NewGuid(),
                        AudioR2Url = $"https://pub-r2.oralexam.fpt.edu.vn/exams/{shift.Id}/05_{student.StudentCode}.webm",
                        AudioHashSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                        TranscriptWhisper = "Thí sinh 5 trả lời đầy đủ hoàn tất.",
                        AiScore = 9.0m,
                        FinalScore = 9.0m,
                        GradingStatus = "audited"
                    };
                    ticket.Submissions.Add(submission);
                    context.ExamQuestionSubmissions.Add(submission);
                }
                else
                {
                    // THIẾU ĐIỂM: FinalScore = null và AiScore = null
                    var submission = new ExamQuestionSubmission
                    {
                        Id = Guid.NewGuid(),
                        TicketId = ticket.Id,
                        ExamQuestionId = Guid.NewGuid(),
                        TranscriptWhisper = "Thí sinh 5 chưa có điểm chấm.",
                        AiScore = null,
                        FinalScore = null,
                        GradingStatus = "pending"
                    };
                    ticket.Submissions.Add(submission);
                    context.ExamQuestionSubmissions.Add(submission);
                }
            }

            tickets.Add(ticket);
            context.StudentExamTickets.Add(ticket);
        }

        await context.SaveChangesAsync();
        return (lecturer, shift, tickets);
    }

    #region 1. Ca biên PublishGradesCommandHandler

    [Fact(DisplayName = "ADV-M4-01: Ca thi KHÔNG CÓ vé thi nào -> BẮT BUỘC FAIL, trả về lỗi không tìm thấy vé")]
    public async Task ADV_M4_01_Empty_Shift_Must_Fail()
    {
        using var context = CreateDbContext();

        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "empty.lecturer@fpt.edu.vn",
            FullName = "GV Empty Shift",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var emptyShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Thi Rỗng Không Vé",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "scheduled"
        };

        context.Users.Add(lecturer);
        context.RealExamSessionShifts.Add(emptyShift);
        await context.SaveChangesAsync();

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(emptyShift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ca thi không có vé thi nào để công bố điểm.");
    }

    [Fact(DisplayName = "ADV-M4-02: Ca thi có 5 vé thi, 4 vé có FinalScore, 1 vé FinalScore = null -> BẮT BUỘC FAIL (Hard Gate 100%)")]
    public async Task ADV_M4_02_Five_Tickets_One_Missing_Score_Must_Fail()
    {
        using var context = CreateDbContext();
        var (lecturer, shift, tickets) = await SeedShiftWith5TicketsAsync(
            context,
            fifthStudentHasScore: false,
            fifthStudentHasOnlyAiScore: false);

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Không thể công bố điểm. Còn sinh viên chưa có điểm hoàn chỉnh.");

        // Kiểm tra tính bất biến: Toàn bộ 5 vé KHÔNG ĐƯỢC PHÉP bị khóa hay chuyển trạng thái Published
        var postCheckTickets = await context.StudentExamTickets
            .Where(t => t.ShiftId == shift.Id)
            .ToListAsync();

        postCheckTickets.Should().HaveCount(5);
        postCheckTickets.Should().OnlyContain(t => !t.IsLocked);
        postCheckTickets.Should().OnlyContain(t => t.Status != ExamTicketStatus.Published);
    }

    [Fact(DisplayName = "ADV-M4-03: Ca thi có 5 vé thi, 100% có FinalScore hợp lệ -> BẮT BUỘC PASS, toàn bộ 5 vé chuyển IsLocked = true và Published")]
    public async Task ADV_M4_03_Five_Tickets_All_Scored_Must_Pass_And_Lock_All()
    {
        using var context = CreateDbContext();
        var (lecturer, shift, tickets) = await SeedShiftWith5TicketsAsync(
            context,
            fifthStudentHasScore: true);

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ShiftId.Should().Be(shift.Id);
        result.Value.TotalPublished.Should().Be(5);
        result.Value.Message.Should().Contain("5/5 sinh viên");

        // Kiểm tra CSDL: Toàn bộ 5 vé chuyển IsLocked = true và Status = Published
        var publishedTickets = await context.StudentExamTickets
            .Where(t => t.ShiftId == shift.Id)
            .ToListAsync();

        publishedTickets.Should().HaveCount(5);
        publishedTickets.Should().OnlyContain(t => t.IsLocked == true);
        publishedTickets.Should().OnlyContain(t => t.Status == ExamTicketStatus.Published);

        var updatedShift = await context.RealExamSessionShifts.FindAsync(shift.Id);
        updatedShift!.Status.Should().Be("completed");
    }

    [Fact(DisplayName = "ADV-M4-04: Thử sửa đổi vé thi đã khóa -> OneWayLockInterceptor BẮT BUỘC ném InvalidOperationException")]
    public async Task ADV_M4_04_Modifying_Locked_Ticket_Must_Throw_InvalidOperationException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);

        var (lecturer, shift, tickets) = await SeedShiftWith5TicketsAsync(
            context,
            fifthStudentHasScore: true);

        // Công bố điểm để kích hoạt One-Way Lock
        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);
        var publishResult = await handler.Handle(command, CancellationToken.None);
        publishResult.IsSuccess.Should().BeTrue();

        // Tạo DbContext instance mới trên cùng Database để mô phỏng request HTTP tiếp theo
        using var nextContext = CreateDbContext(dbName);
        var lockedTicket = await nextContext.StudentExamTickets
            .FirstAsync(t => t.ShiftId == shift.Id && t.SeatNumber == 1);

        lockedTicket.IsLocked.Should().BeTrue();

        // Kịch bản tấn công 1: Cố tình thay đổi số ghế SeatNumber
        lockedTicket.SeatNumber = 99;

        Func<Task> act1 = async () => await nextContext.SaveChangesAsync();
        await act1.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permanently locked (One-Way Lock)*");

        // Kịch bản tấn công 2: Cố tình mở khóa IsLocked = false
        using var rollbackContext = CreateDbContext(dbName);
        var lockedTicket2 = await rollbackContext.StudentExamTickets
            .FirstAsync(t => t.ShiftId == shift.Id && t.SeatNumber == 1);

        lockedTicket2.IsLocked = false;
        Func<Task> act2 = async () => await rollbackContext.SaveChangesAsync();
        await act2.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permanently locked (One-Way Lock)*");

        // Kịch bản tấn công 3: Cố tình chuyển Status về SCHEDULED
        using var statusContext = CreateDbContext(dbName);
        var lockedTicket3 = await statusContext.StudentExamTickets
            .FirstAsync(t => t.ShiftId == shift.Id && t.SeatNumber == 1);

        lockedTicket3.Status = ExamTicketStatus.Scheduled;
        Func<Task> act3 = async () => await statusContext.SaveChangesAsync();
        await act3.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permanently locked (One-Way Lock)*");
    }

    [Fact(DisplayName = "ADV-M4-05: Thử XÓA vé thi đã khóa -> OneWayLockInterceptor BẮT BUỘC ném InvalidOperationException")]
    public async Task ADV_M4_05_Deleting_Locked_Ticket_Must_Throw_InvalidOperationException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);

        var (lecturer, shift, tickets) = await SeedShiftWith5TicketsAsync(
            context,
            fifthStudentHasScore: true);

        // Công bố điểm để khóa
        var handler = new PublishGradesCommandHandler(context);
        await handler.Handle(new PublishGradesCommand(shift.Id, lecturer.Id), CancellationToken.None);

        // Trong request mới, kẻ tấn công cố tình xóa vé thi
        using var deleteContext = CreateDbContext(dbName);
        var lockedTicket = await deleteContext.StudentExamTickets
            .FirstAsync(t => t.ShiftId == shift.Id && t.SeatNumber == 2);

        deleteContext.StudentExamTickets.Remove(lockedTicket);

        Func<Task> act = async () => await deleteContext.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permanently locked (One-Way Lock)*");
    }

    [Fact(DisplayName = "ADV-M4-06: Thử sửa đổi hoặc xóa LecturerAudit đã khóa -> OneWayLockInterceptor BẮT BUỘC ném InvalidOperationException")]
    public async Task ADV_M4_06_Modifying_Or_Deleting_Locked_LecturerAudit_Must_Throw()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);

        var (lecturer, shift, tickets) = await SeedShiftWith5TicketsAsync(
            context,
            fifthStudentHasScore: true);

        // Gán LecturerAudit cho vé thi số 1
        var ticket1 = tickets[0];
        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket1.Id,
            LecturerId = lecturer.Id,
            OriginalAiScore = 7.0m,
            AuditedScore = 8.5m,
            OverrideReason = "Thẩm định nâng điểm hợp lệ.",
            IsLocked = false
        };
        context.LecturerAudits.Add(audit);
        ticket1.LecturerAudit = audit;
        await context.SaveChangesAsync();

        // Công bố điểm -> audit.IsLocked chuyển thành true
        var handler = new PublishGradesCommandHandler(context);
        await handler.Handle(new PublishGradesCommand(shift.Id, lecturer.Id), CancellationToken.None);

        // Thử sửa điểm trong request mới
        using var editContext = CreateDbContext(dbName);
        var lockedAudit = await editContext.LecturerAudits.FindAsync(audit.Id);
        lockedAudit!.IsLocked.Should().BeTrue();

        lockedAudit.AuditedScore = 10.0m;
        Func<Task> actEdit = async () => await editContext.SaveChangesAsync();
        await actEdit.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permanently locked (One-Way Lock)*");

        // Thử xóa audit trong request mới
        using var delContext = CreateDbContext(dbName);
        var auditToDelete = await delContext.LecturerAudits.FindAsync(audit.Id);
        delContext.LecturerAudits.Remove(auditToDelete!);
        Func<Task> actDelete = async () => await delContext.SaveChangesAsync();
        await actDelete.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permanently locked (One-Way Lock)*");
    }

    [Fact(DisplayName = "ADV-M4-07: Vé thứ 5 có 2 câu hỏi (1 có điểm, 1 thiếu điểm) -> BẮT BUỘC FAIL (không cho công bố dở dang)")]
    public async Task ADV_M4_07_Partial_Submissions_In_Ticket_Must_Fail()
    {
        using var context = CreateDbContext();
        var (lecturer, shift, tickets) = await SeedShiftWith5TicketsAsync(
            context,
            fifthStudentHasPartialSubmissions: true);

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Không thể công bố điểm. Còn sinh viên chưa có điểm hoàn chỉnh.");
    }

    [Fact(DisplayName = "ADV-M4-08: Vé thứ 5 chỉ có AiScore (FinalScore = null) -> BẮT BUỘC PASS và tự động đồng bộ FinalScore = AiScore")]
    public async Task ADV_M4_08_Only_AiScore_Must_Pass_And_Sync_FinalScore()
    {
        using var context = CreateDbContext();
        var (lecturer, shift, tickets) = await SeedShiftWith5TicketsAsync(
            context,
            fifthStudentHasOnlyAiScore: true);

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPublished.Should().Be(5);

        // Kiểm tra vé số 5: FinalScore đã được đồng bộ từ AiScore (7.8m)
        var ticket5 = await context.StudentExamTickets
            .Include(t => t.Submissions)
            .FirstAsync(t => t.ShiftId == shift.Id && t.SeatNumber == 5);

        ticket5.IsLocked.Should().BeTrue();
        var sub5 = ticket5.Submissions.First();
        sub5.FinalScore.Should().Be(7.8m);
        sub5.AiScore.Should().Be(7.8m);
    }

    #endregion

    #region 2. Ca biên GetAuditEvidenceQueryHandler

    [Fact(DisplayName = "ADV-M4-09: Xác minh đầy đủ các trường Evidence Panel (Audio, Transcript, CoT, Scores, Doubt Guard)")]
    public async Task ADV_M4_09_Evidence_Panel_Must_Contain_All_Required_Fields()
    {
        using var context = CreateDbContext();

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Khảo Thí Evidence",
            RoomLab = "Lab 405",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow,
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.evidence.all@fpt.edu.vn",
            FullName = "Nguyễn Toàn Diện",
            StudentCode = "SE170888",
            Role = UserRole.Student,
            IsActive = true
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 3,
            IpAddress = "192.168.1.103",
            Status = "AUDITED",
            IsLocked = false
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AudioR2Url = "https://r2.fpt.edu.vn/exams/shift-test/03_SE170888.webm",
            AudioHashSha256 = "c5b2a096c4b22c7a72d3e34b9d03426e2e4ef12419c72f777b73c4d7ec4188b8",
            TranscriptWhisper = "Thí sinh trình bày rõ về Open/Closed Principle.",
            AiScore = 9.20m,
            FinalScore = 9.20m,
            GradingStatus = "audited"
        };

        var aiEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            ExamSubmissionId = submission.Id,
            TotalScore = 9.20m,
            ConfidenceScore = 0.95m,
            CotTrace = "Phân tích OCP: Sinh viên giải thích đúng việc mở rộng thay vì sửa đổi.",
            Feedback = "Rất tốt."
        };

        submission.AiEvaluations.Add(aiEval);
        ticket.Submissions.Add(submission);

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.AiEvaluations.Add(aiEval);
        await context.SaveChangesAsync();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var query = new GetAuditEvidenceQuery(shift.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var item = result.Value[0];
        item.TicketId.Should().Be(ticket.Id);
        item.StudentCode.Should().Be("SE170888");
        item.FullName.Should().Be("Nguyễn Toàn Diện");
        item.SeatNumber.Should().Be(3);
        item.Status.Should().Be("AUDITED");
        item.AudioR2Url.Should().Be("https://r2.fpt.edu.vn/exams/shift-test/03_SE170888.webm");
        item.AudioHashSha256.Should().Be("c5b2a096c4b22c7a72d3e34b9d03426e2e4ef12419c72f777b73c4d7ec4188b8");
        item.TranscriptWhisper.Should().Be("Thí sinh trình bày rõ về Open/Closed Principle.");
        item.CotTrace.Should().Contain("Phân tích OCP");
        item.AiScore.Should().Be(9.20m);
        item.FinalScore.Should().Be(9.20m);
        item.ConfidenceScore.Should().Be(0.95m);
        item.IsSuspicious.Should().BeFalse();
        item.SuspiciousReason.Should().BeNull();
        item.IsLocked.Should().BeFalse();
    }

    [Fact(DisplayName = "ADV-M4-10: Sinh viên có nhiều submissions -> Transcript và CoT được ghép nối bằng separator chuẩn")]
    public async Task ADV_M4_10_Multiple_Submissions_Must_Concatenate_Transcript_And_Cot()
    {
        using var context = CreateDbContext();

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Thi 2 Câu Hỏi",
            RoomLab = "Lab 402",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.multi@fpt.edu.vn",
            FullName = "Vũ Nhiều Câu",
            StudentCode = "SE170333",
            Role = UserRole.Student,
            IsActive = true
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = "AI_GRADED",
            IsLocked = false
        };

        var sub1 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Câu 1: Giải thích Singleton Pattern.",
            AiScore = 8.0m,
            FinalScore = 8.0m,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var eval1 = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            ExamSubmissionId = sub1.Id,
            AnswerType = "exam",
            ConfidenceScore = 0.85m,
            CotTrace = "CoT Câu 1: Đánh giá thread-safe Singleton.",
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        sub1.AiEvaluations.Add(eval1);

        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Câu 2: Giải thích Factory Method Pattern.",
            AiScore = 8.5m,
            FinalScore = 8.5m,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var eval2 = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            ExamSubmissionId = sub2.Id,
            AnswerType = "exam",
            ConfidenceScore = 0.88m,
            CotTrace = "CoT Câu 2: Đánh giá loose coupling Factory.",
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        sub2.AiEvaluations.Add(eval2);

        ticket.Submissions.Add(sub1);
        ticket.Submissions.Add(sub2);

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.AddRange(sub1, sub2);
        context.AiEvaluations.AddRange(eval1, eval2);
        await context.SaveChangesAsync();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var result = await handler.Handle(new GetAuditEvidenceQuery(shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value[0];

        // Transcript ghép nối bằng "\n\n---\n\n"
        item.TranscriptWhisper.Should().Contain("Câu 1: Giải thích Singleton Pattern.");
        item.TranscriptWhisper.Should().Contain("\n\n---\n\n");
        item.TranscriptWhisper.Should().Contain("Câu 2: Giải thích Factory Method Pattern.");

        // CoT trace ghép nối bằng "\n\n---\n\n"
        item.CotTrace.Should().Contain("CoT Câu 1");
        item.CotTrace.Should().Contain("\n\n---\n\n");
        item.CotTrace.Should().Contain("CoT Câu 2");
    }

    [Theory(DisplayName = "ADV-M4-11: Ranh giới AI Doubt Guard theo ConfidenceScore (0.69m vs 0.70m)")]
    [InlineData(0.69, true)]
    [InlineData(0.70, false)]
    public async Task ADV_M4_11_ConfidenceScore_Boundary_Check(double confidence, bool expectedSuspicious)
    {
        using var context = CreateDbContext();

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Test Confidence Boundary",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student.conf.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Thí Sinh Boundary Conf",
            StudentCode = "SE170111",
            Role = UserRole.Student,
            IsActive = true
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = "AI_GRADED",
            IsLocked = false
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 6.5m,
            FinalScore = 6.5m
        };

        var eval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            ExamSubmissionId = submission.Id,
            AnswerType = "exam",
            ConfidenceScore = (decimal)confidence,
            CotTrace = "Đánh giá test confidence."
        };

        submission.AiEvaluations.Add(eval);
        ticket.Submissions.Add(submission);

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.AiEvaluations.Add(eval);
        await context.SaveChangesAsync();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var result = await handler.Handle(new GetAuditEvidenceQuery(shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].IsSuspicious.Should().Be(expectedSuspicious);

        if (expectedSuspicious)
        {
            result.Value[0].SuspiciousReason.Should().Contain("< 70%");
        }
        else
        {
            result.Value[0].SuspiciousReason.Should().BeNull();
        }
    }

    [Theory(DisplayName = "ADV-M4-12: Ranh giới AI Doubt Guard theo độ lệch điểm Giảng viên vs AI (2.00m vs 1.99m)")]
    [InlineData(6.0, 8.0, true)]    // Lệch đúng 2.0 -> Suspicious
    [InlineData(6.0, 7.99, false)]  // Lệch 1.99 < 2.0 -> Not suspicious
    public async Task ADV_M4_12_Score_Delta_Boundary_Check(double originalAi, double audited, bool expectedSuspicious)
    {
        using var context = CreateDbContext();

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Test Delta Boundary",
            RoomLab = "Lab 102",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student.delta.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Thí Sinh Boundary Delta",
            StudentCode = "SE170222",
            Role = UserRole.Student,
            IsActive = true
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = "AUDITED",
            IsLocked = false
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = (decimal)originalAi,
            FinalScore = (decimal)audited
        };

        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = Guid.NewGuid(),
            OriginalAiScore = (decimal)originalAi,
            AuditedScore = (decimal)audited,
            OverrideReason = "Thẩm định điểm lệch.",
            IsLocked = false
        };

        ticket.Submissions.Add(submission);
        ticket.LecturerAudit = audit;

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.LecturerAudits.Add(audit);
        await context.SaveChangesAsync();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var result = await handler.Handle(new GetAuditEvidenceQuery(shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].IsSuspicious.Should().Be(expectedSuspicious);
    }

    [Fact(DisplayName = "ADV-M4-13: Vé thi không có submission (sinh viên vắng mặt) -> Handler an toàn, không ném NullRef")]
    public async Task ADV_M4_13_Empty_Submission_Ticket_Must_Not_Crash()
    {
        using var context = CreateDbContext();

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Có Thí Sinh Vắng",
            RoomLab = "Lab 103",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "absent.student@fpt.edu.vn",
            FullName = "Sinh Viên Vắng Mặt",
            StudentCode = "SE170000",
            Role = UserRole.Student,
            IsActive = true
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = "SCHEDULED",
            IsLocked = false
            // Submissions rỗng
        };

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var result = await handler.Handle(new GetAuditEvidenceQuery(shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var item = result.Value[0];
        item.AudioR2Url.Should().BeNull();
        item.TranscriptWhisper.Should().BeNull();
        item.CotTrace.Should().BeNull();
        item.AiScore.Should().BeNull();
        item.FinalScore.Should().BeNull();
        item.IsSuspicious.Should().BeFalse();
    }

    [Fact(DisplayName = "ADV-M4-14: Tra cứu ca thi không tồn tại -> Handler trả về Failure")]
    public async Task ADV_M4_14_Nonexistent_Shift_Must_Return_Failure()
    {
        using var context = CreateDbContext();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var result = await handler.Handle(new GetAuditEvidenceQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ca thi không tồn tại trong hệ thống.");
    }

    #endregion

    #region 3. Rà soát Controller Security & Error Handling

    [Fact(DisplayName = "ADV-M4-15: OfficialExamsController.PublishGrades với request null & no auth -> trả về 422 an toàn, không 500")]
    public async Task ADV_M4_15_Controller_PublishGrades_Null_Body_Returns_422()
    {
        var mockSender = new Mock<ISender>();
        mockSender
            .Setup(s => s.Send(It.IsAny<PublishGradesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<PublishGradesResponseDto>.Failure("ID giảng viên công bố điểm không được để trống."));

        var controller = new OfficialExamsController(mockSender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        var response = await controller.PublishGrades(Guid.NewGuid(), null);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
        var objResult = response as UnprocessableEntityObjectResult;
        objResult!.StatusCode.Should().Be(422);
    }

    [Fact(DisplayName = "ADV-M4-16: OfficialExamsController.GetAuditEvidence với ca thi không tồn tại -> trả về 404 RFC 7807")]
    public async Task ADV_M4_16_Controller_GetAuditEvidence_NotFound_Returns_404()
    {
        var mockSender = new Mock<ISender>();
        mockSender
            .Setup(s => s.Send(It.IsAny<GetAuditEvidenceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<List<AuditEvidenceItemDto>>.Failure("Ca thi không tồn tại trong hệ thống."));

        var controller = new OfficialExamsController(mockSender.Object);

        var response = await controller.GetAuditEvidence(Guid.NewGuid());

        response.Should().BeOfType<NotFoundObjectResult>();
        var notFound = response as NotFoundObjectResult;
        notFound!.StatusCode.Should().Be(404);
    }

    #endregion
}
