using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Hubs;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;

namespace OralExamination.UnitTests.Features.DeadLetterQueue;

public class GradingQueueWorkerB1HardeningTests
{
    [Fact(DisplayName = "B1-01: MF-01 GradingQueueWorker đóng dấu ProcessingStartedAt trước khi gọi LLM và bộ quét 3p/30s không đẩy trùng bài đang chấm")]
    public async Task Mf01_GradingQueueWorker_StampsProcessingStartedAtOnClaim_AndActiveJobIsNotReEnqueued()
    {
        var dbName = $"b1-mf01-claim-{Guid.NewGuid():N}";
        var student = new User { Id = Guid.NewGuid(), Email = "sv-b101@fpt.edu.vn", FullName = "SV B101", StudentCode = "SE181001", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric B1", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Core", MaxScore = 10m, Weight = 1.0m, OrderIndex = 1 };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Explain Middleware",
            Content = "Explain ASP.NET Core Middleware pipeline",
            SampleAnswer = new string('A', 60),
            KeyPoints = "[]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-6),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var answer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = "Middleware handles HTTP requests in sequence.",
            Status = "pending",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessingStartedAt = null
        };

        var llmEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLlm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (AiGradingRequest _, CancellationToken ct) =>
            {
                llmEntered.TrySetResult();
                await releaseLlm.Task.WaitAsync(ct);
                return new AiGradingResult
                {
                    Score = 8.5m,
                    ConfidenceScore = 0.94m,
                    Feedback = "Good explanation",
                    Details = [new GradingDetailDto { CriterionId = criterion.Id.ToString(), Score = 8.5m, Comment = "Solid" }]
                };
            });

        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        services.AddSingleton(aiMock.Object);

        await using var provider = services.BuildServiceProvider();
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.Add(student);
            db.Courses.Add(course);
            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(criterion);
            db.PracticeQuestions.Add(question);
            db.PracticeSessions.Add(session);
            db.PracticeAnswers.Add(answer);
            await db.SaveChangesAsync();
        }

        var hubMock = CreatePracticeHubMock(session.Id);
        var queueChannel = new InMemoryTestGradingChannel();
        var worker = new GradingQueueWorker(queueChannel, provider, hubMock.Object, NullLogger<GradingQueueWorker>.Instance);

        var beforeStart = DateTime.UtcNow.AddSeconds(-2);
        await worker.StartAsync(CancellationToken.None);
        await queueChannel.EnqueueAsync(new GradingTask
        {
            AnswerId = answer.Id,
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = answer.AnswerText,
            IsFollowUp = false,
            IsFullSession = false
        });

        await llmEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // 1. Khi LLM đang chấm, ProcessingStartedAt đã được đóng dấu trong DB
        using (var midScope = provider.CreateScope())
        {
            var midDb = midScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var inFlightAnswer = await midDb.PracticeAnswers.AsNoTracking().SingleAsync(a => a.Id == answer.Id);
            inFlightAnswer.Status.Should().Be("pending");
            inFlightAnswer.ProcessingStartedAt.Should().NotBeNull();
            inFlightAnswer.ProcessingStartedAt!.Value.Should().BeOnOrAfter(beforeStart);
        }

        // 2. Bộ quét cứu hộ 3p/30s chạy ngay lúc đang chấm KHÔNG được đẩy trùng bài dù SubmittedAt > 3p
        var recoveryChannelMock = new Mock<IGradingQueueChannel>();
        var replayWorker = new OfficialExamDlqReplayWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            recoveryChannelMock.Object);
        await replayWorker.RecoverInterruptedAsync(CancellationToken.None);
        recoveryChannelMock.Verify(
            c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()),
            Times.Never);

        releaseLlm.TrySetResult();
        queueChannel.Complete();
        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await worker.StopAsync(CancellationToken.None);

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var finalAnswer = await verifyDb.PracticeAnswers.AsNoTracking().SingleAsync(a => a.Id == answer.Id);
        finalAnswer.Status.Should().Be("graded");
        (await verifyDb.AiEvaluations.CountAsync(e => e.PracticeAnswerId == answer.Id)).Should().Be(1);
        aiMock.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "B1-02: MF-05 GradingQueueWorker đóng dấu ProcessingStartedAt trước khi gọi LLM và bộ quét 3p/30s không đẩy trùng bài đang chấm")]
    public async Task Mf05_GradingQueueWorker_StampsProcessingStartedAtOnClaim_AndActiveJobIsNotReEnqueued()
    {
        var dbName = $"b1-mf05-claim-{Guid.NewGuid():N}";
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-b102@fpt.edu.vn", FullName = "GV B102", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-b102@fpt.edu.vn", FullName = "SV B102", StudentCode = "SE181002", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1802", Name = "SE1802", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric B102", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Architecture", MaxScore = 10m, Weight = 1.0m, OrderIndex = 1 };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Clean Architecture",
            Content = "Explain Dependency Rule",
            SampleAnswer = new string('B', 60),
            KeyPoints = "[]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca B102",
            TotpSecretKey = "SECRET_B102",
            Status = "IN_PROGRESS",
            TotalQuestionsPerStudent = 1,
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            EndTime = DateTime.UtcNow.AddMinutes(15)
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { question.Id }),
            Status = "SUBMITTED",
            JoinedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Inner layers must not depend on outer infrastructure layers.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessingStartedAt = null
        };

        var llmEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLlm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (AiGradingRequest _, CancellationToken ct) =>
            {
                llmEntered.TrySetResult();
                await releaseLlm.Task.WaitAsync(ct);
                return new AiGradingResult
                {
                    Score = 9.0m,
                    ConfidenceScore = 0.96m,
                    Feedback = "Accurate",
                    Details = [new GradingDetailDto { CriterionId = criterion.Id.ToString(), Score = 9.0m, Comment = "Clear" }]
                };
            });

        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        services.AddSingleton(aiMock.Object);

        await using var provider = services.BuildServiceProvider();
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.AddRange(lecturer, student);
            db.Courses.Add(course);
            db.Classes.Add(cls);
            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(criterion);
            db.PracticeQuestions.Add(question);
            db.InClassExamShifts.Add(shift);
            db.InClassShiftParticipants.Add(participant);
            db.InClassExamSubmissions.Add(submission);
            await db.SaveChangesAsync();
        }

        var queueChannel = new InMemoryTestGradingChannel();
        var worker = new GradingQueueWorker(
            queueChannel,
            provider,
            new Mock<IHubContext<PracticeHub, IPracticeClient>>().Object,
            NullLogger<GradingQueueWorker>.Instance);

        var beforeStart = DateTime.UtcNow.AddSeconds(-2);
        await worker.StartAsync(CancellationToken.None);
        await queueChannel.EnqueueAsync(new GradingTask
        {
            AnswerId = submission.Id,
            InClassSubmissionId = submission.Id,
            SessionId = shift.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = submission.Transcript
        });

        await llmEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using (var midScope = provider.CreateScope())
        {
            var midDb = midScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var inFlightSub = await midDb.InClassExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
            inFlightSub.Status.Should().Be("SUBMITTED");
            inFlightSub.ProcessingStartedAt.Should().NotBeNull();
            inFlightSub.ProcessingStartedAt!.Value.Should().BeOnOrAfter(beforeStart);
        }

        var recoveryChannelMock = new Mock<IGradingQueueChannel>();
        var replayWorker = new OfficialExamDlqReplayWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            recoveryChannelMock.Object);
        await replayWorker.RecoverInterruptedAsync(CancellationToken.None);
        recoveryChannelMock.Verify(
            c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()),
            Times.Never);

        releaseLlm.TrySetResult();
        queueChannel.Complete();
        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await worker.StopAsync(CancellationToken.None);

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var finalSub = await verifyDb.InClassExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
        finalSub.Status.Should().Be("AI_GRADED");
        finalSub.AiScore.Should().Be(9.0m);
        aiMock.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "B1-03: Bộ quét cứu hộ chỉ thu hồi bài có ProcessingStartedAt quá ngưỡng 3 phút và bỏ qua bài đang hoạt động trong cửa sổ")]
    public async Task OfficialExamDlqReplayWorker_OnlyRecoversStaleJobsBeyondCutoff_NotActiveInFlightJobs()
    {
        var dbName = $"b1-cutoff-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        await using var provider = services.BuildServiceProvider();

        var now = DateTime.UtcNow;
        var stalePracticeId = Guid.NewGuid();
        var activePracticeId = Guid.NewGuid();
        var staleInClassId = Guid.NewGuid();
        var activeInClassId = Guid.NewGuid();

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var sessionId = Guid.NewGuid();
            db.PracticeSessions.Add(new PracticeSession
            {
                Id = sessionId,
                StudentId = Guid.NewGuid(),
                CourseId = Guid.NewGuid(),
                PracticeMode = "per_question",
                Status = "in_progress"
            });
            db.PracticeAnswers.AddRange(
                new PracticeAnswer
                {
                    Id = stalePracticeId,
                    SessionId = sessionId,
                    QuestionId = Guid.NewGuid(),
                    StudentId = Guid.NewGuid(),
                    AnswerText = "Stale practice",
                    Status = "pending",
                    SubmittedAt = now.AddMinutes(-10),
                    ProcessingStartedAt = now.AddMinutes(-4)
                },
                new PracticeAnswer
                {
                    Id = activePracticeId,
                    SessionId = sessionId,
                    QuestionId = Guid.NewGuid(),
                    StudentId = Guid.NewGuid(),
                    AnswerText = "Active practice",
                    Status = "pending",
                    SubmittedAt = now.AddMinutes(-10),
                    ProcessingStartedAt = now.AddMinutes(-1)
                });
            db.InClassExamSubmissions.AddRange(
                new InClassExamSubmission
                {
                    Id = staleInClassId,
                    ShiftId = Guid.NewGuid(),
                    StudentId = Guid.NewGuid(),
                    QuestionId = Guid.NewGuid(),
                    Transcript = "Stale in-class",
                    Status = "SUBMITTED",
                    SubmittedAt = now.AddMinutes(-10),
                    ProcessingStartedAt = now.AddMinutes(-4)
                },
                new InClassExamSubmission
                {
                    Id = activeInClassId,
                    ShiftId = Guid.NewGuid(),
                    StudentId = Guid.NewGuid(),
                    QuestionId = Guid.NewGuid(),
                    Transcript = "Active in-class",
                    Status = "SUBMITTED",
                    SubmittedAt = now.AddMinutes(-10),
                    ProcessingStartedAt = now.AddMinutes(-1)
                });
            await db.SaveChangesAsync();
        }

        var enqueued = new List<GradingTask>();
        var channelMock = new Mock<IGradingQueueChannel>();
        channelMock.Setup(c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Callback<GradingTask, CancellationToken>((task, _) => enqueued.Add(task))
            .Returns(ValueTask.CompletedTask);

        var replayWorker = new OfficialExamDlqReplayWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            channelMock.Object);

        await replayWorker.RecoverInterruptedAsync(CancellationToken.None);

        enqueued.Should().HaveCount(2);
        enqueued.Select(t => t.AnswerId).Should().BeEquivalentTo([stalePracticeId, staleInClassId]);
        enqueued.Select(t => t.AnswerId).Should().NotContain([activePracticeId, activeInClassId]);
    }

    [Fact(DisplayName = "B1-04: MF-01 xử lý xung đột unique 23505 (uq_ai_evaluations_practice_answer) lũy đẳng, không lặp Polly retry và không làm bẩn DLQ")]
    public async Task Mf01_GradingQueueWorker_Handles23505RaceIdempotently_WithoutDlqPollutionOrDuplicates()
    {
        var dbName = $"b1-mf01-23505-{Guid.NewGuid():N}";
        var student = new User { Id = Guid.NewGuid(), Email = "sv-b104@fpt.edu.vn", FullName = "SV B104", StudentCode = "SE181004", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric B104", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Core", MaxScore = 10m, Weight = 1.0m, OrderIndex = 1 };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Explain DI",
            Content = "Explain Dependency Injection lifetimes",
            SampleAnswer = new string('C', 60),
            KeyPoints = "[]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            PracticeMode = "per_question",
            Status = "in_progress"
        };
        var answer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = "Transient, Scoped, Singleton",
            Status = "pending",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-4)
        };
        var pendingDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            CorrelationId = answer.Id,
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Previous transient timeout",
            RetryCount = 3,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-3)
        };

        var raceSimulated = 0;
        var reconciliationCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var baseOptions = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 7.5m,
                ConfidenceScore = 0.90m,
                Feedback = "Late worker result",
                Details = [new GradingDetailDto { CriterionId = criterion.Id.ToString(), Score = 7.5m, Comment = "Late" }]
            });

        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => new RacingPracticeEvaluationDbContext(
            baseOptions,
            answer.Id,
            student.Id,
            criterion.Id,
            () => Interlocked.Exchange(ref raceSimulated, 1) == 0,
            () => reconciliationCompleted.TrySetResult(true)));
        services.AddSingleton(aiMock.Object);

        await using var provider = services.BuildServiceProvider();
        await using (var seedDb = new OralExamDbContext(baseOptions))
        {
            seedDb.Users.Add(student);
            seedDb.Courses.Add(course);
            seedDb.Rubrics.Add(rubric);
            seedDb.RubricCriteria.Add(criterion);
            seedDb.PracticeQuestions.Add(question);
            seedDb.PracticeSessions.Add(session);
            seedDb.PracticeAnswers.Add(answer);
            seedDb.DeadLetterQueues.Add(pendingDlq);
            await seedDb.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GradingQueue:RetryDelayMilliseconds"] = "0",
                ["GradingQueue:MaxDegreeOfParallelism"] = "1"
            })
            .Build();
        var hubMock = CreatePracticeHubMock(session.Id);
        var queueChannel = new InMemoryTestGradingChannel();
        var worker = new GradingQueueWorker(queueChannel, provider, hubMock.Object, NullLogger<GradingQueueWorker>.Instance, config);

        await worker.StartAsync(CancellationToken.None);
        await queueChannel.EnqueueAsync(new GradingTask
        {
            AnswerId = answer.Id,
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = answer.AnswerText,
            IsFollowUp = false,
            IsFullSession = false
        });
        queueChannel.Complete();

        await reconciliationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(30));
        }
        await worker.StopAsync(CancellationToken.None);

        // Assert: Xử lý 23505 lũy đẳng -> không retry LLM, không đẩy thêm DLQ, chỉ có duy nhất 1 AiEvaluation và bài ở trạng thái graded
        aiMock.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        await using var verifyDb = new OralExamDbContext(baseOptions);
        var finalAnswer = await verifyDb.PracticeAnswers.AsNoTracking().SingleAsync(a => a.Id == answer.Id);
        finalAnswer.Status.Should().Be("graded");
        finalAnswer.ProcessingStartedAt.Should().NotBeNull();

        var evaluations = await verifyDb.AiEvaluations.AsNoTracking()
            .Where(e => e.PracticeAnswerId == answer.Id)
            .ToListAsync();
        evaluations.Should().ContainSingle();
        evaluations[0].TotalScore.Should().Be(8.0m);

        var dlqs = await verifyDb.DeadLetterQueues.AsNoTracking()
            .Where(d => d.CorrelationId == answer.Id)
            .ToListAsync();
        dlqs.Should().ContainSingle();
        dlqs[0].Status.Should().Be("resolved");
    }

    [Fact(DisplayName = "B1-05: GradingQueueWorker khử trùng lặp tác vụ đang chạy đồng thời trong cùng tiến trình (không gọi LLM 2 lần)")]
    public async Task GradingQueueWorker_DeduplicatesConcurrentInFlightTasks_CallingLlmOnlyOnce()
    {
        var dbName = $"b1-dedupe-{Guid.NewGuid():N}";
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-b105@fpt.edu.vn", FullName = "GV B105", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-b105@fpt.edu.vn", FullName = "SV B105", StudentCode = "SE181005", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1805", Name = "SE1805", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric B105", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Core", MaxScore = 10m, Weight = 1.0m, OrderIndex = 1 };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Explain EF Core",
            Content = "Explain change tracking",
            SampleAnswer = new string('D', 60),
            KeyPoints = "[]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca B105",
            TotpSecretKey = "SECRET_B105",
            Status = "IN_PROGRESS",
            TotalQuestionsPerStudent = 1,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(20)
        };
        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "AsNoTracking disables change tracking for read-only queries.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-2)
        };

        var llmEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLlm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (AiGradingRequest _, CancellationToken ct) =>
            {
                llmEntered.TrySetResult();
                await releaseLlm.Task.WaitAsync(ct);
                return new AiGradingResult
                {
                    Score = 8.0m,
                    ConfidenceScore = 0.92m,
                    Feedback = "Good",
                    Details = [new GradingDetailDto { CriterionId = criterion.Id.ToString(), Score = 8.0m, Comment = "Good" }]
                };
            });

        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        services.AddSingleton(aiMock.Object);

        await using var provider = services.BuildServiceProvider();
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.AddRange(lecturer, student);
            db.Courses.Add(course);
            db.Classes.Add(cls);
            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(criterion);
            db.PracticeQuestions.Add(question);
            db.InClassExamShifts.Add(shift);
            db.InClassExamSubmissions.Add(submission);
            await db.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GradingQueue:MaxDegreeOfParallelism"] = "4" })
            .Build();
        var queueChannel = new InMemoryTestGradingChannel();
        var worker = new GradingQueueWorker(
            queueChannel,
            provider,
            new Mock<IHubContext<PracticeHub, IPracticeClient>>().Object,
            NullLogger<GradingQueueWorker>.Instance,
            config);

        var duplicateTask = new GradingTask
        {
            AnswerId = submission.Id,
            InClassSubmissionId = submission.Id,
            SessionId = shift.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = submission.Transcript
        };

        await worker.StartAsync(CancellationToken.None);
        await queueChannel.EnqueueAsync(duplicateTask);
        await llmEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Đẩy thêm 2 bản sao trùng ID ngay trong lúc tác vụ đầu đang chạy trong LLM
        await queueChannel.EnqueueAsync(duplicateTask);
        await queueChannel.EnqueueAsync(duplicateTask);
        queueChannel.Complete();

        // Chờ đủ lâu để các worker song song lấy hết 2 bản sao ra khỏi channel
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (queueChannel.Count > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(15);
        }

        releaseLlm.TrySetResult();
        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await worker.StopAsync(CancellationToken.None);

        aiMock.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        (await verifyDb.DeadLetterQueues.CountAsync()).Should().Be(0);
        (await verifyDb.InClassExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id)).Status.Should().Be("AI_GRADED");
    }

    private static Mock<IHubContext<PracticeHub, IPracticeClient>> CreatePracticeHubMock(Guid sessionId)
    {
        var hubMock = new Mock<IHubContext<PracticeHub, IPracticeClient>>();
        var clientsMock = new Mock<IHubClients<IPracticeClient>>();
        var clientMock = new Mock<IPracticeClient>();
        clientMock.Setup(c => c.ReceiveGradingResult(It.IsAny<PracticeAnswerDetailDto>())).Returns(Task.CompletedTask);
        clientMock.Setup(c => c.ReceiveGradingError(It.IsAny<Guid>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        clientsMock.Setup(c => c.Group($"session_{sessionId}")).Returns(clientMock.Object);
        hubMock.SetupGet(h => h.Clients).Returns(clientsMock.Object);
        return hubMock;
    }

    private sealed class InMemoryTestGradingChannel : IGradingQueueChannel
    {
        private readonly Channel<GradingTask> _channel = Channel.CreateUnbounded<GradingTask>();

        public int Count => _channel.Reader.Count;
        public int Capacity => 1000;

        public ValueTask EnqueueAsync(GradingTask task, CancellationToken cancellationToken = default) =>
            _channel.Writer.WriteAsync(task, cancellationToken);

        public void Complete() => _channel.Writer.TryComplete();

        public async IAsyncEnumerable<GradingTask> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }
        }
    }

    private sealed class FakeSqlStateException(string sqlState) : Exception($"{sqlState}: duplicate key value violates unique constraint \"uq_ai_evaluations_practice_answer\"")
    {
        public string SqlState { get; } = sqlState;
    }

    private sealed class RacingPracticeEvaluationDbContext : OralExamDbContext
    {
        private readonly DbContextOptions<OralExamDbContext> _options;
        private readonly Guid _targetAnswerId;
        private readonly Guid _studentId;
        private readonly Guid _criterionId;
        private readonly Func<bool> _shouldTriggerRace;
        private readonly Action? _onReconciliationCompleted;

        public RacingPracticeEvaluationDbContext(
            DbContextOptions<OralExamDbContext> options,
            Guid targetAnswerId,
            Guid studentId,
            Guid criterionId,
            Func<bool> shouldTriggerRace,
            Action? onReconciliationCompleted = null)
            : base(options)
        {
            _options = options;
            _targetAnswerId = targetAnswerId;
            _studentId = studentId;
            _criterionId = criterionId;
            _shouldTriggerRace = shouldTriggerRace;
            _onReconciliationCompleted = onReconciliationCompleted;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var hasAddedTargetEval = ChangeTracker.Entries<AiEvaluation>()
                .Any(e => e.State == EntityState.Added && e.Entity.PracticeAnswerId == _targetAnswerId);

            if (hasAddedTargetEval && _shouldTriggerRace())
            {
                await using (var winnerDb = new OralExamDbContext(_options))
                {
                    winnerDb.AiEvaluations.Add(new AiEvaluation
                    {
                        Id = Guid.NewGuid(),
                        AnswerType = "practice",
                        StudentId = _studentId,
                        PracticeAnswerId = _targetAnswerId,
                        TotalScore = 8.0m,
                        ConfidenceScore = 0.95m,
                        Feedback = "Winner worker evaluation",
                        EvaluatedAt = DateTime.UtcNow,
                        Details =
                        [
                            new AiEvaluationDetail
                            {
                                Id = Guid.NewGuid(),
                                CriterionId = _criterionId,
                                Score = 8.0m,
                                Comment = "Winner detail"
                            }
                        ]
                    });
                    await winnerDb.SaveChangesAsync(cancellationToken);
                }

                throw new DbUpdateException(
                    "Simulated concurrent unique violation on uq_ai_evaluations_practice_answer",
                    new FakeSqlStateException("23505"));
            }

            var resolvingTargetDlq = ChangeTracker.Entries<DlqEntity>()
                .Any(e => e.Entity.CorrelationId == _targetAnswerId &&
                          string.Equals(e.Entity.Status, "resolved", StringComparison.OrdinalIgnoreCase));

            var saved = await base.SaveChangesAsync(cancellationToken);
            if (resolvingTargetDlq)
            {
                _onReconciliationCompleted?.Invoke();
            }

            return saved;
        }
    }
}
