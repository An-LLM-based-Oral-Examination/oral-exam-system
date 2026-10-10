using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class NextQuestionAdversarialChallengerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(Course Course, User Student, PracticeSession Session)> SeedSessionAsync(
        OralExamDbContext context,
        List<string> selectedDifficulties)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWT301_ADV",
            Name = "Software Testing - Adversarial Suite",
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "challenger1@fpt.edu.vn",
            FullName = "Challenger 1 Empirical Tester",
            Role = UserRole.Student,
            IsActive = true
        };

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            Status = "in_progress",
            PracticeMode = "per_question",
            SelectedDifficulties = JsonSerializer.Serialize(selectedDifficulties)
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);

        context.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            Key = "SessionInactivityTimeoutMinutes",
            Value = "10",
            Description = "Timeout luyện tập không tương tác"
        });

        await context.SaveChangesAsync();
        return (course, student, session);
    }

    private async Task SeedQuestionsAsync(OralExamDbContext context, Guid courseId, string difficulty, int count)
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = $"Rubric {difficulty}",
            TotalMaxScore = 10.0m
        };
        context.Rubrics.Add(rubric);

        for (int i = 1; i <= count; i++)
        {
            context.PracticeQuestions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                RubricId = rubric.Id,
                Title = $"Question {difficulty} #{i}",
                Content = $"Adversarial content {difficulty} #{i}",
                Difficulty = difficulty,
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    [Fact(DisplayName = "CHALLENGE-01. Monte Carlo: 100 phiên độc lập x 20 câu với cả 3 mức độ - Tuyệt đối không bao giờ có 3 câu liên tiếp cùng mức")]
    public async Task EmpiricalStress_100Sessions_3Difficulties_NeverThreeConsecutiveSameDifficulty()
    {
        for (int sessionIndex = 1; sessionIndex <= 100; sessionIndex++)
        {
            using var context = CreateDbContext();
            var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium", "hard" });

            // Cung cấp mỗi mức 25 câu (tổng 75 câu)
            await SeedQuestionsAsync(context, course.Id, "easy", 25);
            await SeedQuestionsAsync(context, course.Id, "medium", 25);
            await SeedQuestionsAsync(context, course.Id, "hard", 25);

            var handler = new NextQuestionCommandHandler(context);
            var history = new List<string>();

            for (int q = 1; q <= 20; q++)
            {
                var command = new NextQuestionCommand(session.Id, student.Id);
                var result = await handler.Handle(command, CancellationToken.None);

                result.IsSuccess.Should().BeTrue($"Phiên #{sessionIndex}, câu #{q} phải thành công");
                result.Value.HasMoreQuestions.Should().BeTrue();
                result.Value.Question.Should().NotBeNull();

                var diff = result.Value.Question!.Difficulty.ToLowerInvariant();
                history.Add(diff);

                // Nộp câu trả lời để hệ thống cập nhật lịch sử
                context.PracticeAnswers.Add(new PracticeAnswer
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    QuestionId = result.Value.Question.Id,
                    StudentId = student.Id,
                    AnswerText = $"Trả lời câu {q} trong session {sessionIndex}",
                    SubmittedAt = DateTime.UtcNow.AddSeconds(q * 15),
                    IsFollowUp = false
                });
                await context.SaveChangesAsync();
            }

            // Kiểm tra cửa sổ trượt 3 phần tử cho toàn bộ 20 câu của session
            for (int i = 0; i <= history.Count - 3; i++)
            {
                var d1 = history[i];
                var d2 = history[i + 1];
                var d3 = history[i + 2];

                var isThreeSame = (d1 == d2 && d2 == d3);
                isThreeSame.Should().BeFalse(
                    $"VI PHẠM TẠI Session #{sessionIndex}, vị trí [{i}, {i + 1}, {i + 2}]: {d1} -> {d2} -> {d3}");
            }
        }
    }

    [Fact(DisplayName = "CHALLENGE-02. Edge Case: Chọn đơn 1 mức độ (hard) - Bốc đúng, không lỗi, không lặp và cạn đề sạch sẽ")]
    public async Task EmpiricalEdgeCase_SingleDifficulty_OnlyReturnsThatDifficultyAndExhaustsCleanly()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "hard" });

        const int poolCount = 5;
        await SeedQuestionsAsync(context, course.Id, "hard", poolCount);
        // Thêm câu easy và medium để xác nhận không bao giờ bị bốc nhầm
        await SeedQuestionsAsync(context, course.Id, "easy", 10);
        await SeedQuestionsAsync(context, course.Id, "medium", 10);

        var handler = new NextQuestionCommandHandler(context);
        var returnedIds = new HashSet<Guid>();

        for (int i = 1; i <= poolCount; i++)
        {
            var command = new NextQuestionCommand(session.Id, student.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.HasMoreQuestions.Should().BeTrue();
            result.Value.Question.Should().NotBeNull();
            result.Value.Question!.Difficulty.ToLowerInvariant().Should().Be("hard");

            returnedIds.Add(result.Value.Question.Id).Should().BeTrue($"Câu hỏi ID {result.Value.Question.Id} không được lặp lại");

            context.PracticeAnswers.Add(new PracticeAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = result.Value.Question.Id,
                StudentId = student.Id,
                AnswerText = $"Trả lời hard #{i}",
                SubmittedAt = DateTime.UtcNow.AddSeconds(i * 10),
                IsFollowUp = false
            });
            await context.SaveChangesAsync();
        }

        // Lần thứ 6: Kho hard đã cạn sạch
        var finalResult = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        finalResult.IsSuccess.Should().BeTrue();
        finalResult.Value.HasMoreQuestions.Should().BeFalse();
        finalResult.Value.Question.Should().BeNull();
        finalResult.Value.Message.Should().Contain("Đã hoàn thành toàn bộ câu hỏi khả dụng");
    }

    [Fact(DisplayName = "CHALLENGE-03. Edge Case: Cạn câu ở 1 mức độ (easy chỉ 2 câu) - Tiếp tục trơn tru với các mức còn lại và bảo đảm luật chống lặp")]
    public async Task EmpiricalEdgeCase_OneDifficultyExhaustedEarly_GracefullyContinuesWithout3ConsecutiveInRemaining()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium", "hard" });

        // Easy chỉ có 2 câu, Medium và Hard dồi dào
        await SeedQuestionsAsync(context, course.Id, "easy", 2);
        await SeedQuestionsAsync(context, course.Id, "medium", 12);
        await SeedQuestionsAsync(context, course.Id, "hard", 12);

        var handler = new NextQuestionCommandHandler(context);
        var history = new List<string>();

        for (int i = 1; i <= 15; i++)
        {
            var command = new NextQuestionCommand(session.Id, student.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.HasMoreQuestions.Should().BeTrue();
            result.Value.Question.Should().NotBeNull();

            var diff = result.Value.Question!.Difficulty.ToLowerInvariant();
            history.Add(diff);

            context.PracticeAnswers.Add(new PracticeAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = result.Value.Question.Id,
                StudentId = student.Id,
                AnswerText = $"Trả lời câu {i}",
                SubmittedAt = DateTime.UtcNow.AddSeconds(i * 10),
                IsFollowUp = false
            });
            await context.SaveChangesAsync();
        }

        // Đảm bảo số câu easy xuất hiện không vượt quá 2 câu có sẵn
        history.Count(d => d == "easy").Should().BeLessOrEqualTo(2);

        // Sau khi easy cạn, các câu còn lại (medium và hard) vẫn không bao giờ có 3 câu liên tiếp cùng mức
        for (int i = 0; i <= history.Count - 3; i++)
        {
            var d1 = history[i];
            var d2 = history[i + 1];
            var d3 = history[i + 2];

            var isThreeSame = (d1 == d2 && d2 == d3);
            isThreeSame.Should().BeFalse(
                $"Vi phạm 3 câu liên tiếp cùng mức tại [{i}, {i + 1}, {i + 2}]: {d1} -> {d2} -> {d3}");
        }
    }

    [Fact(DisplayName = "CHALLENGE-04. Edge Case: Tất cả các mức cạn sạch câu - Trả về HasMoreQuestions = false và message chuẩn")]
    public async Task EmpiricalEdgeCase_AllQuestionsExhausted_ReturnsHasMoreQuestionsFalseImmediately()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium" });

        // Chỉ có đúng 1 easy và 1 medium
        await SeedQuestionsAsync(context, course.Id, "easy", 1);
        await SeedQuestionsAsync(context, course.Id, "medium", 1);

        var handler = new NextQuestionCommandHandler(context);

        // Lấy câu 1
        var r1 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r1.IsSuccess.Should().BeTrue();
        r1.Value.HasMoreQuestions.Should().BeTrue();
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = r1.Value.Question!.Id,
            StudentId = student.Id,
            AnswerText = "Ans 1",
            SubmittedAt = DateTime.UtcNow.AddSeconds(10),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        // Lấy câu 2
        var r2 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r2.IsSuccess.Should().BeTrue();
        r2.Value.HasMoreQuestions.Should().BeTrue();
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = r2.Value.Question!.Id,
            StudentId = student.Id,
            AnswerText = "Ans 2",
            SubmittedAt = DateTime.UtcNow.AddSeconds(20),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        // Lấy câu 3: Cạn sạch
        var r3 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r3.IsSuccess.Should().BeTrue();
        r3.Value.HasMoreQuestions.Should().BeFalse();
        r3.Value.Question.Should().BeNull();
        r3.Value.Message.Should().Be("Đã hoàn thành toàn bộ câu hỏi khả dụng theo mức độ đã chọn.");

        // Gọi thêm lần 4: Vẫn trả về cạn sạch an toàn
        var r4 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r4.IsSuccess.Should().BeTrue();
        r4.Value.HasMoreQuestions.Should().BeFalse();
        r4.Value.Question.Should().BeNull();
    }

    [Fact(DisplayName = "CHALLENGE-05. Edge Case: Không lặp lại câu hỏi đã làm - 25 câu liên tiếp toàn bộ là ID độc nhất")]
    public async Task EmpiricalEdgeCase_NonRepeatOfAnsweredQuestions_MassivePoolVerification()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium", "hard" });

        const int totalPool = 25;
        await SeedQuestionsAsync(context, course.Id, "easy", 10);
        await SeedQuestionsAsync(context, course.Id, "medium", 10);
        await SeedQuestionsAsync(context, course.Id, "hard", 5);

        var handler = new NextQuestionCommandHandler(context);
        var seenQuestionIds = new HashSet<Guid>();

        for (int i = 1; i <= totalPool; i++)
        {
            var command = new NextQuestionCommand(session.Id, student.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.HasMoreQuestions.Should().BeTrue();

            var qId = result.Value.Question!.Id;
            seenQuestionIds.Add(qId).Should().BeTrue($"Câu hỏi ID {qId} không được lặp lại tại lần bốc thứ {i}");

            context.PracticeAnswers.Add(new PracticeAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = qId,
                StudentId = student.Id,
                AnswerText = $"Ans #{i}",
                SubmittedAt = DateTime.UtcNow.AddSeconds(i * 10),
                IsFollowUp = false
            });
            await context.SaveChangesAsync();
        }

        seenQuestionIds.Should().HaveCount(totalPool);

        // Lần thứ 26: cạn đề
        var exhaustedResult = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        exhaustedResult.IsSuccess.Should().BeTrue();
        exhaustedResult.Value.HasMoreQuestions.Should().BeFalse();
    }

    [Fact(DisplayName = "CHALLENGE-06. Edge Case: Khi 2 bucket cạn sạch (medium, hard = 0), fallback về easy duy nhất còn lại mà không crash")]
    public async Task EmpiricalStress_WhenTwoBucketsExhausted_FallsBackToFinalBucketWithoutCrash()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium", "hard" });

        // Chỉ có 3 câu easy, medium và hard đều là 0
        await SeedQuestionsAsync(context, course.Id, "easy", 3);

        var handler = new NextQuestionCommandHandler(context);

        // Câu 1: easy
        var r1 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r1.IsSuccess.Should().BeTrue();
        r1.Value.Question!.Difficulty.ToLowerInvariant().Should().Be("easy");
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = r1.Value.Question.Id,
            StudentId = student.Id,
            AnswerText = "Ans 1",
            SubmittedAt = DateTime.UtcNow.AddSeconds(10),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        // Câu 2: easy
        var r2 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r2.IsSuccess.Should().BeTrue();
        r2.Value.Question!.Difficulty.ToLowerInvariant().Should().Be("easy");
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = r2.Value.Question.Id,
            StudentId = student.Id,
            AnswerText = "Ans 2",
            SubmittedAt = DateTime.UtcNow.AddSeconds(20),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        // Câu 3: 2 câu trước đều là easy, nhưng medium và hard cạn sạch!
        // Fallback sang câu easy còn lại thay vì báo cạn đề giả định hoặc throw exception.
        var r3 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r3.IsSuccess.Should().BeTrue();
        r3.Value.HasMoreQuestions.Should().BeTrue();
        r3.Value.Question!.Difficulty.ToLowerInvariant().Should().Be("easy");

        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = r3.Value.Question.Id,
            StudentId = student.Id,
            AnswerText = "Ans 3",
            SubmittedAt = DateTime.UtcNow.AddSeconds(30),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        // Câu 4: Đã hết sạch cả 3 câu easy
        var r4 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r4.IsSuccess.Should().BeTrue();
        r4.Value.HasMoreQuestions.Should().BeFalse();
        r4.Value.Question.Should().BeNull();
    }
}
