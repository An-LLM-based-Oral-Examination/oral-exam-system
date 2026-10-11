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

public class NextQuestionAdversarialStressTests
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
            Code = "PRN231",
            Name = "Building Web APIs with .NET 8",
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.adv@fpt.edu.vn",
            FullName = "Nguyễn Đối Kháng",
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
                Title = $"Câu {difficulty} #{i}",
                Content = $"Nội dung {difficulty} #{i}",
                Difficulty = difficulty,
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    [Fact(DisplayName = "ADV-01. Stress Test chuỗi 15 câu với cả 3 mức độ: Kiểm tra cửa sổ trượt không bao giờ có 3 câu cùng mức")]
    public async Task StressTest_SequenceOf15Questions_ShouldNeverHaveThreeConsecutiveQuestionsWithSameDifficulty()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium", "hard" });

        // Cung cấp dồi dào câu hỏi cho cả 3 mức độ (mỗi mức 20 câu để không cạn đề)
        await SeedQuestionsAsync(context, course.Id, "easy", 20);
        await SeedQuestionsAsync(context, course.Id, "medium", 20);
        await SeedQuestionsAsync(context, course.Id, "hard", 20);

        var handler = new NextQuestionCommandHandler(context);
        var obtainedDifficulties = new List<string>();

        // Act: Mô phỏng sinh viên lấy 15 câu liên tiếp, sau mỗi câu nộp câu trả lời để hệ thống ghi nhận lịch sử
        for (int i = 1; i <= 15; i++)
        {
            var command = new NextQuestionCommand(session.Id, student.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue($"Lần bốc thứ {i} phải thành công");
            result.Value.HasMoreQuestions.Should().BeTrue();
            result.Value.Question.Should().NotBeNull();

            var currentDifficulty = result.Value.Question!.Difficulty.ToLowerInvariant();
            obtainedDifficulties.Add(currentDifficulty);

            // Giả lập lưu câu trả lời cho câu vừa nhận được
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

        // Assert: Cửa sổ trượt 3 phần tử (Window check of 3 consecutive elements)
        obtainedDifficulties.Should().HaveCount(15);

        for (int i = 0; i <= obtainedDifficulties.Count - 3; i++)
        {
            var d1 = obtainedDifficulties[i];
            var d2 = obtainedDifficulties[i + 1];
            var d3 = obtainedDifficulties[i + 2];

            var allThreeSame = (d1 == d2 && d2 == d3);
            allThreeSame.Should().BeFalse(
                $"Vi phạm luật chống lặp 3 câu cùng mức tại vị trí [{i}, {i + 1}, {i + 2}]: {d1} -> {d2} -> {d3}");
        }
    }

    [Fact(DisplayName = "ADV-02. Khi 2 câu liền trước là easy trong tổ hợp [easy, medium], câu thứ 3 BẮT BUỘC phải là medium")]
    public async Task AntiConsecutiveRule_WhenTwoPreviousAreEasy_WithEasyAndMedium_NextMustBeMedium()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium" });

        await SeedQuestionsAsync(context, course.Id, "easy", 10);
        await SeedQuestionsAsync(context, course.Id, "medium", 10);

        var easyQuestions = await context.PracticeQuestions
            .Where(q => q.CourseId == course.Id && q.Difficulty == "easy")
            .Take(2)
            .ToListAsync();

        // Giả lập 2 câu liên tiếp gần nhất đã trả lời đều là easy
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestions[0].Id,
            StudentId = student.Id,
            AnswerText = "Trả lời easy 1",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
            IsFollowUp = false
        });
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestions[1].Id,
            StudentId = student.Id,
            AnswerText = "Trả lời easy 2",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-3),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Do cả 2 câu liền trước là easy, câu tiếp theo BẮT BUỘC là medium
        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question!.Difficulty.ToLowerInvariant().Should().Be("medium");
    }

    [Fact(DisplayName = "ADV-03. Khi chỉ chọn đơn 1 mức độ (easy), lấy liên tiếp nhiều câu vẫn hoạt động trơn tru")]
    public async Task SingleDifficulty_ConsecutiveRequests_ShouldWorkWithoutException()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy" });
        await SeedQuestionsAsync(context, course.Id, "easy", 5);

        var handler = new NextQuestionCommandHandler(context);

        // Act & Assert: Lấy liên tiếp 4 câu mà không bị exception
        for (int i = 1; i <= 4; i++)
        {
            var command = new NextQuestionCommand(session.Id, student.Id);
            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.HasMoreQuestions.Should().BeTrue();
            result.Value.Question!.Difficulty.ToLowerInvariant().Should().Be("easy");

            context.PracticeAnswers.Add(new PracticeAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = result.Value.Question.Id,
                StudentId = student.Id,
                AnswerText = $"Đã làm câu easy {i}",
                SubmittedAt = DateTime.UtcNow.AddSeconds(i * 10),
                IsFollowUp = false
            });
            await context.SaveChangesAsync();
        }
    }

    [Fact(DisplayName = "ADV-04. Khi 2 câu liền trước là easy nhưng medium đã cạn sạch, hệ thống fallback cấp tiếp easy thay vì cạn đề giả")]
    public async Task AntiConsecutiveRule_WhenFallbackNeeded_IfMediumExhausted_AllowsRemainingEasy()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium" });

        await SeedQuestionsAsync(context, course.Id, "easy", 5);
        // Không seed câu medium nào (medium cạn sạch)

        var easyQuestions = await context.PracticeQuestions
            .Where(q => q.CourseId == course.Id && q.Difficulty == "easy")
            .Take(2)
            .ToListAsync();

        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestions[0].Id,
            StudentId = student.Id,
            AnswerText = "Trả lời easy 1",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
            IsFollowUp = false
        });
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestions[1].Id,
            StudentId = student.Id,
            AnswerText = "Trả lời easy 2",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-3),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Fallback sang easy còn lại vì non-excluded pool (medium) đã hết
        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question!.Difficulty.ToLowerInvariant().Should().Be("easy");
        result.Value.Question.Id.Should().NotBe(easyQuestions[0].Id);
        result.Value.Question.Id.Should().NotBe(easyQuestions[1].Id);
    }
}
