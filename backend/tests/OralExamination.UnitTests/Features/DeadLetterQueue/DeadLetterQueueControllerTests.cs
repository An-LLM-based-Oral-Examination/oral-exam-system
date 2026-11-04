using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
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
using OralExamination.Application.Features.DeadLetterQueue.Commands.ResolveDlqTask;
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

    private void SetUserContext(string role, string? userId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new(ClaimTypes.NameIdentifier, userId ?? Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact(DisplayName = "1. DeadLetterQueueController phải có [Authorize] chứa admin và examination_officer, KHÔNG chứa department_head")]
    public void Controller_Must_Have_Authorize_With_Admin_And_ExaminationOfficer_Roles_And_No_DepartmentHead()
    {
        var type = typeof(DeadLetterQueueController);
        var authAttr = type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        authAttr.Should().NotBeNull("DeadLetterQueueController bắt buộc phải có [Authorize]");
        authAttr!.Roles.Should().NotBeNull();
        authAttr.Roles.Should().Contain("admin", "Admin phải có thẩm quyền giám sát DLQ luồng MF-01/02");
        authAttr.Roles.Should().Contain("examination_officer", "Cán bộ Khảo thí phải có thẩm quyền giám sát DLQ luồng MF-04");
        authAttr.Roles.Should().NotContain("department_head", "Trưởng Bộ Môn bị cấm 100% can thiệp DLQ theo AGENTS.md và R2");

        // Xác nhận không còn route nào chứa department-head trên controller hoặc method
        var classRoutes = type.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(r => r.Template);
        classRoutes.Should().NotContain(t => t.Contains("department-head"));

        var methodRoutes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes(typeof(RouteAttribute), inherit: true).Cast<RouteAttribute>())
            .Select(r => r.Template);
        methodRoutes.Should().NotContain(t => t.Contains("department-head"));
    }

    [Fact(DisplayName = "2. Các endpoint Admin (/api/v1/admin/dlq) phải có [Authorize(Roles = \"admin\")]")]
    public void Admin_Endpoints_Must_Have_Authorize_With_Admin_Role()
    {
        var type = typeof(DeadLetterQueueController);
        var adminMethods = new[]
        {
            nameof(DeadLetterQueueController.GetAdminDlqTasks),
            nameof(DeadLetterQueueController.ReplayAdminDlqTask),
            nameof(DeadLetterQueueController.ResolveAdminDlqTask)
        };

        foreach (var methodName in adminMethods)
        {
            var method = type.GetMethod(methodName);
            method.Should().NotBeNull($"Method {methodName} phải tồn tại");

            var authAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            authAttr.Should().NotBeNull($"Method {methodName} phải có attribute [Authorize]");
            authAttr!.Roles.Should().Be("admin", $"Method {methodName} phải yêu cầu role admin");
        }
    }

    [Fact(DisplayName = "3. Các endpoint Khảo thí (/api/v1/examination-officer/dlq) phải có [Authorize(Roles = \"examination_officer\")]")]
    public void ExaminationOfficer_Endpoints_Must_Have_Authorize_With_ExaminationOfficer_Role()
    {
        var type = typeof(DeadLetterQueueController);
        var examMethods = new[]
        {
            nameof(DeadLetterQueueController.GetExamOfficerDlqTasks),
            nameof(DeadLetterQueueController.ReplayExamOfficerDlqTask),
            nameof(DeadLetterQueueController.ResolveExamOfficerDlqTask)
        };

        foreach (var methodName in examMethods)
        {
            var method = type.GetMethod(methodName);
            method.Should().NotBeNull($"Method {methodName} phải tồn tại");

            var authAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            authAttr.Should().NotBeNull($"Method {methodName} phải có attribute [Authorize]");
            authAttr!.Roles.Should().Be("examination_officer", $"Method {methodName} phải yêu cầu role examination_officer");
        }
    }

    [Fact(DisplayName = "4. GetAdminDlqTasks gửi query lọc mặc định PRACTICE và MOCK_EXAM")]
    public async Task GetAdminDlqTasks_Should_Filter_By_Admin_FlowTypes_And_Return_200OK()
    {
        SetUserContext("admin");
        var tasks = new List<DeadLetterQueueDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TaskType = "PracticeGrading",
                FlowType = "PRACTICE",
                ManagedByRole = "admin",
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                TaskType = "MockExamGrading",
                FlowType = "MOCK_EXAM",
                ManagedByRole = "admin",
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            }
        };

        _senderMock
            .Setup(s => s.Send(It.Is<GetDlqTasksQuery>(q =>
                q.ManagedByRole == "admin" &&
                q.FlowTypes != null &&
                q.FlowTypes.Contains("PRACTICE") &&
                q.FlowTypes.Contains("MOCK_EXAM")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DeadLetterQueueDto>>.Success(tasks));

        var actionResult = await _controller.GetAdminDlqTasks();

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(tasks);
    }

    [Fact(DisplayName = "5. GetExamOfficerDlqTasks gửi query lọc mặc định OFFICIAL_EXAM")]
    public async Task GetExamOfficerDlqTasks_Should_Filter_By_OfficialExam_And_Return_200OK()
    {
        SetUserContext("examination_officer");
        var tasks = new List<DeadLetterQueueDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TaskType = "OfficialExamGrading",
                FlowType = "OFFICIAL_EXAM",
                ManagedByRole = "examination_officer",
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            }
        };

        _senderMock
            .Setup(s => s.Send(It.Is<GetDlqTasksQuery>(q =>
                q.ManagedByRole == "examination_officer" &&
                q.FlowType == "OFFICIAL_EXAM"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DeadLetterQueueDto>>.Success(tasks));

        var actionResult = await _controller.GetExamOfficerDlqTasks();

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(tasks);
    }

    [Fact(DisplayName = "6. ReplayAdminDlqTask kích hoạt replay với role admin thành công")]
    public async Task ReplayAdminDlqTask_Should_Send_Command_With_Admin_Role_And_Return_200OK()
    {
        var adminId = Guid.NewGuid();
        SetUserContext("admin", adminId.ToString());
        var taskId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<ReplayDlqTaskCommand>(c =>
                c.Id == taskId &&
                c.ActorRole == "admin" &&
                c.ActorUserId == adminId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Success(Unit.Value));

        var actionResult = await _controller.ReplayAdminDlqTask(taskId);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "7. ReplayExamOfficerDlqTask kích hoạt replay với role examination_officer thành công")]
    public async Task ReplayExamOfficerDlqTask_Should_Send_Command_With_ExamOfficer_Role_And_Return_200OK()
    {
        var officerId = Guid.NewGuid();
        SetUserContext("examination_officer", officerId.ToString());
        var taskId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<ReplayDlqTaskCommand>(c =>
                c.Id == taskId &&
                c.ActorRole == "examination_officer" &&
                c.ActorUserId == officerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Success(Unit.Value));

        var actionResult = await _controller.ReplayExamOfficerDlqTask(taskId);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "8. ResolveAdminDlqTask đánh dấu giải quyết với role admin thành công")]
    public async Task ResolveAdminDlqTask_Should_Send_Command_And_Return_200OK()
    {
        var adminId = Guid.NewGuid();
        SetUserContext("admin", adminId.ToString());
        var taskId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<ResolveDlqTaskCommand>(c =>
                c.Id == taskId &&
                c.ResolutionNotes == "Đã xử lý timeout" &&
                c.ActorRole == "admin" &&
                c.ResolvedByUserId == adminId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Success(Unit.Value));

        var actionResult = await _controller.ResolveAdminDlqTask(taskId, new ResolveDlqTaskRequest { ResolutionNotes = "Đã xử lý timeout" });

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "9. ResolveExamOfficerDlqTask đánh dấu giải quyết với role examination_officer thành công")]
    public async Task ResolveExamOfficerDlqTask_Should_Send_Command_And_Return_200OK()
    {
        var officerId = Guid.NewGuid();
        SetUserContext("examination_officer", officerId.ToString());
        var taskId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<ResolveDlqTaskCommand>(c =>
                c.Id == taskId &&
                c.ResolutionNotes == "Đã đối chiếu bài thi" &&
                c.ActorRole == "examination_officer" &&
                c.ResolvedByUserId == officerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Success(Unit.Value));

        var actionResult = await _controller.ResolveExamOfficerDlqTask(taskId, new ResolveDlqTaskRequest { ResolutionNotes = "Đã đối chiếu bài thi" });

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "10. ReplayDlqTask trả về 403 Forbidden khi vi phạm quyền hạn vai trò")]
    public async Task ReplayDlqTask_Should_Return_403Forbidden_When_Role_Forbidden()
    {
        var taskId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<ReplayDlqTaskCommand>(c => c.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Failure("FORBIDDEN: Chỉ có Quản trị viên (Admin) mới có quyền replay tác vụ Luyện tập hoặc Thi thử."));

        var actionResult = await _controller.ReplayDlqTask(taskId);

        var forbiddenResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = forbiddenResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("FORBIDDEN");
    }

    [Fact(DisplayName = "11. ReplayDlqTask trả về 404 NotFound khi không tìm thấy tác vụ DLQ")]
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

    [Fact(DisplayName = "12. ReplayDlqTask trả về 400 BadRequest khi tác vụ đã giải quyết hoặc dữ liệu payload lỗi")]
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
