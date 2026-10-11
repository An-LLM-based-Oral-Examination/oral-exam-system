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
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.DTOs;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public class PracticeControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly PracticeController _controller;

    public PracticeControllerTests()
    {
        _controller = new PracticeController(_senderMock.Object);
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

    [Fact(DisplayName = "1. StartSession trả về 200 OK cùng StartPracticeSessionResponse khi thành công")]
    public async Task StartSession_Should_Return_200_When_Successful()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetUserContext(studentId);

        var request = new StartPracticeSessionRequest(
            StudentId: studentId,
            CourseId: courseId,
            Difficulty: "progressive",
            QuestionCount: 5,
            IsFullSession: false,
            Topic: null
        );

        var expectedResponse = new StartPracticeSessionResponse(
            SessionId: Guid.NewGuid(),
            TranscriptBufferSeconds: 60,
            Questions: new List<PracticeQuestionDto>
            {
                new(Guid.NewGuid(), "Câu hỏi 1", new List<string>()),
                new(Guid.NewGuid(), "Câu hỏi 2", new List<string>()),
                new(Guid.NewGuid(), "Câu hỏi 3", new List<string>()),
                new(Guid.NewGuid(), "Câu hỏi 4", new List<string>()),
                new(Guid.NewGuid(), "Câu hỏi 5", new List<string>())
            }
        );

        _senderMock
            .Setup(s => s.Send(It.Is<StartPracticeSessionCommand>(c =>
                c.StudentId == studentId &&
                c.CourseId == courseId &&
                c.Difficulty == "progressive" &&
                c.QuestionCount == 5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartPracticeSessionResponse>.Success(expectedResponse));

        // Act
        var actionResult = await _controller.StartSession(request);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact(DisplayName = "2. StartSession trả về 400 BadRequest kèm ProblemDetails tiếng Việt khi kho đề không đủ câu hỏi")]
    public async Task StartSession_Should_Return_400_With_ProblemDetails_When_Insufficient_Questions()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetUserContext(studentId);

        var request = new StartPracticeSessionRequest(
            StudentId: studentId,
            CourseId: courseId,
            Difficulty: "progressive",
            QuestionCount: 4,
            IsFullSession: false,
            Topic: null
        );

        const string errorMessage = "Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó: cần 1 Dễ (có 10), 2 Trung bình (có 1), 1 Khó (có 10). Vui lòng liên hệ giảng viên bổ sung câu hỏi.";

        _senderMock
            .Setup(s => s.Send(It.IsAny<StartPracticeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartPracticeSessionResponse>.Failure(errorMessage));

        // Act
        var actionResult = await _controller.StartSession(request);

        // Assert
        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Dữ liệu đầu vào không hợp lệ.");
        problemDetails.Detail.Should().Be(errorMessage);
    }

    [Fact(DisplayName = "3. GetNextQuestion trả về 200 OK cùng NextQuestionResponse khi bốc được câu hỏi thành công")]
    public async Task GetNextQuestion_Should_Return_200_When_Successful_With_Question()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetUserContext(studentId);

        var expectedResponse = new NextQuestionResponse
        {
            HasMoreQuestions = true,
            Question = new NextQuestionDto
            {
                Id = Guid.NewGuid(),
                Content = "Nội dung câu hỏi tiếp theo",
                Difficulty = "medium",
                QuestionOrder = 2,
                RubricCriteria = new List<string> { "Tiêu chí 1", "Tiêu chí 2" }
            }
        };

        _senderMock
            .Setup(s => s.Send(It.Is<NextQuestionCommand>(c =>
                c.SessionId == sessionId && c.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NextQuestionResponse>.Success(expectedResponse));

        // Act
        var actionResult = await _controller.GetNextQuestion(sessionId);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact(DisplayName = "4. GetNextQuestion trả về 200 OK với HasMoreQuestions = false khi kho đề đã hết câu")]
    public async Task GetNextQuestion_Should_Return_200_When_HasMoreQuestions_Is_False()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetUserContext(studentId);

        var expectedResponse = new NextQuestionResponse
        {
            HasMoreQuestions = false,
            Question = null,
            Message = "Đã hoàn thành toàn bộ câu hỏi khả dụng theo mức độ đã chọn."
        };

        _senderMock
            .Setup(s => s.Send(It.Is<NextQuestionCommand>(c =>
                c.SessionId == sessionId && c.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NextQuestionResponse>.Success(expectedResponse));

        // Act
        var actionResult = await _controller.GetNextQuestion(sessionId);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact(DisplayName = "5. GetNextQuestion trả về 410 Gone ProblemDetails khi phiên bị timeout quá 10 phút")]
    public async Task GetNextQuestion_Should_Return_410_Gone_When_Session_Timed_Out()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetUserContext(studentId);

        const string timeoutMessage = "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 10 phút.";

        _senderMock
            .Setup(s => s.Send(It.Is<NextQuestionCommand>(c =>
                c.SessionId == sessionId && c.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NextQuestionResponse>.Failure(timeoutMessage));

        // Act
        var actionResult = await _controller.GetNextQuestion(sessionId);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status410Gone);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status410Gone);
        problemDetails.Title.Should().Be("Session Timed Out");
        problemDetails.Detail.Should().Be(timeoutMessage);
    }

    [Fact(DisplayName = "6. GetNextQuestion trả về 404 NotFound ProblemDetails khi không tìm thấy phiên")]
    public async Task GetNextQuestion_Should_Return_404_NotFound_When_Session_Not_Found()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetUserContext(studentId);

        const string notFoundMessage = "Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.";

        _senderMock
            .Setup(s => s.Send(It.Is<NextQuestionCommand>(c =>
                c.SessionId == sessionId && c.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NextQuestionResponse>.Failure(notFoundMessage));

        // Act
        var actionResult = await _controller.GetNextQuestion(sessionId);

        // Assert
        var notFoundResult = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
        problemDetails.Title.Should().Be("Session Not Found");
        problemDetails.Detail.Should().Be(notFoundMessage);
    }
}
