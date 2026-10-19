using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Features.Student.Commands.AcknowledgeOfficialGrade;
using OralExamination.Application.Features.Student.Queries.GetStudentDashboard;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace OralExamination.UnitTests.Features.Student;

public class StudentDashboardAndAcknowledgeTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null, bool withLockInterceptor = false)
    {
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString());

        if (withLockInterceptor)
        {
            builder.AddInterceptors(new OneWayLockInterceptor());
        }

        return new OralExamDbContext(builder.Options);
    }

    [Fact(DisplayName = "BE-V03: GetStudentDashboardQueryHandler tổng hợp đầy đủ Practice, MockExam, Quota, Mastery và OfficialExam")]
    public async Task GetStudentDashboard_Should_Return_Complete_Aggregated_Stats()
    {
        using var context = CreateDbContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = today.AddDays(-30),
            EndDate = today.AddDays(60),
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "SW Architecture and Design",
            Credits = 3,
            SemesterId = semester.Id,
            MaxMockExamsPerDay = 4,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "se170888@fpt.edu.vn",
            FullName = "Lê Vũ Hoàng",
            StudentCode = "SE170888",
            Role = UserRole.Student,
            IsActive = true
        };

        var cls = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1701",
            CourseId = course.Id,
            Course = course,
            SemesterId = semester.Id,
            IsActive = true
        };

        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            StudentId = student.Id,
            Student = student
        };

        var quota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            QuotaDate = today,
            UsedCount = 1
        };

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric SWD392",
            TotalMaxScore = 10.0m
        };

        var criterion = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "CLO1 - Design Patterns",
            BloomLevel = "Apply",
            MaxScore = 5.0m,
            Weight = 0.5m
        };

        var practiceSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            Status = "completed"
        };

        var practiceAnswer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = practiceSession.Id,
            QuestionId = Guid.NewGuid(),
            StudentId = student.Id,
            AnswerText = "Strategy pattern giúp hoán đổi thuật toán lúc runtime.",
            IsFollowUp = false,
            Status = "graded"
        };

        var eval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "practice",
            PracticeAnswerId = practiceAnswer.Id,
            PracticeAnswer = practiceAnswer,
            TotalScore = 8.0m
        };

        var evalDetail = new AiEvaluationDetail
        {
            Id = Guid.NewGuid(),
            EvaluationId = eval.Id,
            Evaluation = eval,
            CriterionId = criterion.Id,
            Criterion = criterion,
            Score = 4.0m
        };

        var mockSession = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            Status = "completed",
            TotalScore = 7.5m,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        var officialSession = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Course = course,
            ExamStructureId = Guid.NewGuid(),
            Title = "Thi Vấn Đáp Cuối Kỳ SWD392",
            ExamDate = today.AddDays(5)
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = officialSession.Id,
            Session = officialSession,
            ShiftName = "Ca 2",
            RoomLab = "Lab 302",
            StartTime = DateTime.UtcNow.AddDays(5),
            EndTime = DateTime.UtcNow.AddDays(5).AddHours(2)
        };

        var unPublishedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            Shift = shift,
            StudentId = student.Id,
            SeatNumber = 12,
            Status = ExamTicketStatus.Submitted,
            IsLocked = false
        };
        unPublishedTicket.Submissions.Add(new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = unPublishedTicket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 9.0m,
            FinalScore = 9.0m
        });

        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Users.Add(student);
        context.Classes.Add(cls);
        context.ClassEnrollments.Add(enrollment);
        context.MockExamQuotas.Add(quota);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.Add(criterion);
        context.PracticeSessions.Add(practiceSession);
        context.PracticeAnswers.Add(practiceAnswer);
        context.AiEvaluations.Add(eval);
        context.AiEvaluationDetails.Add(evalDetail);
        context.MockExamSessions.Add(mockSession);
        context.OfficialExamSessions.Add(officialSession);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(unPublishedTicket);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context);
        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentCode.Should().Be("SE170888");
        result.Value.TotalPracticeSessions.Should().Be(1);
        result.Value.CompletedPracticeSessions.Should().Be(1);
        result.Value.AveragePracticeScore.Should().Be(8.00m);
        result.Value.TotalMockExams.Should().Be(1);
        result.Value.CompletedMockExams.Should().Be(1);
        result.Value.AverageMockExamScore.Should().Be(7.50m);

        result.Value.CourseQuotas.Should().ContainSingle();
        result.Value.CourseQuotas[0].MaxMockExamsPerDay.Should().Be(4);
        result.Value.CourseQuotas[0].UsedMockExamsToday.Should().Be(1);
        result.Value.CourseQuotas[0].RemainingMockExamsToday.Should().Be(3);

        result.Value.CriterionMastery.Should().ContainSingle();
        result.Value.CriterionMastery[0].CriterionName.Should().Be("CLO1 - Design Patterns");
        result.Value.CriterionMastery[0].AverageScorePercentage.Should().Be(80.00m); // 4.0 / 5.0 = 80%

        // Điểm ca thi thật chưa công bố (SUBMITTED, IsLocked = false) tuyệt đối không bị lộ trên Dashboard
        result.Value.OfficialExamTickets.Should().ContainSingle();
        result.Value.OfficialExamTickets[0].FinalScore.Should().BeNull();
    }

    [Fact(DisplayName = "MF-04 AcknowledgeOfficialGrade: Cho phép sinh viên xác nhận điểm (ACKNOWLEDGED) trên vé thi đã khóa One-Way Lock")]
    public async Task AcknowledgeOfficialGrade_Should_Succeed_On_Locked_Published_Ticket()
    {
        var dbName = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        using (var seedContext = CreateDbContext(dbName, withLockInterceptor: false))
        {
            seedContext.StudentExamTickets.Add(new StudentExamTicket
            {
                Id = ticketId,
                ShiftId = Guid.NewGuid(),
                StudentId = studentId,
                SeatNumber = 5,
                Status = ExamTicketStatus.Published,
                IsLocked = true,
                StudentAcknowledgementStatus = "PENDING"
            });
            await seedContext.SaveChangesAsync();
        }

        using var activeContext = CreateDbContext(dbName, withLockInterceptor: true);
        var handler = new AcknowledgeOfficialGradeCommandHandler(activeContext);
        var result = await handler.Handle(new AcknowledgeOfficialGradeCommand(ticketId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StudentAcknowledgementStatus.Should().Be("ACKNOWLEDGED");

        var updatedTicket = await activeContext.StudentExamTickets.FindAsync(ticketId);
        updatedTicket!.StudentAcknowledgementStatus.Should().Be("ACKNOWLEDGED");
        updatedTicket.IsLocked.Should().BeTrue();
    }

    [Fact(DisplayName = "MF-04 AcknowledgeOfficialGrade: Chặn xác nhận điểm khi vé chưa công bố hoặc không thuộc về sinh viên")]
    public async Task AcknowledgeOfficialGrade_Should_Reject_When_Not_Published_Or_Wrong_Student()
    {
        using var context = CreateDbContext(withLockInterceptor: true);
        var studentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        context.StudentExamTickets.Add(new StudentExamTicket
        {
            Id = ticketId,
            ShiftId = Guid.NewGuid(),
            StudentId = studentId,
            SeatNumber = 2,
            Status = ExamTicketStatus.Submitted,
            IsLocked = false
        });
        await context.SaveChangesAsync();

        var handler = new AcknowledgeOfficialGradeCommandHandler(context);

        var wrongStudentResult = await handler.Handle(
            new AcknowledgeOfficialGradeCommand(ticketId, Guid.NewGuid()),
            CancellationToken.None);
        wrongStudentResult.IsSuccess.Should().BeFalse();
        wrongStudentResult.Error.Should().Contain("không có quyền");

        var unpublishedResult = await handler.Handle(
            new AcknowledgeOfficialGradeCommand(ticketId, studentId),
            CancellationToken.None);
        unpublishedResult.IsSuccess.Should().BeFalse();
        unpublishedResult.Error.Should().Contain("sau khi giảng viên đã công bố điểm");
    }

    [Fact(DisplayName = "BE-V03 StudentController: Trích xuất StudentId từ Claims và trả về 200 OK")]
    public async Task StudentController_Should_Return_200_When_Authenticated()
    {
        var senderMock = new Mock<ISender>();
        var controller = new StudentController(senderMock.Object);
        var studentId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, studentId.ToString()),
            new(ClaimTypes.Role, "student")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };

        senderMock
            .Setup(s => s.Send(It.Is<GetStudentDashboardQuery>(q => q.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<OralExamination.Application.Features.Student.DTOs.StudentDashboardDto>.Success(
                new OralExamination.Application.Features.Student.DTOs.StudentDashboardDto
                {
                    StudentId = studentId,
                    StudentCode = "SE170123",
                    FullName = "Nguyễn Quang Thành"
                }));

        var response = await controller.GetDashboard(null, CancellationToken.None);
        response.Should().BeOfType<OkObjectResult>();
    }
}
