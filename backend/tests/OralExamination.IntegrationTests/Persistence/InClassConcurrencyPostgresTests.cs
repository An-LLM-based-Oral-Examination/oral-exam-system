using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class InClassConcurrencyPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ShiftAllocationAdvisoryLock_WhenContended_SerializesIndependentConnections()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var shiftId = Guid.NewGuid();
        var firstHasLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        long secondWaitMilliseconds = 0;

        var first = Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            await using var scope = await context.BeginShiftAllocationLockScopeAsync(shiftId);
            scope.Should().NotBeNull();
            firstHasLock.TrySetResult();
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(500);
        });
        var second = Task.Run(async () =>
        {
            await firstHasLock.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var context = database.CreateContext();
            secondStarted.TrySetResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await using var scope = await context.BeginShiftAllocationLockScopeAsync(shiftId);
            stopwatch.Stop();
            scope.Should().NotBeNull();
            secondWaitMilliseconds = stopwatch.ElapsedMilliseconds;
        });

        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
        secondWaitMilliseconds.Should().BeGreaterThanOrEqualTo(200,
            "the second PostgreSQL session must wait for the transaction-level advisory lock");
    }

    [Fact]
    public async Task ConcurrentSameQuestionAndTranscript_IsIdempotentAndPersistsOneSubmission()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedShiftAsync(database, joiningStudents: 1, seedExistingQuestionUsage: false,
            seedJoinedParticipant: true);
        var studentId = data.JoiningStudentIds.Single();
        var questionId = data.QuestionIds[0];
        var queue = new RecordingQueue();
        var barrier = new Barrier(2);
        var command = new SubmitInClassShiftAnswerCommand(
            studentId, data.ShiftId, questionId, "Identical concurrent transcript");

        var attempts = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new SubmitInClassShiftAnswerCommandHandler(
                context, queue, new SubmitInClassShiftAnswerCommandValidator());
            barrier.SignalAndWait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            return await handler.Handle(command, default);
        })).ToArray();

        var results = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(30));
        results.Should().OnlyContain(result => result.IsSuccess,
            string.Join(" | ", results.Select(result => result.Error)));
        results.Select(result => result.Value!.SubmissionId).Distinct().Should().ContainSingle();
        await using var verify = database.CreateContext();
        (await verify.InClassExamSubmissions.CountAsync(item =>
            item.ShiftId == data.ShiftId && item.StudentId == studentId && item.QuestionId == questionId))
            .Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentSameQuestionAndDifferentTranscript_ReturnsOneConflictAndPersistsOneSubmission()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedShiftAsync(database, joiningStudents: 1, seedExistingQuestionUsage: false,
            seedJoinedParticipant: true);
        var studentId = data.JoiningStudentIds.Single();
        var questionId = data.QuestionIds[0];
        var queue = new RecordingQueue();
        var barrier = new Barrier(2);
        var commands = new[]
        {
            new SubmitInClassShiftAnswerCommand(studentId, data.ShiftId, questionId, "Concurrent transcript A"),
            new SubmitInClassShiftAnswerCommand(studentId, data.ShiftId, questionId, "Concurrent transcript B")
        };

        var attempts = commands.Select(command => Task.Run(async () =>
        {
            await using var context = database.CreateContext();
            var handler = new SubmitInClassShiftAnswerCommandHandler(
                context, queue, new SubmitInClassShiftAnswerCommandValidator());
            barrier.SignalAndWait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            return await handler.Handle(command, default);
        })).ToArray();

        var results = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(30));
        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => !result.IsSuccess && result.Error == InClassShiftErrors.DuplicateSubmission)
            .Should().Be(1);
        await using var verify = database.CreateContext();
        (await verify.InClassExamSubmissions.CountAsync(item =>
            item.ShiftId == data.ShiftId && item.StudentId == studentId && item.QuestionId == questionId))
            .Should().Be(1);
    }

    [Fact]
    public async Task SubmissionStatusConcurrencyToken_PreservesLecturerAuditAgainstStaleWorker()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedShiftAsync(database, joiningStudents: 1, seedExistingQuestionUsage: false,
            seedJoinedParticipant: true);
        var studentId = data.JoiningStudentIds.Single();
        var questionId = data.QuestionIds[0];
        var submissionId = Guid.NewGuid();
        await using (var setup = database.CreateContext())
        {
            setup.InClassExamSubmissions.Add(new InClassExamSubmission
            {
                Id = submissionId,
                ShiftId = data.ShiftId,
                StudentId = studentId,
                QuestionId = questionId,
                Transcript = "Concurrency token integration test",
                Status = "SUBMITTED",
                SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await setup.SaveChangesAsync();
        }

        await using var worker = database.CreateContext();
        var staleSubmission = await worker.InClassExamSubmissions.SingleAsync(item => item.Id == submissionId);
        await using (var lecturer = database.CreateContext())
        {
            var audited = await lecturer.InClassExamSubmissions.SingleAsync(item => item.Id == submissionId);
            audited.FinalScore = 9.8m;
            audited.LecturerNotes = "Lecturer audit wins";
            audited.Status = "AUDITED";
            audited.GradedAt = DateTime.UtcNow;
            await lecturer.SaveChangesAsync();
        }

        staleSubmission.AiScore = 4.5m;
        staleSubmission.FinalScore = 4.5m;
        staleSubmission.Status = "AI_GRADED";
        staleSubmission.GradedAt = DateTime.UtcNow;
        var saveStaleWorker = async () => await worker.SaveChangesAsync();
        await saveStaleWorker.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verify = database.CreateContext();
        var persisted = await verify.InClassExamSubmissions.SingleAsync(item => item.Id == submissionId);
        persisted.Status.Should().Be("AUDITED");
        persisted.FinalScore.Should().Be(9.8m);
        persisted.LecturerNotes.Should().Be("Lecturer audit wins");
    }

    [Fact]
    public async Task ConcurrentJoins_UseCurrentCommittedLoad_AndKeepQuestionUsageBalanced()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedShiftAsync(database, joiningStudents: 3, seedExistingQuestionUsage: true);
        var barrier = new MutationBarrier("in_class_shift_participants", expectedArrivals: 3);
        var queue = new RecordingQueue();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var attempts = data.JoiningStudentIds.Select(async studentId =>
        {
            await using var context = database.CreateContext(barrier);
            var handler = new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator(), queue);
            return await handler.Handle(new EnterInClassShiftCommand(studentId, data.ShiftId, data.OtpCode), timeout.Token);
        });

        var results = await Task.WhenAll(attempts);

        results.Should().OnlyContain(result => result.IsSuccess, string.Join(" | ", results.Select(result => result.Error)));
        barrier.ConnectionIds.Should().HaveCount(3,
            "the race must execute through three physical PostgreSQL sessions");
        await using var verify = database.CreateContext();
        var assigned = await verify.InClassShiftParticipants
            .Where(item => item.ShiftId == data.ShiftId)
            .Select(item => item.AssignedQuestionId!.Value)
            .ToListAsync();
        var usage = data.QuestionIds.ToDictionary(id => id, id => assigned.Count(value => value == id));
        (usage.Values.Max() - usage.Values.Min()).Should().BeLessThanOrEqualTo(1,
            "least-used allocation must be evaluated after each committed join, not from a shared stale snapshot");
    }

    [Fact]
    public async Task ConcurrentSameStudentJoin_IsIdempotentAndReturnsOneAssignment()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedShiftAsync(database, joiningStudents: 1, seedExistingQuestionUsage: false);
        var barrier = new MutationBarrier("in_class_shift_participants", expectedArrivals: 2);
        var queue = new RecordingQueue();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var studentId = data.JoiningStudentIds.Single();

        var attempts = Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var context = database.CreateContext(barrier);
            var handler = new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator(), queue);
            return await handler.Handle(new EnterInClassShiftCommand(studentId, data.ShiftId, data.OtpCode), timeout.Token);
        });

        var results = await Task.WhenAll(attempts);

        results.Should().OnlyContain(result => result.IsSuccess, string.Join(" | ", results.Select(result => result.Error)));
        results.Select(result => result.Value!.AssignedQuestionId).Distinct().Should().ContainSingle();
        barrier.ConnectionIds.Should().HaveCount(2,
            "both PostgreSQL sessions must reach the serialization boundary before either proceeds");
        await using var verify = database.CreateContext();
        (await verify.InClassShiftParticipants.CountAsync(item =>
            item.ShiftId == data.ShiftId && item.StudentId == studentId)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentDifferentQuestionSubmissions_FinalizeParticipantAfterBothCommit()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var data = await SeedShiftAsync(database, joiningStudents: 1, seedExistingQuestionUsage: false,
            seedJoinedParticipant: true);
        var barrier = new MutationBarrier("in_class_exam_submissions", expectedArrivals: 2);
        var queue = new RecordingQueue();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var studentId = data.JoiningStudentIds.Single();

        var attempts = data.QuestionIds.Select(async questionId =>
        {
            await using var context = database.CreateContext(barrier);
            var handler = new SubmitInClassShiftAnswerCommandHandler(
                context, queue, new SubmitInClassShiftAnswerCommandValidator());
            return await handler.Handle(new SubmitInClassShiftAnswerCommand(
                studentId, data.ShiftId, questionId, $"Answer for {questionId:N}"), timeout.Token);
        });

        var results = await Task.WhenAll(attempts);

        results.Should().OnlyContain(result => result.IsSuccess, string.Join(" | ", results.Select(result => result.Error)));
        barrier.ConnectionIds.Should().HaveCount(2);
        await using var verify = database.CreateContext();
        (await verify.InClassExamSubmissions.CountAsync(item =>
            item.ShiftId == data.ShiftId && item.StudentId == studentId)).Should().Be(2);
        (await verify.InClassShiftParticipants.SingleAsync(item =>
            item.ShiftId == data.ShiftId && item.StudentId == studentId)).Status.Should().Be("SUBMITTED");
        queue.Tasks.Should().HaveCount(2);
    }

    private static async Task<ShiftData> SeedShiftAsync(
        TestDatabase database,
        int joiningStudents,
        bool seedExistingQuestionUsage,
        bool seedJoinedParticipant = false)
    {
        await using var context = database.CreateContext();
        var academic = await TestData.AcademicAsync(context);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var @class = new Class
        {
            Code = "IC" + suffix,
            Name = "Integration in-class",
            JoinCode = "J" + suffix,
            CourseId = academic.Course.Id,
            SemesterId = academic.Course.SemesterId,
            LecturerId = academic.Lecturer.Id
        };
        var questions = Enumerable.Range(1, 2).Select(index => new PracticeQuestion
        {
            CourseId = academic.Course.Id,
            CloId = academic.Clo.Id,
            RubricId = academic.Rubric.Id,
            Title = $"Concurrent question {index}",
            Content = $"Explain concurrency case {index}.",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        }).ToArray();
        var secret = TotpPasswordHelper.GenerateSecretKey(32);
        var shift = new InClassExamShift
        {
            Class = @class,
            LecturerId = academic.Lecturer.Id,
            ShiftName = "Concurrent shift",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "IN_PROGRESS",
            TotalQuestionsPerStudent = seedJoinedParticipant ? 2 : 1,
            TotpSecretKey = secret
        };
        var students = Enumerable.Range(0, joiningStudents).Select(index => new User
        {
            Email = $"{suffix}-{index}@student.example.test",
            FullName = $"Concurrent Student {index}",
            Role = UserRole.Student
        }).ToArray();
        context.AddRange(questions);
        context.AddRange(students);
        context.AddRange(students.Select(student => new ClassEnrollment
        {
            Class = @class, Student = student, Status = "enrolled"
        }));
        context.Add(shift);

        if (seedExistingQuestionUsage)
        {
            var existing = new User
            {
                Email = $"{suffix}-existing@student.example.test",
                FullName = "Existing Participant",
                Role = UserRole.Student
            };
            context.Add(new InClassShiftParticipant
            {
                Shift = shift,
                Student = existing,
                AssignedQuestion = questions[0],
                AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { questions[0].Id }),
                Status = "JOINED"
            });
        }

        if (seedJoinedParticipant)
        {
            context.Add(new InClassShiftParticipant
            {
                Shift = shift,
                Student = students.Single(),
                AssignedQuestion = questions[0],
                AssignedQuestionIdsJson = JsonSerializer.Serialize(questions.Select(question => question.Id)),
                Status = "JOINED"
            });
        }

        await context.SaveChangesAsync();
        return new ShiftData(
            shift.Id,
            students.Select(student => student.Id).ToArray(),
            questions.Select(question => question.Id).ToArray(),
            TotpPasswordHelper.GenerateCode(secret));
    }

    private sealed record ShiftData(Guid ShiftId, Guid[] JoiningStudentIds, Guid[] QuestionIds, string OtpCode);

    private sealed class MutationBarrier(string tableName, int expectedArrivals) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<int, byte> _guardedConnections = new();
        private int _arrivals;

        public ConcurrentDictionary<int, byte> ConnectionIds { get; } = new();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.OrdinalIgnoreCase))
            {
                var processId = ((NpgsqlConnection)command.Connection!).ProcessID;
                _guardedConnections.TryAdd(processId, 0);
                ConnectionIds.TryAdd(processId, 0);
                await WaitForAllContendersAsync(cancellationToken);
            }

            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains($"INSERT INTO {tableName}", StringComparison.OrdinalIgnoreCase))
            {
                var processId = ((NpgsqlConnection)command.Connection!).ProcessID;
                ConnectionIds.TryAdd(processId, 0);
                if (!_guardedConnections.ContainsKey(processId))
                {
                    await WaitForAllContendersAsync(cancellationToken);
                }
            }

            return result;
        }

        private async Task WaitForAllContendersAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == expectedArrivals)
            {
                _allArrived.TrySetResult();
            }

            await _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
    }

    private sealed class RecordingQueue : IGradingQueueChannel
    {
        public int Count => Tasks.Count;
        public int Capacity => 1000;
        public ConcurrentQueue<GradingTask> Tasks { get; } = new();

        public ValueTask EnqueueAsync(GradingTask task, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Tasks.Enqueue(task);
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<GradingTask> ReadAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
