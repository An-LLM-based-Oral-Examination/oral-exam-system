using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ResolveDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;
using Dlq = OralExamination.Domain.Entities.DeadLetterQueue;
using System.Text.Json;

namespace OralExamination.UnitTests.Features.DeadLetterQueue;

public class Task09DlqAuthorizationTests
{
    [Theory]
    [InlineData("admin", "PracticeGrading", true)]
    [InlineData("admin", "OfficialExamGrading", false)]
    [InlineData("examination_officer", "OfficialExamGrading", true)]
    [InlineData("examination_officer", "PracticeGrading", false)]
    [InlineData("student", "PracticeGrading", false)]
    [InlineData("proctor", "PracticeGrading", false)]
    [InlineData("department_head", "PracticeGrading", false)]
    public async Task Resolve_RoleFlowCeiling(string role, string taskType, bool allowed)
    {
        await using var db = Create(); var actor = DlqTestActors.User(Guid.NewGuid(), role);
        var item = Item(); item.TaskType = taskType;
        db.Users.Add(actor); db.DeadLetterQueues.Add(item); await db.SaveChangesAsync();
        var result = await new ResolveDlqTaskCommandHandler(db).Handle(new(item.Id, "QA", actor.Id, role), default);
        result.IsSuccess.Should().Be(allowed);
        if (!allowed) item.Status.Should().Be("pending");
    }

    [Fact]
    public async Task List_MissingActor_FailsClosed()
    {
        await using var db = Create(); db.DeadLetterQueues.Add(Item()); await db.SaveChangesAsync();
        var result = await new GetDlqTasksQueryHandler(db).Handle(new(), default);
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public async Task Lecturer_ListAndMutations_UseSamePersistedOwner(bool payloadId, bool replay, bool conflicting)
    {
        await using var db = Create();
        var actor = DlqTestActors.User(Guid.NewGuid(), "lecturer");
        var student = DlqTestActors.User(Guid.NewGuid(), "student");
        var shift = new InClassExamShift { Id = Guid.NewGuid(), ClassId = Guid.NewGuid(), LecturerId = actor.Id, ShiftName = "QA", TotpSecretKey = "QA" };
        var submission = new InClassExamSubmission { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, QuestionId = Guid.NewGuid(), Transcript = "Persisted answer", Status = "FAILED" };
        var item = Item(); item.CorrelationId = conflicting ? Guid.NewGuid() : submission.Id;
        if (payloadId) item.PayloadJson = JsonSerializer.Serialize(new GradingTask { InClassSubmissionId = submission.Id, SessionId = shift.Id, StudentId = student.Id, QuestionId = submission.QuestionId });
        db.Users.AddRange(actor, student); db.InClassExamShifts.Add(shift); db.InClassExamSubmissions.Add(submission); db.DeadLetterQueues.Add(item); await db.SaveChangesAsync();
        var list = await new GetDlqTasksQueryHandler(db).Handle(new(ActorUserId: actor.Id), default);
        list.IsSuccess.Should().BeTrue(); list.Value.Count.Should().Be(conflicting ? 0 : 1);
        var queue = new Mock<IGradingQueueChannel>(); queue.Setup(x => x.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        var result = replay
            ? await new ReplayDlqTaskCommandHandler(db, queue.Object).Handle(new(item.Id, actor.Id, actor.Role), default)
            : await new ResolveDlqTaskCommandHandler(db).Handle(new(item.Id, "QA", actor.Id, actor.Role), default);
        result.IsSuccess.Should().Be(!conflicting);
        if (conflicting)
        {
            item.Status.Should().Be("pending"); submission.Status.Should().Be("FAILED");
            queue.Verify(x => x.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else if (replay)
            queue.Verify(x => x.EnqueueAsync(It.Is<GradingTask>(t => t.AnswerId == submission.Id && t.SessionId == shift.Id && t.StudentId == student.Id && t.AnswerText == submission.Transcript), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, "admin", false)]
    [InlineData(false, "admin", true)]
    [InlineData(true, null, true)]
    [InlineData(true, "lecturer", true)]
    public async Task ActorRole_IsRequiredAndMustMatchActiveDatabaseUser(bool active, string? claimedRole, bool denied)
    {
        await using var db = Create(); var actor = DlqTestActors.User(Guid.NewGuid(), "admin"); actor.IsActive = active;
        var item = Item(); item.TaskType = "PracticeGrading";
        db.Users.Add(actor); db.DeadLetterQueues.Add(item); await db.SaveChangesAsync();
        var result = await new ResolveDlqTaskCommandHandler(db).Handle(new(item.Id, "QA", actor.Id, claimedRole), default);
        result.IsSuccess.Should().Be(!denied);
        if (denied) item.Status.Should().Be("pending");
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingActor_DeniesReplayAndResolveWithoutSideEffects(bool replay)
    {
        await using var db = Create(); var item = Item(); db.DeadLetterQueues.Add(item); await db.SaveChangesAsync();
        var queue = new Mock<IGradingQueueChannel>();
        var result = replay
            ? await new ReplayDlqTaskCommandHandler(db, queue.Object).Handle(new(item.Id), default)
            : await new ResolveDlqTaskCommandHandler(db).Handle(new(item.Id), default);
        result.IsSuccess.Should().BeFalse(); item.Status.Should().Be("pending");
        queue.Verify(x => x.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lecturer_OtherOwnersInClassTask_DeniesWithoutSideEffects(bool replay)
    {
        await using var db = Create();
        var actor = new User { Id = Guid.NewGuid(), Email = "qa@qa.invalid", FullName = "QA", Role = "lecturer", IsActive = true };
        var shift = new InClassExamShift { Id = Guid.NewGuid(), ClassId = Guid.NewGuid(), LecturerId = Guid.NewGuid(), ShiftName = "QA", TotpSecretKey = "QA" };
        var submission = new InClassExamSubmission { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = Guid.NewGuid(), QuestionId = Guid.NewGuid(), Transcript = "QA", Status = "FAILED" };
        var item = Item(); item.CorrelationId = submission.Id;
        db.Users.Add(actor); db.InClassExamShifts.Add(shift); db.InClassExamSubmissions.Add(submission); db.DeadLetterQueues.Add(item); await db.SaveChangesAsync();
        var queue = new Mock<IGradingQueueChannel>();
        var result = replay
            ? await new ReplayDlqTaskCommandHandler(db, queue.Object).Handle(new(item.Id, actor.Id, actor.Role), default)
            : await new ResolveDlqTaskCommandHandler(db).Handle(new(item.Id, "QA", actor.Id, actor.Role), default);
        result.IsSuccess.Should().BeFalse(); item.Status.Should().Be("pending"); submission.Status.Should().Be("FAILED");
        queue.Verify(x => x.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Dlq Item() => new() { Id = Guid.NewGuid(), TaskType = "InClassGrading", FlowType = "IN_CLASS_EXAM", ManagedByRole = "lecturer", PayloadJson = "{}", ErrorMessage = "QA", Status = "pending" };
    private static OralExamDbContext Create() => new(new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
