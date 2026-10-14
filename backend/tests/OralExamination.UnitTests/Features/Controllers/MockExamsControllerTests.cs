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
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public class MockExamsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly MockExamsController _controller;

    public MockExamsControllerTests()
    {
        _controller = new MockExamsController(_senderMock.Object);
    }

    private void SetUserContext(Guid userId, string role = "student")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact(DisplayName = "1. GetQuota trả về 200 OK cùng MockExamQuotaDto khi thành công")]
    public async Task GetQuota_Should_Return_200_When_Successful()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dto = new MockExamQuotaDto(
            courseId,
            studentId,
            today,
            UsedCount: 1,
            RemainingCount: 2,
            MaxDailyQuota: 3,
            CanStartExam: true
        );

        _senderMock
            .Setup(s => s.Send(It.Is<GetMockExamQuotaQuery>(q => q.CourseId == courseId && q.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MockExamQuotaDto>.Success(dto));

        var actionResult = await _controller.GetQuota(courseId, null);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact(DisplayName = "2. GetQuota trả về 400 BadRequest khi query thất bại")]
    public async Task GetQuota_Should_Return_400_When_Failed()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        _senderMock
            .Setup(s => s.Send(It.IsAny<GetMockExamQuotaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MockExamQuotaDto>.Failure("Môn học không tồn tại trong hệ thống."));

        var actionResult = await _controller.GetQuota(courseId, null);

        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "3. StartSession trả về 200 OK khi khởi tạo bài thi thử thành công")]
    public async Task StartSession_Should_Return_200_When_Successful()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        var request = new StartMockExamRequest(courseId, HasFollowUp: false);
        var response = new StartMockExamResponse(
            SessionId: Guid.NewGuid(),
            CourseId: courseId,
            CourseCode: "SWD392",
            CourseName: "Software Architecture",
            HasFollowUp: false,
            DurationMinutes: 30,
            StartedAt: DateTime.UtcNow,
            ExpiresAt: DateTime.UtcNow.AddMinutes(30),
            UsedQuota: 1,
            RemainingQuota: 2,
            MaxDailyQuota: 3,
            Questions: new List<MockExamQuestionDto>()
        );

        _senderMock
            .Setup(s => s.Send(It.Is<OralExamination.Application.Features.MockExams.Commands.StartMockExam.StartMockExamCommand>(c => c.CourseId == courseId && c.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartMockExamResponse>.Success(response));

        var actionResult = await _controller.StartSession(request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact(DisplayName = "4. StartSession trả về 429 TooManyRequests khi vượt quá hạn ngạch thi thử")]
    public async Task StartSession_Should_Return_429_When_Quota_Exceeded()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        var request = new StartMockExamRequest(courseId, HasFollowUp: false);

        _senderMock
            .Setup(s => s.Send(It.IsAny<OralExamination.Application.Features.MockExams.Commands.StartMockExam.StartMockExamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartMockExamResponse>.Failure("QUOTA_EXCEEDED:Bạn đã sử dụng hết hạn ngạch thi thử trong ngày (3/3 lượt) cho môn học này."));

        var actionResult = await _controller.StartSession(request);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact(DisplayName = "5. StartSession trả về 404 NotFound khi môn học không tồn tại")]
    public async Task StartSession_Should_Return_404_When_Course_Not_Found()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        var request = new StartMockExamRequest(courseId, HasFollowUp: false);

        _senderMock
            .Setup(s => s.Send(It.IsAny<OralExamination.Application.Features.MockExams.Commands.StartMockExam.StartMockExamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartMockExamResponse>.Failure("Môn học không tồn tại hoặc đã bị vô hiệu hóa."));

        var actionResult = await _controller.StartSession(request);

        var notFoundResult = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "6. StartSession trả về 401 Unauthorized khi không xác định được người dùng")]
    public async Task StartSession_Should_Return_401_When_User_Not_Authenticated()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext() // No user principal
        };

        var request = new StartMockExamRequest(Guid.NewGuid(), HasFollowUp: false);
        var actionResult = await _controller.StartSession(request);

        var unauthorizedResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact(DisplayName = "7. SubmitAnswer trả về 200 OK khi nộp câu trả lời thành công")]
    public async Task SubmitAnswer_Should_Return_200_When_Successful()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);
        var sessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var request = new SubmitMockExamAnswerFormRequest
        {
            QuestionId = questionId,
            TimeTakenSeconds = 30,
            Transcript = "Câu trả lời thử nghiệm"
        };

        var response = new SubmitMockExamAnswerResponse(
            AnswerId: Guid.NewGuid(),
            QuestionId: questionId,
            Transcript: "Câu trả lời thử nghiệm",
            Status: "answered",
            AnsweredAt: DateTime.UtcNow
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<SubmitMockExamAnswerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitMockExamAnswerResponse>.Success(response));

        var actionResult = await _controller.SubmitAnswer(sessionId, request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact(DisplayName = "8. SubmitAnswer trả về 401 Unauthorized khi không xác thực")]
    public async Task SubmitAnswer_Should_Return_401_When_User_Not_Authenticated()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var actionResult = await _controller.SubmitAnswer(Guid.NewGuid(), new SubmitMockExamAnswerFormRequest());

        var unauthorizedResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact(DisplayName = "9. SubmitAnswer trả về 410 Gone khi ca thi hết giờ làm bài")]
    public async Task SubmitAnswer_Should_Return_410_When_Session_Expired()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        _senderMock
            .Setup(s => s.Send(It.IsAny<SubmitMockExamAnswerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitMockExamAnswerResponse>.Failure("Ca thi đã hết thời gian làm bài."));

        var actionResult = await _controller.SubmitAnswer(Guid.NewGuid(), new SubmitMockExamAnswerFormRequest());

        var objResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status410Gone);
    }

    [Fact(DisplayName = "10. SubmitSession trả về 200 OK kèm Scorecard khi nộp bài thành công")]
    public async Task SubmitSession_Should_Return_200_When_Successful()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);
        var sessionId = Guid.NewGuid();

        var scorecard = new MockExamScorecardDto(
            SessionId: sessionId,
            CourseId: Guid.NewGuid(),
            CourseCode: "SWD392",
            CourseName: "Architecture",
            TotalScore: 8.5m,
            Classification: "Giỏi",
            TotalQuestions: 5,
            AnsweredQuestions: 5,
            DurationMinutes: 30,
            IsOverdue: false,
            StartedAt: DateTime.UtcNow.AddMinutes(-30),
            SubmittedAt: DateTime.UtcNow,
            Questions: new List<MockExamQuestionScoreDto>()
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<SubmitMockExamSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MockExamScorecardDto>.Success(scorecard));

        var actionResult = await _controller.SubmitSession(sessionId);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(scorecard);
    }

    [Fact(DisplayName = "11. SubmitSession trả về 404 NotFound khi ca thi không tồn tại")]
    public async Task SubmitSession_Should_Return_404_When_Session_Not_Found()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        _senderMock
            .Setup(s => s.Send(It.IsAny<SubmitMockExamSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MockExamScorecardDto>.Failure("Ca thi không tồn tại."));

        var actionResult = await _controller.SubmitSession(Guid.NewGuid());

        var notFoundResult = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "12. GetHistory trả về 200 OK kèm danh sách lịch sử phân trang")]
    public async Task GetHistory_Should_Return_200_When_Successful()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        var historyResponse = new MockExamHistoryListResponse(
            Items: new List<MockExamHistoryItemDto>(),
            PageIndex: 1,
            PageSize: 10,
            TotalCount: 0,
            TotalPages: 0
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<GetMockExamHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MockExamHistoryListResponse>.Success(historyResponse));

        var actionResult = await _controller.GetHistory(null, 1, 10);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "13. GetHistoryDetail trả về 200 OK kèm Scorecard")]
    public async Task GetHistoryDetail_Should_Return_200_When_Successful()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);
        var sessionId = Guid.NewGuid();

        var scorecard = new MockExamScorecardDto(
            SessionId: sessionId,
            CourseId: Guid.NewGuid(),
            CourseCode: "SWD392",
            CourseName: "Architecture",
            TotalScore: 9.0m,
            Classification: "Xuất sắc",
            TotalQuestions: 5,
            AnsweredQuestions: 5,
            DurationMinutes: 30,
            IsOverdue: false,
            StartedAt: DateTime.UtcNow.AddMinutes(-30),
            SubmittedAt: DateTime.UtcNow,
            Questions: new List<MockExamQuestionScoreDto>()
        );

        _senderMock
            .Setup(s => s.Send(It.IsAny<GetMockExamSessionDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MockExamScorecardDto>.Success(scorecard));

        var actionResult = await _controller.GetHistoryDetail(sessionId);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "14. GetHistoryDetail trả về 404 NotFound khi không tìm thấy ca thi")]
    public async Task GetHistoryDetail_Should_Return_404_When_Not_Found()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        _senderMock
            .Setup(s => s.Send(It.IsAny<GetMockExamSessionDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MockExamScorecardDto>.Failure("Không tìm thấy thông tin ca thi thử."));

        var actionResult = await _controller.GetHistoryDetail(Guid.NewGuid());

        var notFoundResult = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
