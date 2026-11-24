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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OralExamination.API.Controllers.v1;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.CloseDueInClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.ExpirePersonalInClassExams;
using OralExamination.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using OralExamination.Application.Features.Notifications.Commands.MarkNotificationRead;
using OralExamination.Application.Features.Notifications.DTOs;
using OralExamination.Application.Features.Notifications.Queries.GetNotifications;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Api;

public sealed class MissingControllersAndWorkersCoverageTests
{
    private static ControllerContext CreateControllerContext(Guid? userId = null, string? role = null, bool useSubClaim = false)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(useSubClaim ? "sub" : ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = claims.Count > 0 ? new ClaimsIdentity(claims, "TestAuth") : new ClaimsIdentity();
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    #region LecturerQuestionReviewRequestsController

    [Fact(DisplayName = "PR5-CTRL-01: LecturerQuestionReviewRequestsController yêu cầu role lecturer và trả 401 khi thiếu user claim")]
    public async Task LecturerQuestionReviewRequestsController_RequiresLecturerRole_AndReturns401WhenUnauthenticated()
    {
        var auth = typeof(LecturerQuestionReviewRequestsController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .Single();
        auth.Roles.Should().Be("lecturer");

        var sender = new Mock<ISender>();
        var controller = new LecturerQuestionReviewRequestsController(sender.Object)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.List(Guid.NewGuid(), "PENDING", 1, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        sender.Verify(s => s.Send(It.IsAny<ListMyQuestionReviewRequests>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = "PR5-CTRL-02: LecturerQuestionReviewRequestsController.List chuyển tiếp đúng bộ lọc qua ISender và trả 200 OK")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LecturerQuestionReviewRequestsController_List_ForwardsRequestAndReturnsPage(bool useSubClaim)
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var expectedPage = new ReviewRequestPage(
            [
                new ReviewRequestSummary(
                    Guid.NewGuid(),
                    courseId,
                    "SWD392",
                    "Software Architecture",
                    "GV Nguyễn A",
                    "gv@fpt.edu.vn",
                    DateTime.UtcNow,
                    "Đợt duyệt tuần 5",
                    "PENDING",
                    5,
                    5,
                    0,
                    0,
                    0,
                    1)
            ],
            TotalCount: 1,
            Page: 2,
            PageSize: 20);

        var sender = new Mock<ISender>();
        ListMyQuestionReviewRequests? captured = null;
        sender.Setup(s => s.Send(It.IsAny<ListMyQuestionReviewRequests>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ReviewRequestPage>, CancellationToken>((req, _) => captured = (ListMyQuestionReviewRequests)req)
            .ReturnsAsync(expectedPage);

        var controller = new LecturerQuestionReviewRequestsController(sender.Object)
        {
            ControllerContext = CreateControllerContext(actorId, "lecturer", useSubClaim)
        };

        var action = await controller.List(courseId, "PENDING", page: 2, CancellationToken.None);

        var ok = action.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expectedPage);
        captured.Should().NotBeNull();
        captured!.ActorId.Should().Be(actorId);
        captured.CourseId.Should().Be(courseId);
        captured.Status.Should().Be("PENDING");
        captured.Page.Should().Be(2);
    }

    #endregion

    #region NotificationsController

    [Fact(DisplayName = "PR5-CTRL-03: NotificationsController trả 401 khi không có user claim ở cả 3 endpoints")]
    public async Task NotificationsController_MissingUserClaim_Returns401AcrossAllActions()
    {
        typeof(NotificationsController).GetCustomAttributes<AuthorizeAttribute>(inherit: false).Should().NotBeEmpty();

        var sender = new Mock<ISender>();
        var controller = new NotificationsController(sender.Object)
        {
            ControllerContext = CreateControllerContext()
        };

        (await controller.GetNotifications(cancellationToken: CancellationToken.None)).Should().BeOfType<UnauthorizedResult>();
        (await controller.MarkRead(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<UnauthorizedResult>();
        (await controller.MarkAllRead(CancellationToken.None)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact(DisplayName = "PR5-CTRL-04: NotificationsController.GetNotifications trả 200 OK và 404 ProblemDetails đúng chuẩn RFC 7807")]
    public async Task NotificationsController_GetNotifications_ReturnsOkAnd404ProblemDetails()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        var dto = new NotificationsSummaryResponseDto
        {
            Items =
            [
                new NotificationItemDto
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Type = "GRADE_PUBLISHED",
                    Title = "Đã công bố điểm",
                    Message = "Điểm thi chính thức đã được công bố.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            UnreadCount = 1,
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        sender.Setup(s => s.Send(It.IsAny<GetNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NotificationsSummaryResponseDto>.Success(dto));

        var controller = new NotificationsController(sender.Object)
        {
            ControllerContext = CreateControllerContext(userId, "student")
        };

        var ok = await controller.GetNotifications(unreadOnly: true, limit: 20, page: 1, pageSize: 20, CancellationToken.None);
        ok.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(dto);

        sender.Setup(s => s.Send(It.IsAny<GetNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NotificationsSummaryResponseDto>.Failure("NOT_FOUND: User không tồn tại."));

        var notFound = await controller.GetNotifications(cancellationToken: CancellationToken.None);
        var problem = notFound.Should().BeOfType<NotFoundObjectResult>().Subject
            .Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Type.Should().Be("https://oralexam.fpt.edu.vn/errors/notifications-404");
    }

    [Fact(DisplayName = "PR5-CTRL-05: NotificationsController.MarkRead và MarkAllRead xử lý thành công và lỗi 400 RFC 7807")]
    public async Task NotificationsController_MarkReadAndMarkAllRead_HandleSuccessAndFailure()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        var controller = new NotificationsController(sender.Object)
        {
            ControllerContext = CreateControllerContext(userId, "student", useSubClaim: true)
        };

        // MarkRead success
        sender.Setup(s => s.Send(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));
        (await controller.MarkRead(notificationId, CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        // MarkRead failure -> 400 ProblemDetails
        sender.Setup(s => s.Send(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("FORBIDDEN: Không có quyền."));
        var markReadFail = await controller.MarkRead(notificationId, CancellationToken.None);
        markReadFail.Should().BeOfType<BadRequestObjectResult>().Subject
            .Value.Should().BeOfType<ProblemDetails>().Which.Type.Should().Be("https://oralexam.fpt.edu.vn/errors/notifications-400");

        // MarkAllRead success
        sender.Setup(s => s.Send(It.IsAny<GetNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NotificationsSummaryResponseDto>.Success(new NotificationsSummaryResponseDto { UnreadCount = 3, TotalCount = 5 }));
        sender.Setup(s => s.Send(It.IsAny<MarkAllNotificationsReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));
        (await controller.MarkAllRead(CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        // MarkAllRead failure -> 400 ProblemDetails
        sender.Setup(s => s.Send(It.IsAny<MarkAllNotificationsReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("VALIDATION: Lỗi đánh dấu."));
        var markAllFail = await controller.MarkAllRead(CancellationToken.None);
        markAllFail.Should().BeOfType<BadRequestObjectResult>().Subject
            .Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region ExamStructuresController

    [Fact(DisplayName = "PR5-CTRL-06: ExamStructuresController GET list/by-id trả 200 OK và 404 ProblemDetails khi không tồn tại")]
    public async Task ExamStructuresController_GetListAndGetById_ReturnsOkAndNotFoundProblemDetails()
    {
        var sender = new Mock<ISender>();
        var courseId = Guid.NewGuid();
        var structureId = Guid.NewGuid();
        var sampleDto = new ExamStructureDto(
            Id: structureId,
            CourseId: courseId,
            CourseCode: "SWD392",
            CreatedBy: Guid.NewGuid(),
            Name: "Cấu trúc đề FE Fall 2026",
            TotalQuestions: 5,
            DurationMinutes: 30,
            BloomDistribution: "{\"Understand\":2,\"Analyze\":3}",
            IsActive: true,
            CreatedAt: DateTime.UtcNow);

        sender.Setup(s => s.Send(It.IsAny<GetExamStructuresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ExamStructureDto>>.Success([sampleDto]));

        var controller = new ExamStructuresController(sender.Object)
        {
            ControllerContext = CreateControllerContext(Guid.NewGuid(), "department_head")
        };

        var listResult = await controller.GetExamStructures(courseId, isActive: true, CancellationToken.None);
        listResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new[] { sampleDto });

        var byIdResult = await controller.GetExamStructureById(structureId, CancellationToken.None);
        byIdResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(sampleDto);

        sender.Setup(s => s.Send(It.IsAny<GetExamStructuresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ExamStructureDto>>.Success([]));

        var missing = await controller.GetExamStructureById(Guid.NewGuid(), CancellationToken.None);
        var problem = missing.Should().BeOfType<NotFoundObjectResult>().Subject
            .Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Type.Should().Be("https://oralexam.fpt.edu.vn/errors/exam-structure-not-found");
    }

    [Fact(DisplayName = "PR5-CTRL-07: ExamStructuresController POST/PUT khóa role department_head và map đúng 200/400/403/404 RFC 7807")]
    public async Task ExamStructuresController_CreateAndUpdate_EnforceRoleAndMapProblems()
    {
        var createAuth = typeof(ExamStructuresController)
            .GetMethod(nameof(ExamStructuresController.CreateExamStructure))!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .Single();
        createAuth.Roles.Should().Be("department_head");

        var updateAuth = typeof(ExamStructuresController)
            .GetMethod(nameof(ExamStructuresController.UpdateExamStructure))!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .Single();
        updateAuth.Roles.Should().Be("department_head");

        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var structureId = Guid.NewGuid();
        var dto = new ExamStructureDto(
            Id: structureId,
            CourseId: courseId,
            CourseCode: "PRN231",
            CreatedBy: actorId,
            Name: "Structure A",
            TotalQuestions: 5,
            DurationMinutes: 30,
            BloomDistribution: "{}",
            IsActive: true,
            CreatedAt: DateTime.UtcNow);

        var sender = new Mock<ISender>();
        var controller = new ExamStructuresController(sender.Object)
        {
            ControllerContext = CreateControllerContext(actorId, "department_head")
        };

        sender.Setup(s => s.Send(It.IsAny<CreateExamStructureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamStructureDto>.Success(dto));

        var createOk = await controller.CreateExamStructure(
            new CreateExamStructureRequest(
                CourseId: courseId,
                Name: "Structure A",
                TotalQuestions: 5,
                DurationMinutes: 30,
                BloomDistribution: "{}",
                IsActive: true),
            CancellationToken.None);
        createOk.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(dto);

        sender.Setup(s => s.Send(It.IsAny<CreateExamStructureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamStructureDto>.Failure("FORBIDDEN: Không phụ trách môn."));
        var createForbidden = await controller.CreateExamStructure(
            new CreateExamStructureRequest(CourseId: courseId, Name: "Structure A"),
            CancellationToken.None);
        createForbidden.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        sender.Setup(s => s.Send(It.IsAny<UpdateExamStructureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamStructureDto>.Success(dto));
        var updateOk = await controller.UpdateExamStructure(
            structureId,
            new UpdateExamStructureRequest(Name: "Structure A Updated", TotalQuestions: 6, DurationMinutes: 35, BloomDistribution: "{}", IsActive: true),
            CancellationToken.None);
        updateOk.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(dto);

        sender.Setup(s => s.Send(It.IsAny<UpdateExamStructureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamStructureDto>.Failure("NOT_FOUND: Không tìm thấy cấu trúc."));
        var updateNotFound = await controller.UpdateExamStructure(
            structureId,
            new UpdateExamStructureRequest(Name: "Structure A Updated"),
            CancellationToken.None);
        updateNotFound.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region Workers (InClassShiftAutoCloseWorker & OfficialExamGradingDispatcher)

    [Fact(DisplayName = "PR5-WRK-01: InClassShiftAutoCloseWorker gọi lần lượt CloseDueInClassShiftsCommand và ExpirePersonalInClassExamsCommand rồi dừng an toàn")]
    public async Task InClassShiftAutoCloseWorker_ExecutesBothCommands_AndStopsGracefully()
    {
        var executionOrder = new List<string>();
        var tickCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CloseDueInClassShiftsCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionOrder.Add("close_due"))
            .ReturnsAsync(2);
        senderMock.Setup(s => s.Send(It.IsAny<ExpirePersonalInClassExamsCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                executionOrder.Add("expire_personal");
                tickCompleted.TrySetResult();
            })
            .ReturnsAsync(3);

        var services = new ServiceCollection();
        services.AddScoped(_ => senderMock.Object);
        using var provider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<InClassShiftAutoCloseWorker>>();
        using var worker = new InClassShiftAutoCloseWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            loggerMock.Object);

        await worker.StartAsync(CancellationToken.None);
        await tickCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        executionOrder.Should().ContainInOrder("close_due", "expire_personal");
        senderMock.Verify(s => s.Send(It.IsAny<CloseDueInClassShiftsCommand>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        senderMock.Verify(s => s.Send(It.IsAny<ExpirePersonalInClassExamsCommand>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "PR5-WRK-02: InClassShiftAutoCloseWorker nuốt lỗi bất thường có ghi log Error và không làm sập BackgroundService")]
    public async Task InClassShiftAutoCloseWorker_WhenCommandThrows_LogsErrorAndRemainsStable()
    {
        var errorLogged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CloseDueInClassShiftsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated transient DB failure"));

        var services = new ServiceCollection();
        services.AddScoped(_ => senderMock.Object);
        using var provider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<InClassShiftAutoCloseWorker>>();
        loggerMock.Setup(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => errorLogged.TrySetResult());

        using var worker = new InClassShiftAutoCloseWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            loggerMock.Object);

        await worker.StartAsync(CancellationToken.None);
        await errorLogged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(ex => ex is InvalidOperationException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact(DisplayName = "PR5-WRK-03: OfficialExamGradingDispatcher claim đúng pending & lease hết hạn, bỏ qua completed/future/active-lease")]
    public async Task OfficialExamGradingDispatcher_ClaimsOnlyEligibleJobs_AndEnqueuesInCreatedOrder()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        using var provider = services.BuildServiceProvider();

        var now = DateTime.UtcNow;
        var pendingJob = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = Guid.NewGuid(),
            Status = "pending",
            AttemptCount = 0,
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-10)
        };
        var expiredLeaseJob = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = Guid.NewGuid(),
            Status = "processing",
            AttemptCount = 1,
            LockedUntil = now.AddMinutes(-1),
            CreatedAt = now.AddMinutes(-5),
            UpdatedAt = now.AddMinutes(-5)
        };
        var activeLeaseJob = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = Guid.NewGuid(),
            Status = "processing",
            AttemptCount = 1,
            LockedUntil = now.AddMinutes(4),
            CreatedAt = now.AddMinutes(-8),
            UpdatedAt = now.AddMinutes(-8)
        };
        var futureRetryJob = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = Guid.NewGuid(),
            Status = "pending",
            AttemptCount = 2,
            NextAttemptAt = now.AddMinutes(3),
            CreatedAt = now.AddMinutes(-9),
            UpdatedAt = now.AddMinutes(-9)
        };
        var completedJob = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = Guid.NewGuid(),
            Status = "completed",
            AttemptCount = 1,
            CompletedAt = now.AddMinutes(-2),
            CreatedAt = now.AddMinutes(-12),
            UpdatedAt = now.AddMinutes(-2)
        };

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.GradingJobs.AddRange(pendingJob, expiredLeaseJob, activeLeaseJob, futureRetryJob, completedJob);
            await db.SaveChangesAsync();
        }

        var enqueued = new List<OfficialExamGradingTask>();
        var queueMock = new Mock<IOfficialExamGradingQueue>();
        queueMock.Setup(q => q.EnqueueAsync(It.IsAny<OfficialExamGradingTask>(), It.IsAny<CancellationToken>()))
            .Callback<OfficialExamGradingTask, CancellationToken>((task, _) => enqueued.Add(task))
            .Returns(ValueTask.CompletedTask);

        using var dispatcher = new OfficialExamGradingDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            queueMock.Object,
            Mock.Of<ILogger<OfficialExamGradingDispatcher>>());

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        enqueued.Should().HaveCount(2);
        enqueued[0].Should().Be(new OfficialExamGradingTask(pendingJob.Id, pendingJob.SubmissionId));
        enqueued[1].Should().Be(new OfficialExamGradingTask(expiredLeaseJob.Id, expiredLeaseJob.SubmissionId));

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();

        var claimedPending = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == pendingJob.Id);
        claimedPending.Status.Should().Be("processing");
        claimedPending.AttemptCount.Should().Be(1);
        claimedPending.LockedUntil.Should().BeAfter(now.AddMinutes(4));

        var claimedExpired = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == expiredLeaseJob.Id);
        claimedExpired.Status.Should().Be("processing");
        claimedExpired.AttemptCount.Should().Be(2);
        claimedExpired.LockedUntil.Should().BeAfter(now.AddMinutes(4));

        var untouchedActive = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == activeLeaseJob.Id);
        untouchedActive.AttemptCount.Should().Be(1);

        var untouchedFuture = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == futureRetryJob.Id);
        untouchedFuture.Status.Should().Be("pending");
        untouchedFuture.AttemptCount.Should().Be(2);

        var untouchedCompleted = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == completedJob.Id);
        untouchedCompleted.Status.Should().Be("completed");
    }

    [Fact(DisplayName = "PR5-WRK-04: OfficialExamGradingDispatcher giới hạn tối đa 50 jobs mỗi batch để bảo vệ áp lực hàng đợi")]
    public async Task OfficialExamGradingDispatcher_CapsBatchSizeAt50Jobs()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        using var provider = services.BuildServiceProvider();

        var baseTime = DateTime.UtcNow.AddHours(-1);
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            for (var i = 0; i < 55; i++)
            {
                db.GradingJobs.Add(new GradingJob
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = Guid.NewGuid(),
                    Status = "pending",
                    CreatedAt = baseTime.AddSeconds(i),
                    UpdatedAt = baseTime.AddSeconds(i)
                });
            }

            await db.SaveChangesAsync();
        }

        var enqueuedCount = 0;
        var queueMock = new Mock<IOfficialExamGradingQueue>();
        queueMock.Setup(q => q.EnqueueAsync(It.IsAny<OfficialExamGradingTask>(), It.IsAny<CancellationToken>()))
            .Callback(() => enqueuedCount++)
            .Returns(ValueTask.CompletedTask);

        using var dispatcher = new OfficialExamGradingDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            queueMock.Object,
            Mock.Of<ILogger<OfficialExamGradingDispatcher>>());

        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        enqueuedCount.Should().Be(50);

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        (await verifyDb.GradingJobs.CountAsync(j => j.Status == "processing")).Should().Be(50);
        (await verifyDb.GradingJobs.CountAsync(j => j.Status == "pending")).Should().Be(5);
    }

    #endregion

    #region Clean Architecture Boundary Enforcement (Reflection — Zero External Packages)

    [Fact(DisplayName = "PR5-ARCH-01: Assembly Domain không được tham chiếu Application, Infrastructure, API, EF Core hay ASP.NET Core")]
    public void Architecture_DomainAssembly_MustRemainPure()
    {
        var referencedNames = typeof(User).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        referencedNames.Should().NotContain(name =>
            name.Equals("OralExamination.Application", StringComparison.OrdinalIgnoreCase)
            || name.Equals("OralExamination.Infrastructure", StringComparison.OrdinalIgnoreCase)
            || name.Equals("OralExamination.API", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase),
            "Domain phải hoàn toàn độc lập với tầng ngoài, EF Core và ASP.NET Core");
    }

    [Fact(DisplayName = "PR5-ARCH-02: Assembly Application không được tham chiếu Infrastructure, API hay ASP.NET Core")]
    public void Architecture_ApplicationAssembly_MustNotDependOnInfrastructureOrApi()
    {
        var referencedNames = typeof(Result).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        referencedNames.Should().NotContain(name =>
            name.Equals("OralExamination.Infrastructure", StringComparison.OrdinalIgnoreCase)
            || name.Equals("OralExamination.API", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase),
            "Application không được phụ thuộc vào Infrastructure, API hoặc ASP.NET Core");
    }

    [Fact(DisplayName = "PR5-ARCH-03: Mọi Controller trong API phải kế thừa ControllerBase và gắn [Authorize] trừ các public endpoints có chủ đích")]
    public void Architecture_AllApiControllers_MustInheritControllerBase_AndDeclareAuthorizeUnlessExplicitlyPublic()
    {
        var apiAssembly = typeof(OfficialExamKioskController).Assembly;
        var explicitlyPublicControllers = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(AuthController),
            nameof(VerificationController),
            "HealthController"
        };

        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract
                && (typeof(ControllerBase).IsAssignableFrom(t)
                    || t.Name.EndsWith("Controller", StringComparison.Ordinal)
                    || t.GetCustomAttributes<ApiControllerAttribute>(inherit: true).Any()))
            .ToList();

        controllerTypes.Should().NotBeEmpty();
        controllerTypes.Should().Contain(typeof(OfficialExamKioskController));

        foreach (var controller in controllerTypes)
        {
            typeof(ControllerBase).IsAssignableFrom(controller).Should().BeTrue(
                $"Controller {controller.FullName} phải kế thừa ControllerBase");

            if (explicitlyPublicControllers.Contains(controller.Name))
            {
                continue;
            }

            var hasClassAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
            hasClassAuthorize.Should().BeTrue(
                $"Controller {controller.FullName} phải khai báo [Authorize] ở cấp lớp (trừ các public controller được duyệt)");
        }
    }

    #endregion
}
