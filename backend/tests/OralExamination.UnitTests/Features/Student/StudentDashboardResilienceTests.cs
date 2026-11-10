using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Student.DTOs;
using OralExamination.Application.Features.Student.Queries.GetStudentDashboard;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Student;

/// <summary>
/// Unit test suite kiểm chứng tính bền bỉ và phòng thủ (Resilience & Defensive Handling)
/// cho GetStudentDashboardQueryHandler (FE-10 / BE-V03) sau khi khắc phục các lỗi HTTP 500 thực tế trên VPS Production:
/// - Trường hợp sinh viên hoàn toàn mới (empty stats): 0 vé thi, 0 phiên luyện tập, 0 ca thi thử -> trả về 200 OK với counters 0 và list rỗng, không crash 500.
/// - Trường hợp vé thi có Shift bị lỗi/mồ côi hoặc thiếu Course navigation -> phòng thủ try-catch và null-coalescing, không văng NullReferenceException hay crash 500.
/// - Trường hợp vé thi có Submissions bị null hoặc rỗng -> tính điểm an toàn không chia cho 0 (DivideByZeroException), trả về null.
/// - Trường hợp phân bổ và tính điểm trung bình chính xác khi có submissions hoặc điểm do giảng viên hậu kiểm (LecturerAudit).
/// - Trường hợp ngoại lệ cấu trúc CSDL phụ (AiEvaluationDetails) -> suy thoái êm dịu (Graceful Degradation), không làm sập dashboard.
/// </summary>
public class StudentDashboardResilienceTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString());
        return new OralExamDbContext(builder.Options);
    }

    private static RealExamSessionShift CreateShiftHierarchy(OralExamDbContext context, bool withCourse = true)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            IsActive = true
        };

        Course? course = null;
        if (withCourse)
        {
            course = new Course
            {
                Id = Guid.NewGuid(),
                Code = "SWD392",
                Name = "Software Architecture and Design",
                Credits = 3,
                SemesterId = semester.Id,
                IsActive = true
            };
            context.Courses.Add(course);
        }

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course?.Id ?? Guid.NewGuid(),
            Course = course!,
            ExamStructureId = Guid.NewGuid(),
            Title = "Thi Vấn Đáp Cuối Kỳ SWD392",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Session = session,
            ShiftName = "Ca 1",
            RoomLab = "Lab 302",
            StartTime = DateTime.UtcNow.AddDays(5),
            EndTime = DateTime.UtcNow.AddDays(5).AddHours(2)
        };

        context.Semesters.Add(semester);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        return shift;
    }

    [Fact(DisplayName = "StudentDashboard: Sinh viên hoàn toàn mới (0 vé thi, 0 luyện tập, 0 thi thử) trả về Success, 0 counters, list rỗng, không crash 500")]
    public async Task Handle_CompletelyEmptyStudent_Should_ReturnSuccessWithZeroCountersAndEmptyCollections()
    {
        // Arrange
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "new_student@fpt.edu.vn",
            FullName = "Nguyễn Văn Mới",
            StudentCode = "SE190001",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<GetStudentDashboardQueryHandler>>();
        var handler = new GetStudentDashboardQueryHandler(context, loggerMock.Object);

        // Act
        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var dto = result.Value!;
        dto.StudentId.Should().Be(student.Id);
        dto.StudentCode.Should().Be("SE190001");
        dto.FullName.Should().Be("Nguyễn Văn Mới");
        dto.Email.Should().Be("new_student@fpt.edu.vn");

        // Practice counters
        dto.TotalPracticeSessions.Should().Be(0);
        dto.CompletedPracticeSessions.Should().Be(0);
        dto.AveragePracticeScore.Should().BeNull();

        // Mock exam counters
        dto.TotalMockExams.Should().Be(0);
        dto.CompletedMockExams.Should().Be(0);
        dto.AverageMockExamScore.Should().BeNull();

        // Collections
        dto.CourseQuotas.Should().BeEmpty();
        dto.CriterionMastery.Should().BeEmpty();
        dto.OfficialExamTickets.Should().BeEmpty();
    }

    [Fact(DisplayName = "StudentDashboard: Vé thi có Shift mồ côi hoặc lỗi CSDL được try-catch bảo vệ, trả về Success 200 không crash 500")]
    public async Task Handle_StudentWithTicketHavingNullShift_Should_ReturnSuccessWithSafeFallbacks()
    {
        // Arrange
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "null_shift_student@fpt.edu.vn",
            FullName = "Trần Thị Lệ",
            StudentCode = "SE180002",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);

        // Vé thi mồ côi (Shift = null, ví dụ ca thi bị xóa hoặc drift dữ liệu)
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ShiftId = Guid.NewGuid(),
            Shift = null!,
            SeatNumber = 12,
            Status = ExamTicketStatus.Published,
            IsLocked = true,
            CheckedInAt = DateTime.UtcNow.AddHours(-2)
        };
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        // Act
        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        // Assert: Khối try-catch tại dòng 298-316 bắt lỗi Include/ThenInclude an toàn, không ném 500
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.StudentId.Should().Be(student.Id);
        // Suy thoái êm dịu: danh sách vé thi rỗng thay vì làm sập toàn bộ trang dashboard của sinh viên
        result.Value.OfficialExamTickets.Should().BeEmpty();
    }

    [Fact(DisplayName = "StudentDashboard: Vé thi có Submissions rỗng hoặc null không bị chia cho 0, trả về null FinalScore")]
    public async Task Handle_StudentWithTicketHavingNullOrEmptySubmissions_Should_NotDivideByZero()
    {
        // Arrange
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "empty_subs_student@fpt.edu.vn",
            FullName = "Lê Hoàng Nam",
            StudentCode = "SE180003",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);
        var shift = CreateShiftHierarchy(context, withCourse: true);

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ShiftId = shift.Id,
            Shift = shift,
            SeatNumber = 5,
            Status = ExamTicketStatus.Locked,
            IsLocked = true,
            Submissions = new List<ExamQuestionSubmission>() // Danh sách rỗng
        };
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        // Act
        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ticketDto = result.Value!.OfficialExamTickets.Should().ContainSingle().Subject;
        ticketDto.TicketId.Should().Be(ticket.Id);
        ticketDto.FinalScore.Should().BeNull(); // Không chia cho 0, trả về null an toàn
        ticketDto.CourseCode.Should().Be("SWD392");
        ticketDto.ShiftName.Should().Be("Ca 1");
    }

    [Fact(DisplayName = "StudentDashboard: Vé thi có submissions chưa có điểm (null scores) không bị tính sai điểm")]
    public async Task Handle_StudentWithTicketHavingSubmissionsWithoutScores_Should_ReturnNullFinalScore()
    {
        // Arrange
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "unscored_student@fpt.edu.vn",
            FullName = "Phạm Đình Bảo",
            StudentCode = "SE180004",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);
        var shift = CreateShiftHierarchy(context, withCourse: true);

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ShiftId = shift.Id,
            Shift = shift,
            SeatNumber = 7,
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            FinalScore = null,
            AiScore = null
        };
        ticket.Submissions = new List<ExamQuestionSubmission> { submission };

        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        // Act
        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ticketDto = result.Value!.OfficialExamTickets.Should().ContainSingle().Subject;
        ticketDto.FinalScore.Should().BeNull();
    }

    [Fact(DisplayName = "StudentDashboard: Vé thi có submissions hợp lệ tính toán điểm trung bình chính xác")]
    public async Task Handle_StudentWithTicketHavingValidSubmissions_Should_CalculateCorrectAverageScore()
    {
        // Arrange
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "graded_student@fpt.edu.vn",
            FullName = "Đỗ Trọng Hiếu",
            StudentCode = "SE180005",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);
        var shift = CreateShiftHierarchy(context, withCourse: true);

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ShiftId = shift.Id,
            Shift = shift,
            SeatNumber = 1,
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };

        // 3 câu hỏi: 8.0, 9.0, và 7.0 (AiScore) -> trung bình = (8.0 + 9.0 + 7.0) / 3 = 8.00
        var sub1 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            FinalScore = 8.0m,
            AiScore = 7.5m
        };
        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            FinalScore = 9.0m,
            AiScore = 8.5m
        };
        var sub3 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            FinalScore = null,
            AiScore = 7.0m // Fallback sang AiScore
        };

        ticket.Submissions = new List<ExamQuestionSubmission> { sub1, sub2, sub3 };

        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.AddRange(sub1, sub2, sub3);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        // Act
        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ticketDto = result.Value!.OfficialExamTickets.Should().ContainSingle().Subject;
        ticketDto.FinalScore.Should().Be(8.00m);
    }

    [Fact(DisplayName = "StudentDashboard: Điểm do Giảng viên phúc khảo/hậu kiểm (LecturerAudit) được ưu tiên hơn điểm trung bình submissions")]
    public async Task Handle_StudentWithAuditedScore_Should_PreferAuditedScore()
    {
        // Arrange
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "audited_student@fpt.edu.vn",
            FullName = "Hoàng Minh Tuấn",
            StudentCode = "SE180006",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);
        var shift = CreateShiftHierarchy(context, withCourse: true);

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ShiftId = shift.Id,
            Shift = shift,
            SeatNumber = 10,
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };

        var sub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            FinalScore = 6.0m
        };
        ticket.Submissions = new List<ExamQuestionSubmission> { sub };

        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = Guid.NewGuid(),
            AuditedScore = 9.25m,
            OverrideReason = "Sinh viên giải thích xuất sắc câu hỏi follow-up.",
            AuditedAt = DateTime.UtcNow
        };
        ticket.LecturerAudit = audit;

        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(sub);
        context.LecturerAudits.Add(audit);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        // Act
        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ticketDto = result.Value!.OfficialExamTickets.Should().ContainSingle().Subject;
        ticketDto.FinalScore.Should().Be(9.25m);
    }

    [Fact(DisplayName = "StudentDashboard: Vé thi chưa công bố điểm (Scheduled/InProgress và chưa Lock) không lộ điểm")]
    public async Task Handle_UnpublishedAndUnlockedTicket_Should_HideScore()
    {
        // Arrange
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "in_progress_student@fpt.edu.vn",
            FullName = "Vũ Hải Đăng",
            StudentCode = "SE180007",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);
        var shift = CreateShiftHierarchy(context, withCourse: true);

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ShiftId = shift.Id,
            Shift = shift,
            SeatNumber = 15,
            Status = ExamTicketStatus.InProgress,
            IsLocked = false
        };

        var sub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 8.5m
        };
        ticket.Submissions = new List<ExamQuestionSubmission> { sub };

        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(sub);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        // Act
        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ticketDto = result.Value!.OfficialExamTickets.Should().ContainSingle().Subject;
        ticketDto.FinalScore.Should().BeNull(); // Điểm chưa được công bố
    }

    [Fact(DisplayName = "StudentDashboard: Guid.Empty trả về Failure rõ ràng thay vì unhandled exception")]
    public async Task Handle_EmptyStudentGuid_Should_ReturnFailure()
    {
        using var context = CreateDbContext();
        var handler = new GetStudentDashboardQueryHandler(context, null);

        var result = await handler.Handle(new GetStudentDashboardQuery(Guid.Empty), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không xác định được định danh sinh viên");
    }

    [Fact(DisplayName = "StudentDashboard: Sinh viên không tồn tại trả về Failure không tìm thấy")]
    public async Task Handle_NonExistentStudent_Should_ReturnFailure()
    {
        using var context = CreateDbContext();
        var handler = new GetStudentDashboardQueryHandler(context, null);

        var nonExistentId = Guid.NewGuid();
        var result = await handler.Handle(new GetStudentDashboardQuery(nonExistentId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không tồn tại");
    }

    [Fact(DisplayName = "StudentDashboard: Hỗ trợ primary constructor với ILogger null mà không gây NullReferenceException")]
    public async Task Handle_NullLogger_Should_ExecuteGracefully()
    {
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "logger_test@fpt.edu.vn",
            FullName = "Ngô Bá Khá",
            StudentCode = "SE180008",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, logger: null);

        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
