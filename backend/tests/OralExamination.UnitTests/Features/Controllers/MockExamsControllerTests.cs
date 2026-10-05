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
}
