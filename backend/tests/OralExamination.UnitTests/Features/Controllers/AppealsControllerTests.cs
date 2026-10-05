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
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.Appeals.Queries.GetAppealById;
using OralExamination.Application.Features.Appeals.Queries.GetAppeals;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public class AppealsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly AppealsController _controller;

    public AppealsControllerTests()
    {
        _controller = new AppealsController(_senderMock.Object);
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

    [Fact(DisplayName = "1. CreateAppeal trả về 201 Created khi gửi đơn phúc khảo thành công")]
    public async Task CreateAppeal_Should_Return_201_When_Successful()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        var request = new CreateAppealRequestDto
        {
            TicketId = Guid.NewGuid(),
            Reason = "Lý do phúc khảo hợp lệ trên 10 ký tự"
        };

        var responseDto = new AppealResponseDto
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            StudentId = studentId,
            Reason = request.Reason,
            Status = "PENDING"
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<CreateAppealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Success(responseDto));

        var actionResult = await _controller.CreateAppeal(request);

        var createdAtResult = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdAtResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdAtResult.Value.Should().BeEquivalentTo(responseDto);
    }

    [Fact(DisplayName = "2. CreateAppeal trả về 422 UnprocessableEntity khi Command thất bại")]
    public async Task CreateAppeal_Should_Return_422_When_Failed()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId);

        var request = new CreateAppealRequestDto
        {
            TicketId = Guid.NewGuid(),
            Reason = "Lý do ngắn"
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<CreateAppealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Failure("Đơn phúc khảo cho bài thi này đã tồn tại."));

        var actionResult = await _controller.CreateAppeal(request);

        var unprocResult = actionResult.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact(DisplayName = "3. GetAppeals trả về 200 OK với danh sách đơn phúc khảo")]
    public async Task GetAppeals_Should_Return_200_With_List()
    {
        var list = new List<AppealResponseDto>
        {
            new() { Id = Guid.NewGuid(), Reason = "Đơn 1", Status = "PENDING" },
            new() { Id = Guid.NewGuid(), Reason = "Đơn 2", Status = "APPROVED" }
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<GetAppealsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<AppealResponseDto>>.Success(list));

        var actionResult = await _controller.GetAppeals(status: null, sessionId: null, studentId: null, assignedTo: null);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(list);
    }

    [Fact(DisplayName = "4. GetAppealById trả về 200 OK khi tìm thấy đơn")]
    public async Task GetAppealById_Should_Return_200_When_Found()
    {
        var appealId = Guid.NewGuid();
        var dto = new AppealResponseDto { Id = appealId, Reason = "Đơn mẫu", Status = "PENDING" };

        _senderMock
            .Setup(s => s.Send(It.Is<GetAppealByIdQuery>(q => q.Id == appealId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Success(dto));

        var actionResult = await _controller.GetAppealById(appealId);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact(DisplayName = "5. GetAppealById trả về 404 NotFound khi không tìm thấy đơn")]
    public async Task GetAppealById_Should_Return_404_When_Not_Found()
    {
        var appealId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<GetAppealByIdQuery>(q => q.Id == appealId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Failure("Không tìm thấy đơn phúc khảo"));

        var actionResult = await _controller.GetAppealById(appealId);

        var notFoundResult = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "6. ReviewAppeal trả về 200 OK khi thẩm định thành công")]
    public async Task ReviewAppeal_Should_Return_200_When_Successful()
    {
        var reviewerId = Guid.NewGuid();
        SetUserContext(reviewerId, role: "department_head");

        var appealId = Guid.NewGuid();
        var request = new ReviewAppealRequestDto
        {
            Decision = "APPROVED",
            ProposedScore = 9.0m,
            ReviewNotes = "Giải trình chính xác, tăng 0.5 điểm."
        };

        var responseDto = new AppealResponseDto
        {
            Id = appealId,
            Decision = "APPROVED",
            ProposedScore = 9.0m,
            Status = "APPROVED"
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<ReviewAppealDecisionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Success(responseDto));

        var actionResult = await _controller.ReviewAppeal(appealId, request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(responseDto);
    }

    [Fact(DisplayName = "7. ReviewAppeal trả về 422 UnprocessableEntity khi Command thất bại")]
    public async Task ReviewAppeal_Should_Return_422_When_Failed()
    {
        var reviewerId = Guid.NewGuid();
        SetUserContext(reviewerId, role: "department_head");

        var appealId = Guid.NewGuid();
        var request = new ReviewAppealRequestDto
        {
            Decision = "INVALID_DECISION",
            ReviewNotes = "Lỗi"
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<ReviewAppealDecisionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Failure("Quyết định thẩm định không hợp lệ."));

        var actionResult = await _controller.ReviewAppeal(appealId, request);

        var unprocResult = actionResult.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
