using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using API.Controllers.v1;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Admin.Queries.GetAbuseMetrics;
using OralExamination.Application.Features.Admin.Queries.GetRuntimeDiagnostics;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Admin;

public class AdminDiagnosticsTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact]
    public async Task GetRuntimeDiagnosticsQueryHandler_AggregatesAllMetricsCorrectly()
    {
        // Arrange
        await using var context = CreateDbContext();

        // Seed DeadLetterQueue records
        context.DeadLetterQueues.AddRange(
            new DlqEntity
            {
                TaskType = "PracticeGrading",
                FlowType = "PRACTICE",
                Status = "pending",
                ErrorMessage = "Err 1"
            },
            new DlqEntity
            {
                TaskType = "PracticeGrading",
                FlowType = "PRACTICE",
                Status = "pending",
                ErrorMessage = "Err 2"
            },
            new DlqEntity
            {
                TaskType = "MockExamGrading",
                FlowType = "MOCK_EXAM",
                Status = "pending",
                ErrorMessage = "Err 3"
            },
            new DlqEntity
            {
                TaskType = "InClassGrading",
                FlowType = "OFFICIAL_EXAM", // legacy default flow type with InClassGrading task
                Status = "pending",
                ErrorMessage = "Err 4"
            },
            new DlqEntity
            {
                TaskType = "PracticeGrading",
                FlowType = "PRACTICE",
                Status = "resolved",
                ErrorMessage = "Resolved task"
            }
        );
        await context.SaveChangesAsync();

        var gradingQueueMock = new Mock<IGradingQueueChannel>();
        gradingQueueMock.Setup(q => q.Count).Returns(42);
        gradingQueueMock.Setup(q => q.Capacity).Returns(1000);

        var abuseReaderMock = new Mock<IAbuseMetricsReader>();
        abuseReaderMock.Setup(r => r.Snapshot(1)).Returns(new AbuseMetricsSnapshot(120, 5, 1, "api-process"));

        var loginGuardMock = new Mock<ILoginAttemptGuard>();
        loginGuardMock.Setup(g => g.Lockouts).Returns(1);

        var metricsTrackerMock = new Mock<IRuntimeMetricsTracker>();
        metricsTrackerMock.Setup(m => m.GetSnapshot()).Returns(new RuntimeMetricsSnapshot(
            TotalRequests: 500,
            ServerErrors5xx: 2,
            ServerErrorRatePercent: 0.4,
            AverageLatencyMs: 25.5,
            P95LatencyMs: 80.0,
            Uptime: TimeSpan.FromHours(2),
            WorkingSetMb: 150.0,
            GcTotalMemoryMb: 75.0,
            ThreadCount: 16));

        var handler = new GetRuntimeDiagnosticsQueryHandler(
            context,
            gradingQueueMock.Object,
            abuseReaderMock.Object,
            loginGuardMock.Object,
            metricsTrackerMock.Object);

        // Act
        var result = await handler.Handle(new GetRuntimeDiagnosticsQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Uptime.Should().NotBeNullOrWhiteSpace();

        // Bounded channels
        result.BoundedChannels.GradingQueue.Count.Should().Be(42);
        result.BoundedChannels.GradingQueue.Capacity.Should().Be(1000);

        // Dead letter queue breakdown
        result.DeadLetterQueue.TotalPending.Should().Be(4);
        result.DeadLetterQueue.ByFlowType.Practice.Should().Be(2);
        result.DeadLetterQueue.ByFlowType.MockExam.Should().Be(1);
        result.DeadLetterQueue.ByFlowType.InClassExam.Should().Be(1);

        // Abuse metrics
        result.AbuseMetrics.Requests.Should().Be(120);
        result.AbuseMetrics.Rejected.Should().Be(5);
        result.AbuseMetrics.LoginLockouts.Should().Be(1);

        // Performance
        result.Performance.TotalRequests.Should().Be(500);
        result.Performance.ServerErrors5xx.Should().Be(2);
        result.Performance.ServerErrorRatePercent.Should().Be(0.4);
        result.Performance.AverageLatencyMs.Should().Be(25.5);
        result.Performance.P95LatencyMs.Should().Be(80.0);

        // System
        result.System.WorkingSetMb.Should().Be(150.0);
        result.System.GcTotalMemoryMb.Should().Be(75.0);
        result.System.ThreadCount.Should().Be(16);
    }

    [Fact]
    public void AdminController_HasAuthorizeAttribute_WithAdminRole()
    {
        var authAttr = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Be("admin");
    }

    [Fact]
    public async Task GetRuntimeDiagnostics_EndpointMethod_Returns200OkWithDiagnostics()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        var expectedDto = new RuntimeDiagnosticsDto(
            Timestamp: DateTime.UtcNow,
            Uptime: "0.01:00:00",
            BoundedChannels: new BoundedChannelsStats(new BoundedQueueStats(0, 1000)),
            DeadLetterQueue: new DeadLetterQueueStats(0, new DlqByFlowTypeStats(0, 0, 0)),
            AbuseMetrics: new AbuseMetricsDto(10, 0, 0),
            Performance: new PerformanceStats(100, 0, 0.0, 15.0, 30.0),
            System: new SystemStats(128.0, 64.0, 12));

        senderMock
            .Setup(s => s.Send(It.IsAny<GetRuntimeDiagnosticsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var controller = new AdminController(senderMock.Object);

        // Act
        var actionResult = await controller.GetRuntimeDiagnostics(CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedDto);
    }

    [Fact]
    public async Task GetRuntimeDiagnosticsQueryHandler_ExcludesOfficialExamDlqFromTotalPending()
    {
        await using var context = CreateDbContext();
        context.DeadLetterQueues.AddRange(
            new DlqEntity { TaskType = "PracticeGrading", FlowType = "PRACTICE", Status = "pending", ErrorMessage = "P1" },
            new DlqEntity { TaskType = "MockExamGrading", FlowType = "MOCK_EXAM", Status = "pending", ErrorMessage = "M1" },
            new DlqEntity { TaskType = "InClassGrading", FlowType = "OFFICIAL_EXAM", Status = "pending", ErrorMessage = "IC-legacy" },
            new DlqEntity { TaskType = "OfficialExamGrading", FlowType = "OFFICIAL_EXAM", Status = "pending", ErrorMessage = "Official pending" });
        await context.SaveChangesAsync();

        var gradingQueueMock = new Mock<IGradingQueueChannel>();
        gradingQueueMock.Setup(q => q.Count).Returns(0);
        gradingQueueMock.Setup(q => q.Capacity).Returns(1000);

        var abuseReaderMock = new Mock<IAbuseMetricsReader>();
        abuseReaderMock.Setup(r => r.Snapshot(0)).Returns(new AbuseMetricsSnapshot(10, 0, 0, "api-process"));

        var loginGuardMock = new Mock<ILoginAttemptGuard>();
        loginGuardMock.Setup(g => g.Lockouts).Returns(0);

        var metricsTrackerMock = new Mock<IRuntimeMetricsTracker>();
        metricsTrackerMock.Setup(m => m.GetSnapshot()).Returns(new RuntimeMetricsSnapshot(
            TotalRequests: 10, ServerErrors5xx: 0, ServerErrorRatePercent: 0, AverageLatencyMs: 5, P95LatencyMs: 10,
            Uptime: TimeSpan.FromMinutes(5), WorkingSetMb: 50, GcTotalMemoryMb: 20, ThreadCount: 4));

        var handler = new GetRuntimeDiagnosticsQueryHandler(
            context, gradingQueueMock.Object, abuseReaderMock.Object, loginGuardMock.Object, metricsTrackerMock.Object);

        var result = await handler.Handle(new GetRuntimeDiagnosticsQuery(), CancellationToken.None);

        result.DeadLetterQueue.TotalPending.Should().Be(3);
        result.DeadLetterQueue.ByFlowType.Practice.Should().Be(1);
        result.DeadLetterQueue.ByFlowType.MockExam.Should().Be(1);
        result.DeadLetterQueue.ByFlowType.InClassExam.Should().Be(1);
    }

    [Fact]
    public async Task GetRuntimeDiagnostics_And_GetDlqTasks_UseCanonicalTaskTypePrecedence_AndAgreeOnTotalPending()
    {
        await using var context = CreateDbContext();
        var admin = new OralExamination.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = "admin.diag@fpt.edu.vn",
            FullName = "Admin Diagnostics",
            Role = OralExamination.Domain.Enums.UserRole.Admin,
            IsActive = true
        };
        context.Users.Add(admin);
        context.DeadLetterQueues.AddRange(
            new DlqEntity { TaskType = "PracticeGrading", FlowType = "PRACTICE", ManagedByRole = "admin", Status = "pending", ErrorMessage = "P1" },
            new DlqEntity { TaskType = "MockExamGrading", FlowType = "MOCK_EXAM", ManagedByRole = "admin", Status = "pending", ErrorMessage = "M1" },
            new DlqEntity { TaskType = "InClassGrading", FlowType = "OFFICIAL_EXAM", ManagedByRole = "examination_officer", Status = "pending", ErrorMessage = "IC-legacy" },
            new DlqEntity { TaskType = "OfficialExamGrading", FlowType = "IN_CLASS_EXAM", ManagedByRole = "admin", Status = "pending", ErrorMessage = "Official-mistagged" },
            new DlqEntity { TaskType = "OfficialExamGrading", FlowType = "OFFICIAL_EXAM", ManagedByRole = "examination_officer", Status = "pending", ErrorMessage = "Official" });
        await context.SaveChangesAsync();

        var gradingQueueMock = new Mock<IGradingQueueChannel>();
        gradingQueueMock.Setup(q => q.Count).Returns(0);
        gradingQueueMock.Setup(q => q.Capacity).Returns(1000);
        var abuseReaderMock = new Mock<IAbuseMetricsReader>();
        abuseReaderMock.Setup(r => r.Snapshot(0)).Returns(new AbuseMetricsSnapshot(10, 0, 0, "api-process"));
        var loginGuardMock = new Mock<ILoginAttemptGuard>();
        loginGuardMock.Setup(g => g.Lockouts).Returns(0);
        var metricsTrackerMock = new Mock<IRuntimeMetricsTracker>();
        metricsTrackerMock.Setup(m => m.GetSnapshot()).Returns(new RuntimeMetricsSnapshot(
            TotalRequests: 10, ServerErrors5xx: 0, ServerErrorRatePercent: 0, AverageLatencyMs: 5, P95LatencyMs: 10,
            Uptime: TimeSpan.FromMinutes(5), WorkingSetMb: 50, GcTotalMemoryMb: 20, ThreadCount: 4));

        var diagHandler = new GetRuntimeDiagnosticsQueryHandler(
            context, gradingQueueMock.Object, abuseReaderMock.Object, loginGuardMock.Object, metricsTrackerMock.Object);
        var diag = await diagHandler.Handle(new GetRuntimeDiagnosticsQuery(), CancellationToken.None);

        var dlqHandler = new OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks.GetDlqTasksQueryHandler(context);
        var adminDlq = await dlqHandler.Handle(
            new OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks.GetDlqTasksQuery(
                Status: "pending",
                ManagedByRole: "admin",
                FlowTypes: new System.Collections.Generic.List<string> { "PRACTICE", "MOCK_EXAM", "IN_CLASS_EXAM" },
                ActorUserId: admin.Id),
            CancellationToken.None);

        adminDlq.IsSuccess.Should().BeTrue();
        diag.DeadLetterQueue.TotalPending.Should().Be(3);
        diag.DeadLetterQueue.ByFlowType.Practice.Should().Be(1);
        diag.DeadLetterQueue.ByFlowType.MockExam.Should().Be(1);
        diag.DeadLetterQueue.ByFlowType.InClassExam.Should().Be(1);
        adminDlq.Value.Should().HaveCount(diag.DeadLetterQueue.TotalPending);
        adminDlq.Value.Select(t => t.TaskType).Should().BeEquivalentTo(new[] { "PracticeGrading", "MockExamGrading", "InClassGrading" });
        adminDlq.Value.Single(t => t.TaskType == "InClassGrading").FlowType.Should().Be("IN_CLASS_EXAM");
    }
}
