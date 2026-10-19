using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.DTOs;
using OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;
using Xunit;

namespace OralExamination.UnitTests.Features.DeadLetterQueue;

public class DeadLetterQueueControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly DeadLetterQueueController _controller;

    public DeadLetterQueueControllerTests()
    {
        _controller = new DeadLetterQueueController(_senderMock.Object);
    }

    [Fact(DisplayName = "1. DeadLetterQueueController phải có attribute [Authorize] với Roles chứa department_head")]
    public void Controller_Must_Have_Authorize_With_DepartmentHead_Role()
    {
        var type = typeof(DeadLetterQueueController);
        var authAttr = type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        authAttr.Should().NotBeNull("DeadLetterQueueController bắt buộc phải có [Authorize]");
        authAttr!.Roles.Should().NotBeNull();
        authAttr.Roles.Should().Contain("department_head", "Trưởng Bộ Môn phải có thẩm quyền giám sát và xử lý DLQ");
    }

    [Fact(DisplayName = "2. GetDlqTasks trả về 200 OK kèm danh sách các tác vụ DLQ")]
    public async Task GetDlqTasks_Should_Return_200OK_With_TaskList()
    {
        var tasks = new List<DeadLetterQueueDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TaskType = "PracticeGrading",
                ErrorMessage = "Rate limit 429",
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            }
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<GetDlqTasksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DeadLetterQueueDto>>.Success(tasks));

        var actionResult = await _controller.GetDlqTasks();

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(tasks);
    }

    [Fact(DisplayName = "3. ReplayDlqTask trả về 200 OK khi kích hoạt chấm bù thành công")]
    public async Task ReplayDlqTask_Should_Return_200OK_When_Successful()
    {
        var taskId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<ReplayDlqTaskCommand>(c => c.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Success(Unit.Value));

        var actionResult = await _controller.ReplayDlqTask(taskId);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "4. ReplayDlqTask trả về 404 NotFound khi không tìm thấy tác vụ DLQ")]
    public async Task ReplayDlqTask_Should_Return_404NotFound_When_Task_Not_Found()
    {
        var taskId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<ReplayDlqTaskCommand>(c => c.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Failure("Không tìm thấy tác vụ DLQ với Id đã cho."));

        var actionResult = await _controller.ReplayDlqTask(taskId);

        var notFoundResult = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var problem = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("Không tìm thấy");
    }

    [Fact(DisplayName = "5. ReplayDlqTask trả về 400 BadRequest khi tác vụ đã giải quyết hoặc dữ liệu payload lỗi")]
    public async Task ReplayDlqTask_Should_Return_400BadRequest_When_Invalid()
    {
        var taskId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<ReplayDlqTaskCommand>(c => c.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Failure("Tác vụ DLQ đã được giải quyết (resolved) trước đó."));

        var actionResult = await _controller.ReplayDlqTask(taskId);

        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("đã được giải quyết");
    }
}
