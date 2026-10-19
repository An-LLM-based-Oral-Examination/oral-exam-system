using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public class StartMockExamCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(Course Course, User Student, ExamStructure Structure, List<PracticeQuestion> PracticeQuestions, List<ExamQuestion> ExamQuestions)> SeedDataAsync(
        OralExamDbContext context,
        int maxMockExamsPerDay = 3,
        int practiceQuestionCount = 10,
        string bloomDistribution = "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}")
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture & Design",
            Credits = 3,
            MaxMockExamsPerDay = maxMockExamsPerDay,
            IsActive = true
        };
        context.Courses.Add(course);

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Nguyễn Văn Sinh Viên",
            Role = "student",
            IsActive = true
        };
        context.Users.Add(student);

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Barem Rubric Kiến Trúc Phần Mềm",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        context.Rubrics.Add(rubric);

        var criterion1 = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Kiến thức lý thuyết Clean Architecture",
            MaxScore = 4.0m,
            Weight = 0.4m,
            OrderIndex = 1
        };
        var criterion2 = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Kỹ năng phân tích thiết kế",
            MaxScore = 6.0m,
            Weight = 0.6m,
            OrderIndex = 2
        };
        context.RubricCriteria.AddRange(criterion1, criterion2);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = student.Id,
            Name = "Cấu trúc đề thi thử SWD392",
            TotalQuestions = 5,
            DurationMinutes = 30,
            BloomDistribution = bloomDistribution,
            IsActive = true
        };
        context.ExamStructures.Add(structure);

        var bloomLevels = new[] { "Remember", "Understand", "Apply", "Analyze", "Evaluate" };
        var practiceQuestions = new List<PracticeQuestion>();
        for (int i = 0; i < practiceQuestionCount; i++)
        {
            var pq = new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                Title = $"Câu hỏi luyện tập #{i + 1}",
                Content = $"Nội dung câu hỏi luyện tập kiến trúc #{i + 1}",
                BloomLevel = bloomLevels[i % bloomLevels.Length],
                Difficulty = (i % 3 == 0) ? "easy" : (i % 3 == 1) ? "medium" : "hard",
                IsActive = true
            };
            practiceQuestions.Add(pq);
            context.PracticeQuestions.Add(pq);
        }

        var examQuestions = new List<ExamQuestion>();
        for (int i = 0; i < 5; i++)
        {
            var eq = new ExamQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                SubmittedBy = student.Id,
                Title = $"CÂU HỎI THI THẬT BẢO MẬT #{i + 1}",
                Content = $"Đề thi thật tuyệt mật phòng lab #{i + 1}",
                BloomLevel = "Analyze",
                Difficulty = "hard",
                ApprovalStatus = "APPROVED",
                IsActive = true
            };
            examQuestions.Add(eq);
            context.ExamQuestions.Add(eq);
        }

        await context.SaveChangesAsync();
        return (course, student, structure, practiceQuestions, examQuestions);
    }

    [Fact(DisplayName = "1. Bốc đề thi thử thành công và trừ 1 lượt hạn ngạch trong ngày (K=3)")]
    public async Task Handle_ValidRequest_CreatesSessionAndDecrementsQuota()
    {
        using var context = CreateDbContext();
        var (course, student, _, _, _) = await SeedDataAsync(context);

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CourseId.Should().Be(course.Id);
        result.Value.CourseCode.Should().Be(course.Code);
        result.Value.HasFollowUp.Should().BeFalse();
        result.Value.DurationMinutes.Should().Be(30);
        result.Value.UsedQuota.Should().Be(1);
        result.Value.RemainingQuota.Should().Be(2);
        result.Value.MaxDailyQuota.Should().Be(3);
        result.Value.Questions.Should().HaveCount(5);

        // Kiểm tra cơ sở dữ liệu đã tạo MockExamSession và Answers
        var session = await context.MockExamSessions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == result.Value.SessionId);

        session.Should().NotBeNull();
        session!.StudentId.Should().Be(student.Id);
        session.CourseId.Should().Be(course.Id);
        session.Status.Should().Be("in_progress");
        session.Answers.Should().HaveCount(5);
        session.Answers.All(a => a.Status == "pending").Should().BeTrue();

        // Kiểm tra Quota
        var quota = await context.MockExamQuotas.FirstOrDefaultAsync(q => q.StudentId == student.Id && q.CourseId == course.Id);
        quota.Should().NotBeNull();
        quota!.UsedCount.Should().Be(1);
    }

    [Fact(DisplayName = "2. Khi bật tùy chọn Follow-up, thời lượng làm bài tăng lên 45 phút")]
    public async Task Handle_ValidRequestWithFollowUp_SetsDurationTo45Minutes()
    {
        using var context = CreateDbContext();
        var (course, student, _, _, _) = await SeedDataAsync(context);

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasFollowUp.Should().BeTrue();
        result.Value.DurationMinutes.Should().Be(45);
        result.Value.ExpiresAt.Should().BeCloseTo(result.Value.StartedAt.AddMinutes(45), TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "3. Chặn thi và trả lỗi QUOTA_EXCEEDED khi sinh viên dùng hết hạn ngạch")]
    public async Task Handle_QuotaExceeded_ReturnsQuotaExceededFailure()
    {
        using var context = CreateDbContext();
        var (course, student, _, _, _) = await SeedDataAsync(context, maxMockExamsPerDay: 3);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var existingQuota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            QuotaDate = todayVn,
            UsedCount = 3,
            UpdatedAt = DateTime.UtcNow
        };
        context.MockExamQuotas.Add(existingQuota);
        await context.SaveChangesAsync();

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("QUOTA_EXCEEDED:");
        result.Error.Should().Contain("3/3 lượt");
    }

    [Fact(DisplayName = "4. Tuân thủ hạn ngạch động do Admin cấu hình trong Course.MaxMockExamsPerDay")]
    public async Task Handle_DynamicQuotaFromCourse_RespectsCustomCourseQuota()
    {
        using var context = CreateDbContext();
        var (course, student, _, _, _) = await SeedDataAsync(context, maxMockExamsPerDay: 5);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var existingQuota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            QuotaDate = todayVn,
            UsedCount = 4,
            UpdatedAt = DateTime.UtcNow
        };
        context.MockExamQuotas.Add(existingQuota);
        await context.SaveChangesAsync();

        var handler = new StartMockExamCommandHandler(context);

        // Lần thi thứ 5: Cho phép (hạn ngạch là 5)
        var cmd1 = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);
        var res1 = await handler.Handle(cmd1, CancellationToken.None);
        res1.IsSuccess.Should().BeTrue();
        res1.Value.UsedQuota.Should().Be(5);
        res1.Value.RemainingQuota.Should().Be(0);

        // Lần thi thứ 6: Bị chặn đứng
        var cmd2 = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);
        var res2 = await handler.Handle(cmd2, CancellationToken.None);
        res2.IsSuccess.Should().BeFalse();
        res2.Error.Should().StartWith("QUOTA_EXCEEDED:");
        res2.Error.Should().Contain("5/5 lượt");
    }

    [Fact(DisplayName = "5. Báo lỗi khi môn học không tồn tại trong hệ thống")]
    public async Task Handle_CourseNotFound_ReturnsFailure()
    {
        using var context = CreateDbContext();
        var nonExistentCourseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(nonExistentCourseId, studentId, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Môn học không tồn tại");
    }

    [Fact(DisplayName = "6. Báo lỗi khi môn học đã bị vô hiệu hóa (IsActive = false)")]
    public async Task Handle_CourseInactive_ReturnsFailure()
    {
        using var context = CreateDbContext();
        var (course, student, _, _, _) = await SeedDataAsync(context);
        course.IsActive = false;
        await context.SaveChangesAsync();

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("vô hiệu hóa");
    }

    [Fact(DisplayName = "7. CÔ LẬP TUYỆT ĐỐI: 100% câu hỏi rút từ practice_questions, 0 câu từ exam_questions")]
    public async Task Handle_IsolateExamQuestions_NeverContainsExamQuestionId()
    {
        using var context = CreateDbContext();
        var (course, student, _, practiceQuestions, examQuestions) = await SeedDataAsync(context);

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var returnedIds = result.Value.Questions.Select(q => q.Id).ToHashSet();

        // Khẳng định 100% câu hỏi nằm trong kho luyện tập
        var practiceIds = practiceQuestions.Select(p => p.Id).ToHashSet();
        returnedIds.All(id => practiceIds.Contains(id)).Should().BeTrue();

        // Khẳng định KHÔNG CÓ BẤT KỲ CÂU NÀO thuộc kho thi thật exam_questions
        var examIds = examQuestions.Select(e => e.Id).ToHashSet();
        returnedIds.Any(id => examIds.Contains(id)).Should().BeFalse();
    }

    [Fact(DisplayName = "8. Fallback thông minh khi kho câu hỏi thiếu mức Bloom theo cấu trúc")]
    public async Task Handle_MissingBloomQuestions_FallbacksToAvailableQuestions()
    {
        using var context = CreateDbContext();
        // Cấu trúc yêu cầu Remember: 1, Understand: 2, Apply: 2
        // Nhưng tạo 5 câu hỏi toàn bộ là "Remember"
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = ".NET 8 Cross Platform",
            MaxMockExamsPerDay = 3,
            IsActive = true
        };
        context.Courses.Add(course);

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student2@fpt.edu.vn",
            FullName = "Sinh Viên Test",
            Role = "student",
            IsActive = true
        };
        context.Users.Add(student);

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric PRN",
            TotalMaxScore = 10m,
            IsActive = true
        };
        context.Rubrics.Add(rubric);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = student.Id,
            Name = "Cấu trúc đề PRN",
            TotalQuestions = 5,
            DurationMinutes = 30,
            BloomDistribution = "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}",
            IsActive = true
        };
        context.ExamStructures.Add(structure);

        // Chỉ có câu Remember
        for (int i = 0; i < 5; i++)
        {
            context.PracticeQuestions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                Title = $"Câu Remember #{i + 1}",
                Content = $"Nội dung #{i + 1}",
                BloomLevel = "Remember",
                Difficulty = "easy",
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        // Dù thiếu Understand và Apply, thuật toán vẫn fallback bù đủ 5 câu
        result.IsSuccess.Should().BeTrue();
        result.Value.Questions.Should().HaveCount(5);
    }

    [Fact(DisplayName = "9. Báo lỗi khi tổng số câu hỏi luyện tập của môn ít hơn cấu trúc yêu cầu")]
    public async Task Handle_InsufficientTotalQuestions_ReturnsBadRequest()
    {
        using var context = CreateDbContext();
        // Chỉ tạo 3 câu hỏi nhưng cấu trúc đòi 5 câu
        var (course, student, _, _, _) = await SeedDataAsync(context, practiceQuestionCount: 3);

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("hiện chỉ có 3 câu, không đủ 5 câu");
    }

    [Fact(DisplayName = "10. Validator chặn đứng khi CourseId hoặc StudentId là Guid.Empty")]
    public void Validator_Should_Fail_When_Ids_Are_Empty()
    {
        var validator = new StartMockExamCommandValidator();

        var invalidCourse = new StartMockExamCommand(Guid.Empty, Guid.NewGuid(), false);
        validator.Validate(invalidCourse).IsValid.Should().BeFalse();

        var invalidStudent = new StartMockExamCommand(Guid.NewGuid(), Guid.Empty, false);
        validator.Validate(invalidStudent).IsValid.Should().BeFalse();

        var valid = new StartMockExamCommand(Guid.NewGuid(), Guid.NewGuid(), true);
        validator.Validate(valid).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "11. Cho phép thi thử hôm nay khi hạn ngạch ngày hôm qua đã dùng hết")]
    public async Task Handle_QuotaExhaustedYesterday_AllowsNewExamToday()
    {
        using var context = CreateDbContext();
        var (course, student, _, _, _) = await SeedDataAsync(context, maxMockExamsPerDay: 3);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var yesterdayVn = todayVn.AddDays(-1);

        var yesterdayQuota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            QuotaDate = yesterdayVn,
            UsedCount = 3,
            UpdatedAt = DateTime.UtcNow
        };
        context.MockExamQuotas.Add(yesterdayQuota);
        await context.SaveChangesAsync();

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UsedQuota.Should().Be(1);
        result.Value.RemainingQuota.Should().Be(2);
    }

    [Theory(DisplayName = "12. Fallback về hạn ngạch mặc định K=3 khi MaxMockExamsPerDay <= 0")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task Handle_ZeroOrNegativeMaxMockExamsPerDay_FallbacksToDefault3(int invalidQuota)
    {
        using var context = CreateDbContext();
        var (course, student, _, _, _) = await SeedDataAsync(context, maxMockExamsPerDay: invalidQuota);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var existingQuota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            QuotaDate = todayVn,
            UsedCount = 2,
            UpdatedAt = DateTime.UtcNow
        };
        context.MockExamQuotas.Add(existingQuota);
        await context.SaveChangesAsync();

        var handler = new StartMockExamCommandHandler(context);

        // Lần thứ 3: Thành công (vì fallback = 3, used = 2 -> 3)
        var cmd1 = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);
        var res1 = await handler.Handle(cmd1, CancellationToken.None);
        res1.IsSuccess.Should().BeTrue();
        res1.Value.UsedQuota.Should().Be(3);
        res1.Value.RemainingQuota.Should().Be(0);

        // Lần thứ 4: Bị chặn (vượt quá 3)
        var cmd2 = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);
        var res2 = await handler.Handle(cmd2, CancellationToken.None);
        res2.IsSuccess.Should().BeFalse();
        res2.Error.Should().StartWith("QUOTA_EXCEEDED:");
        res2.Error.Should().Contain("3/3 lượt");
    }

    [Fact(DisplayName = "13. Trả về QUOTA_EXCEEDED khi UsedCount lớn hơn MaxDailyQuota")]
    public async Task Handle_UsedCountExceedsMaxDailyQuota_ReturnsQuotaExceededFailure()
    {
        using var context = CreateDbContext();
        var (course, student, _, _, _) = await SeedDataAsync(context, maxMockExamsPerDay: 3);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var excessQuota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            QuotaDate = todayVn,
            UsedCount = 5,
            UpdatedAt = DateTime.UtcNow
        };
        context.MockExamQuotas.Add(excessQuota);
        await context.SaveChangesAsync();

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("QUOTA_EXCEEDED:");
        result.Error.Should().Contain("3/3 lượt");
    }
}
