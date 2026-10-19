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
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Commands.CompletePracticeSession;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Application.Features.Practice.Queries.GetPracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class PracticeBolaAdversarialTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly PracticeController _controller;

    public PracticeBolaAdversarialTests()
    {
        _controller = new PracticeController(_senderMock.Object);
    }

    private void SetUserContext(Guid userId, string role = "student", Dictionary<string, string>? customHeaders = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        if (customHeaders != null)
        {
            foreach (var kvp in customHeaders)
            {
                httpContext.Request.Headers[kvp.Key] = kvp.Value;
            }
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #region 1. StartSession BOLA & Parameter Tampering Challenges

    [Fact(DisplayName = "CHALLENGE-BOLA-01. StartSession: Student A cố tình truyền StudentId của Student B trong body -> Bắt buộc ghim vào Student A")]
    public async Task StartSession_TamperedStudentId_Should_Pin_To_Authenticated_Student()
    {
        // Arrange
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetUserContext(studentAId, role: "student");

        var request = new StartPracticeSessionRequest(
            StudentId: studentBId, // Tampered Student ID
            CourseId: courseId,
            Difficulty: "per_question",
            QuestionCount: 5,
            IsFullSession: false,
            Topic: null
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<StartPracticeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartPracticeSessionResponse>.Success(new StartPracticeSessionResponse(
                Guid.NewGuid(), 60, new List<PracticeQuestionDto>())));

        // Act
        var result = await _controller.StartSession(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _senderMock.Verify(s => s.Send(
            It.Is<StartPracticeSessionCommand>(c => c.StudentId == studentAId),
            It.IsAny<CancellationToken>()), Times.Once,
            "Controller PHẢI phớt lờ studentId bị giả mạo trong body và ghim vào token claim của Student A.");
    }

    [Theory(DisplayName = "CHALLENGE-BOLA-02. StartSession: Elevated roles (lecturer, department_head, admin) có quyền tạo phiên hộ cho Student B")]
    [InlineData("lecturer")]
    [InlineData("department_head")]
    [InlineData("admin")]
    public async Task StartSession_ElevatedRoles_Should_Honor_Requested_StudentId(string elevatedRole)
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var targetStudentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetUserContext(callerId, role: elevatedRole);

        var request = new StartPracticeSessionRequest(
            StudentId: targetStudentId,
            CourseId: courseId,
            Difficulty: "per_question",
            QuestionCount: 5,
            IsFullSession: false,
            Topic: null
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<StartPracticeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartPracticeSessionResponse>.Success(new StartPracticeSessionResponse(
                Guid.NewGuid(), 60, new List<PracticeQuestionDto>())));

        // Act
        var result = await _controller.StartSession(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _senderMock.Verify(s => s.Send(
            It.Is<StartPracticeSessionCommand>(c => c.StudentId == targetStudentId),
            It.IsAny<CancellationToken>()), Times.Once,
            $"Cán bộ vai trò {elevatedRole} có quyền tạo phiên chỉ định cho sinh viên mục tiêu.");
    }

    #endregion

    #region 2. GetSession BOLA & Cross-Student Inspection Challenges

    [Fact(DisplayName = "CHALLENGE-BOLA-03. GetSession: Student A truy cập SessionId của Student B -> Trả về 403 Forbidden RFC 7807")]
    public async Task GetSession_CrossStudentAccess_Should_Return_403_Forbidden()
    {
        // Arrange
        var studentAId = Guid.NewGuid();
        var sessionBId = Guid.NewGuid();
        SetUserContext(studentAId, role: "student");

        _senderMock
            .Setup(s => s.Send(It.Is<GetPracticeSessionQuery>(q =>
                q.SessionId == sessionBId &&
                q.RequestingUserId == studentAId &&
                q.IsElevatedRole == false), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PracticeSessionDetailDto>.Failure("Bạn không có quyền truy cập phiên luyện tập này."));

        // Act
        var actionResult = await _controller.GetSession(sessionBId);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Forbidden");
        problem.Detail.Should().Contain("không có quyền");
    }

    [Theory(DisplayName = "CHALLENGE-BOLA-04. GetSession: Elevated roles (lecturer, department_head, admin) xem được chi tiết phiên của sinh viên")]
    [InlineData("lecturer")]
    [InlineData("department_head")]
    [InlineData("admin")]
    public async Task GetSession_ElevatedRoles_Should_Have_Full_Inspection_Access(string elevatedRole)
    {
        // Arrange
        var elevatedUserId = Guid.NewGuid();
        var sessionBId = Guid.NewGuid();
        SetUserContext(elevatedUserId, role: elevatedRole);

        var sessionDetail = new PracticeSessionDetailDto
        {
            SessionId = sessionBId,
            CourseCode = "SWD392",
            StudentName = "Sinh viên B",
            PracticeMode = "per_question",
            Status = "in_progress"
        };

        _senderMock
            .Setup(s => s.Send(It.Is<GetPracticeSessionQuery>(q =>
                q.SessionId == sessionBId &&
                q.RequestingUserId == elevatedUserId &&
                q.IsElevatedRole == true), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PracticeSessionDetailDto>.Success(sessionDetail));

        // Act
        var actionResult = await _controller.GetSession(sessionBId);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(sessionDetail);
    }

    #endregion

    #region 3. GetNextQuestion BOLA & Cross-Student Advance Challenges

    [Fact(DisplayName = "CHALLENGE-BOLA-05. GetNextQuestion: Student A cố tình truyền query param ?studentId={StudentB} -> Bắt buộc ghim vào Student A")]
    public async Task GetNextQuestion_TamperedStudentIdQuery_Should_Pin_To_Authenticated_Student()
    {
        // Arrange
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var sessionBId = Guid.NewGuid();
        SetUserContext(studentAId, role: "student");

        _senderMock
            .Setup(s => s.Send(It.IsAny<NextQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NextQuestionResponse>.Failure("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này."));

        // Act
        var actionResult = await _controller.GetNextQuestion(sessionBId, studentId: studentBId);

        // Assert
        _senderMock.Verify(s => s.Send(
            It.Is<NextQuestionCommand>(c => c.SessionId == sessionBId && c.StudentId == studentAId),
            It.IsAny<CancellationToken>()), Times.Once,
            "NextQuestionCommand PHẢI nhận StudentId là Student A (từ token claim), không cho phép Student A đóng giả Student B.");
    }

    #endregion

    #region 4. SubmitAnswer BOLA & Cross-Student Submission Challenges

    [Fact(DisplayName = "CHALLENGE-BOLA-06. SubmitAnswer: Student A cố tình nộp câu trả lời vào session của Student B với StudentId=StudentB trong body -> Bắt buộc ghim vào Student A")]
    public async Task SubmitAnswer_TamperedStudentId_Should_Pin_To_Authenticated_Student()
    {
        // Arrange
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var sessionBId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        SetUserContext(studentAId, role: "student");

        var request = new SubmitPracticeAnswerRequest(
            QuestionId: questionId,
            StudentId: studentBId, // Tampered Student ID in body
            AnswerText: "Câu trả lời bị giả mạo",
            IsFollowUp: false,
            ParentAnswerId: null
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<SubmitPracticeAnswerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));

        // Act
        var actionResult = await _controller.SubmitAnswer(sessionBId, request);

        // Assert
        actionResult.Should().BeOfType<AcceptedResult>();
        _senderMock.Verify(s => s.Send(
            It.Is<SubmitPracticeAnswerCommand>(c =>
                c.SessionId == sessionBId &&
                c.QuestionId == questionId &&
                c.StudentId == studentAId && // MUST be Student A!
                c.AnswerText == "Câu trả lời bị giả mạo"),
            It.IsAny<CancellationToken>()), Times.Once,
            "SubmitAnswerCommand PHẢI bị ép StudentId = Student A để tầng Application từ chối khi session thuộc về Student B.");
    }

    #endregion

    #region 5. SubmitBatch BOLA & Cross-Student Submission Challenges

    [Fact(DisplayName = "CHALLENGE-BOLA-07. SubmitBatch: Student A cố tình nộp batch câu trả lời với StudentId=StudentB trong body -> Bắt buộc ghim vào Student A")]
    public async Task SubmitBatch_TamperedStudentId_Should_Pin_To_Authenticated_Student()
    {
        // Arrange
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var sessionBId = Guid.NewGuid();
        SetUserContext(studentAId, role: "student");

        var request = new SubmitPracticeBatchRequest(
            StudentId: studentBId, // Tampered Student ID in body
            Answers: new List<OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch.BatchAnswerDto>
            {
                new(Guid.NewGuid(), "Câu trả lời 1", false, null),
                new(Guid.NewGuid(), "Câu trả lời 2", false, null)
            }
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch.SubmitPracticeBatchCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Success(Unit.Value));

        // Act
        var actionResult = await _controller.SubmitBatch(sessionBId, request);

        // Assert
        actionResult.Should().BeOfType<AcceptedResult>();
        _senderMock.Verify(s => s.Send(
            It.Is<OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch.SubmitPracticeBatchCommand>(c =>
                c.SessionId == sessionBId &&
                c.StudentId == studentAId), // MUST be Student A!
            It.IsAny<CancellationToken>()), Times.Once,
            "SubmitPracticeBatchCommand PHẢI bị ép StudentId = Student A.");
    }

    #endregion

    #region 6. CompleteSession BOLA & Cross-Student Completion Challenges

    [Fact(DisplayName = "CHALLENGE-BOLA-08. CompleteSession: Student A cố tình hoàn tất phiên của Student B với query param ?studentId={StudentB} -> Bắt buộc ghim vào Student A")]
    public async Task CompleteSession_TamperedStudentIdQuery_Should_Pin_To_Authenticated_Student()
    {
        // Arrange
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var sessionBId = Guid.NewGuid();
        SetUserContext(studentAId, role: "student");

        _senderMock
            .Setup(s => s.Send(It.IsAny<CompletePracticeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var actionResult = await _controller.CompleteSession(sessionBId, studentId: studentBId);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        _senderMock.Verify(s => s.Send(
            It.Is<CompletePracticeSessionCommand>(c =>
                c.SessionId == sessionBId &&
                c.StudentId == studentAId), // MUST be Student A!
            It.IsAny<CancellationToken>()), Times.Once,
            "CompletePracticeSessionCommand PHẢI bị ép StudentId = Student A.");
    }

    #endregion

    #region 7. GetStudentHistory BOLA & Cross-Student History Inspection Challenges

    [Fact(DisplayName = "CHALLENGE-BOLA-09. GetStudentHistory: Student A cố tình xem lịch sử của Student B với query param ?studentId={StudentB} -> Bắt buộc ghim vào Student A")]
    public async Task GetStudentHistory_TamperedStudentIdQuery_Should_Pin_To_Authenticated_Student()
    {
        // Arrange
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        SetUserContext(studentAId, role: "student");

        _senderMock
            .Setup(s => s.Send(It.IsAny<GetStudentPracticeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PracticeHistoryItemDto>>.Success(new List<PracticeHistoryItemDto>()));

        // Act
        var actionResult = await _controller.GetStudentHistory(studentId: studentBId);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        _senderMock.Verify(s => s.Send(
            It.Is<GetStudentPracticeHistoryQuery>(q => q.StudentId == studentAId), // MUST be Student A!
            It.IsAny<CancellationToken>()), Times.Once,
            "GetStudentPracticeHistoryQuery PHẢI bị ép StudentId = Student A khi caller là sinh viên.");
    }

    [Theory(DisplayName = "CHALLENGE-BOLA-10. GetStudentHistory: Elevated roles (lecturer, department_head, admin) có quyền tra cứu lịch sử của bất kỳ sinh viên nào")]
    [InlineData("lecturer")]
    [InlineData("department_head")]
    [InlineData("admin")]
    public async Task GetStudentHistory_ElevatedRoles_Should_Honor_Target_StudentId(string elevatedRole)
    {
        // Arrange
        var elevatedUserId = Guid.NewGuid();
        var targetStudentId = Guid.NewGuid();
        SetUserContext(elevatedUserId, role: elevatedRole);

        _senderMock
            .Setup(s => s.Send(It.IsAny<GetStudentPracticeHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PracticeHistoryItemDto>>.Success(new List<PracticeHistoryItemDto>()));

        // Act
        var actionResult = await _controller.GetStudentHistory(studentId: targetStudentId);

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        _senderMock.Verify(s => s.Send(
            It.Is<GetStudentPracticeHistoryQuery>(q => q.StudentId == targetStudentId),
            It.IsAny<CancellationToken>()), Times.Once,
            $"Cán bộ vai trò {elevatedRole} có quyền tra cứu lịch sử luyện tập của sinh viên mục tiêu.");
    }

    #endregion

    #region 8. Header Spoofing & Claim Precedence Challenges

    [Fact(DisplayName = "CHALLENGE-BOLA-11. Header Spoofing: Kẻ tấn công gửi header X-User-Id giả mạo khi token đã có NameIdentifier -> Token claim LUÔN có quyền ưu tiên")]
    public async Task HeaderSpoofing_WhenJwtClaimExists_Should_Ignore_X_User_Id_Header()
    {
        // Arrange
        var authenticStudentId = Guid.NewGuid();
        var spoofedHeaderStudentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        var headers = new Dictionary<string, string>
        {
            { "X-User-Id", spoofedHeaderStudentId.ToString() }
        };
        SetUserContext(authenticStudentId, role: "student", customHeaders: headers);

        var request = new StartPracticeSessionRequest(
            StudentId: spoofedHeaderStudentId,
            CourseId: courseId,
            Difficulty: "per_question",
            QuestionCount: 5,
            IsFullSession: false,
            Topic: null
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<StartPracticeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartPracticeSessionResponse>.Success(new StartPracticeSessionResponse(
                Guid.NewGuid(), 60, new List<PracticeQuestionDto>())));

        // Act
        var result = await _controller.StartSession(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _senderMock.Verify(s => s.Send(
            It.Is<StartPracticeSessionCommand>(c => c.StudentId == authenticStudentId),
            It.IsAny<CancellationToken>()), Times.Once,
            "Header X-User-Id giả mạo KHÔNG được ghi đè claim NameIdentifier từ JWT hợp lệ.");
    }

    #endregion

    #region 9. End-to-End CQRS Handler Level BOLA Defense Verification

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "CHALLENGE-BOLA-12. Application CQRS Handlers: Toàn diện 5 handlers từ chối thao tác trái phép từ Student B trên Session của Student A")]
    public async Task CQRS_Handlers_Should_Strictly_Enforce_Ownership_Across_All_Operations()
    {
        // Arrange
        using var context = CreateDbContext();
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Web APIs",
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Câu hỏi BOLA Challenge",
            Content = "RESTful API Invariants",
            Difficulty = "easy",
            IsActive = true
        };

        var studentA = new User
        {
            Id = studentAId,
            Email = "studentA@fpt.edu.vn",
            FullName = "Nguyễn Văn A",
            Role = "student"
        };

        var sessionA = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentAId,
            PracticeMode = "full_session",
            Status = "in_progress",
            LastActivityAt = DateTime.UtcNow,
            Course = course,
            Student = studentA
        };

        context.Courses.Add(course);
        context.Users.Add(studentA);
        context.PracticeQuestions.Add(question);
        context.PracticeSessions.Add(sessionA);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<OralExamination.Application.Common.Interfaces.IGradingQueueChannel>();

        // 1. GetPracticeSessionQuery: Student B truy cập session của Student A -> Bị từ chối
        var getHandler = new GetPracticeSessionQueryHandler(context);
        var getResult = await getHandler.Handle(
            new GetPracticeSessionQuery(sessionA.Id, RequestingUserId: studentBId, IsElevatedRole: false),
            CancellationToken.None);
        getResult.IsSuccess.Should().BeFalse("Student B không được phép đọc session của Student A");
        getResult.Error.Should().Contain("không có quyền");

        // 2. NextQuestionCommand: Student B yêu cầu câu hỏi trên session của Student A -> Bị từ chối
        var nextHandler = new NextQuestionCommandHandler(context);
        var nextResult = await nextHandler.Handle(
            new NextQuestionCommand(sessionA.Id, studentBId),
            CancellationToken.None);
        nextResult.IsSuccess.Should().BeFalse("Student B không được phép advance câu hỏi của Student A");
        nextResult.Error.Should().Contain("không thuộc về sinh viên này");

        // 3. SubmitPracticeAnswerCommand: Student B nộp bài trên session của Student A -> Bị từ chối
        var submitAnswerHandler = new SubmitPracticeAnswerCommandHandler(context, queueChannelMock.Object);
        var submitAnswerResult = await submitAnswerHandler.Handle(
            new SubmitPracticeAnswerCommand(sessionA.Id, question.Id, studentBId, "Hacker answer", false, null),
            CancellationToken.None);
        submitAnswerResult.IsSuccess.Should().BeFalse("Student B không được phép submit câu trả lời vào session của Student A");
        submitAnswerResult.Error.Should().Contain("không thuộc về sinh viên này");

        // 4. SubmitPracticeBatchCommand: Student B nộp batch trên session của Student A -> Bị từ chối
        var submitBatchHandler = new SubmitPracticeBatchCommandHandler(context, queueChannelMock.Object);
        var submitBatchResult = await submitBatchHandler.Handle(
            new SubmitPracticeBatchCommand(sessionA.Id, studentBId, new List<BatchAnswerDto>
            {
                new(question.Id, "Batch answer by B", false, null)
            }),
            CancellationToken.None);
        submitBatchResult.IsSuccess.Should().BeFalse("Student B không được phép batch submit vào session của Student A");
        submitBatchResult.Error.Should().Contain("không thuộc về sinh viên này");

        // 5. CompletePracticeSessionCommand: Student B hoàn tất session của Student A -> Bị từ chối
        var completeHandler = new CompletePracticeSessionCommandHandler(context);
        var completeResult = await completeHandler.Handle(
            new CompletePracticeSessionCommand(sessionA.Id, studentBId),
            CancellationToken.None);
        completeResult.IsSuccess.Should().BeFalse("Student B không được phép hoàn tất session của Student A");
        completeResult.Error.Should().Contain("không thuộc về sinh viên này");

        // Verify session state remain intact
        var refreshedSession = await context.PracticeSessions.FindAsync(sessionA.Id);
        refreshedSession!.Status.Should().Be("in_progress", "Trạng thái session của Student A không bị thay đổi bởi các cuộc tấn công của Student B");
    }

    #endregion
}
