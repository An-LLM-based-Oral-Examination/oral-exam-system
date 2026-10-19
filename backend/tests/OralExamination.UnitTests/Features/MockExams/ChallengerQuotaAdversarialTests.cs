using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

/// <summary>
/// Empirical Challenger Adversarial Test Suite for Milestone 3 (MF-02 Mock Exam Quota Guard).
/// Tests GMT+7 date boundaries (17:00 UTC / 00:00 GMT+7), extreme quota values, and concurrency.
/// </summary>
public class ChallengerQuotaAdversarialTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private static async Task<(Course Course, User Student, Rubric Rubric)> SeedCourseAndStudentAsync(
        OralExamDbContext context,
        int maxMockExamsPerDay = 3,
        bool isActive = true)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            MaxMockExamsPerDay = maxMockExamsPerDay,
            IsActive = isActive
        };
        context.Courses.Add(course);

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "challenger_student@fpt.edu.vn",
            FullName = "Challenger Student",
            Role = "student",
            IsActive = true
        };
        context.Users.Add(student);

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric Test",
            TotalMaxScore = 10.0m,
            IsActive = true
        };
        context.Rubrics.Add(rubric);

        var criterion = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Kiến thức",
            MaxScore = 10.0m,
            Weight = 1.0m,
            OrderIndex = 1
        };
        context.RubricCriteria.Add(criterion);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = student.Id,
            Name = "Cấu trúc đề",
            TotalQuestions = 5,
            DurationMinutes = 30,
            BloomDistribution = "{\"Remember\": 5}",
            IsActive = true
        };
        context.ExamStructures.Add(structure);

        for (int i = 0; i < 10; i++)
        {
            context.PracticeQuestions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                Title = $"Câu hỏi {i + 1}",
                Content = $"Nội dung câu hỏi {i + 1}",
                BloomLevel = "Remember",
                Difficulty = "easy",
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
        return (course, student, rubric);
    }

    #region Category 1: GMT+7 Date Boundaries (17:00 UTC / 00:00 GMT+7)

    [Theory(DisplayName = "CHALLENGE-01: Chuyển đổi chính xác ngày GMT+7 tại ranh giới 17:00 UTC (00:00 GMT+7)")]
    [InlineData("2026-10-16T16:59:59.999Z", "2026-10-16")] // 23:59:59.999 GMT+7 -> Oct 16
    [InlineData("2026-10-16T17:00:00.000Z", "2026-10-17")] // 00:00:00.000 GMT+7 -> Oct 17
    [InlineData("2026-10-16T17:00:00.001Z", "2026-10-17")] // 00:00:00.001 GMT+7 -> Oct 17
    [InlineData("2024-02-28T16:59:59.000Z", "2024-02-28")] // Leap year before boundary
    [InlineData("2024-02-28T17:00:00.000Z", "2024-02-29")] // Leap year exact boundary -> Feb 29
    [InlineData("2024-02-29T16:59:59.000Z", "2024-02-29")] // Leap year Feb 29 end
    [InlineData("2024-02-29T17:00:00.000Z", "2024-03-01")] // Transition to March 1
    [InlineData("2026-12-31T16:59:59.000Z", "2026-12-31")] // Year end before boundary
    [InlineData("2026-12-31T17:00:00.000Z", "2027-01-01")] // New Year exact boundary -> Jan 01 2027
    public void DateTime_AddHours7_Produces_Exact_DateOnly_Across_Boundaries(string utcString, string expectedDateString)
    {
        var utcDateTime = DateTime.Parse(utcString, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var expectedDate = DateOnly.Parse(expectedDateString);

        var actualDate = DateOnly.FromDateTime(utcDateTime.AddHours(7));

        actualDate.Should().Be(expectedDate);
    }

    [Fact(DisplayName = "CHALLENGE-02: Quota Handler truy vấn đúng DateOnly hôm nay và hoàn toàn cô lập với hôm qua/ngày mai")]
    public async Task GetMockExamQuota_Strictly_Filters_By_Today_DateOnly()
    {
        using var context = CreateDbContext();
        var (course, student, _) = await SeedCourseAndStudentAsync(context);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var yesterdayVn = todayVn.AddDays(-1);
        var tomorrowVn = todayVn.AddDays(1);

        // Sinh viên đã thi 3 lượt hôm qua (hết quota hôm qua)
        context.MockExamQuotas.Add(new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            QuotaDate = yesterdayVn,
            UsedCount = 3,
            UpdatedAt = DateTime.UtcNow
        });

        // Sinh viên có bản ghi ngày mai (used = 2)
        context.MockExamQuotas.Add(new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            QuotaDate = tomorrowVn,
            UsedCount = 2,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var result = await handler.Handle(new GetMockExamQuotaQuery(course.Id, student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.QuotaDate.Should().Be(todayVn);
        result.Value.UsedCount.Should().Be(0, "Hôm nay chưa có lượt thi nào.");
        result.Value.RemainingCount.Should().Be(3);
        result.Value.CanStartExam.Should().BeTrue();
    }

    #endregion

    #region Category 2: Extreme Quota Values

    [Theory(DisplayName = "CHALLENGE-03: Xử lý các giá trị hạn ngạch cực đoan trong GetMockExamQuotaQueryHandler")]
    [InlineData(1, 0, 1, 1, true)]        // Hạn ngạch 1, chưa dùng -> còn 1
    [InlineData(1, 1, 1, 0, false)]       // Hạn ngạch 1, đã dùng 1 -> còn 0, không thể thi
    [InlineData(100, 45, 100, 55, true)]  // Hạn ngạch 100, đã dùng 45 -> còn 55
    [InlineData(100, 100, 100, 0, false)] // Hạn ngạch 100, đã dùng 100 -> còn 0, chặn
    [InlineData(-99, 0, 3, 3, true)]      // Hạn ngạch âm -99 -> fallback 3, còn 3
    [InlineData(-99, 2, 3, 1, true)]      // Hạn ngạch âm -99 -> fallback 3, dùng 2 -> còn 1
    [InlineData(-99, 3, 3, 0, false)]     // Hạn ngạch âm -99 -> fallback 3, dùng 3 -> chặn
    [InlineData(0, 0, 3, 3, true)]        // Hạn ngạch 0 -> fallback 3, còn 3
    [InlineData(int.MinValue, 1, 3, 2, true)] // int.MinValue -> fallback 3, dùng 1 -> còn 2
    [InlineData(int.MaxValue, 5, int.MaxValue, int.MaxValue - 5, true)] // int.MaxValue -> không tràn số
    public async Task GetMockExamQuota_Handles_Extreme_Quota_Values(
        int maxMockExamsPerDay,
        int simulatedUsedCount,
        int expectedMaxDailyQuota,
        int expectedRemaining,
        bool expectedCanStart)
    {
        using var context = CreateDbContext();
        var (course, student, _) = await SeedCourseAndStudentAsync(context, maxMockExamsPerDay: maxMockExamsPerDay);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (simulatedUsedCount > 0)
        {
            context.MockExamQuotas.Add(new MockExamQuota
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                StudentId = student.Id,
                QuotaDate = todayVn,
                UsedCount = simulatedUsedCount,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var handler = new GetMockExamQuotaQueryHandler(context);
        var result = await handler.Handle(new GetMockExamQuotaQuery(course.Id, student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MaxDailyQuota.Should().Be(expectedMaxDailyQuota);
        result.Value.RemainingCount.Should().Be(expectedRemaining);
        result.Value.CanStartExam.Should().Be(expectedCanStart);
    }

    [Theory(DisplayName = "CHALLENGE-04: StartMockExam tôn trọng hạn ngạch cực đoan (1, 100, -99)")]
    [InlineData(1, 1, true)]    // Hạn ngạch 1: lần 1 được
    [InlineData(1, 2, false)]   // Hạn ngạch 1: lần 2 bị chặn
    [InlineData(-99, 3, true)]  // Hạn ngạch -99 -> fallback 3: lần 3 được
    [InlineData(-99, 4, false)] // Hạn ngạch -99 -> fallback 3: lần 4 bị chặn
    public async Task StartMockExam_Enforces_Extreme_Quota_Limits(
        int maxMockExamsPerDay,
        int targetAttempt,
        bool expectSuccessOnTargetAttempt)
    {
        using var context = CreateDbContext();
        var (course, student, _) = await SeedCourseAndStudentAsync(context, maxMockExamsPerDay: maxMockExamsPerDay);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        int initialUsed = targetAttempt - 1;

        if (initialUsed > 0)
        {
            context.MockExamQuotas.Add(new MockExamQuota
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                StudentId = student.Id,
                QuotaDate = todayVn,
                UsedCount = initialUsed,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        if (expectSuccessOnTargetAttempt)
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.UsedQuota.Should().Be(targetAttempt);
        }
        else
        {
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().StartWith("QUOTA_EXCEEDED:");
        }
    }

    [Fact(DisplayName = "CHALLENGE-05: UsedCount vượt quá MaxDailyQuota (UsedCount = 999) được kẹp Remaining = 0 và chặn thi")]
    public async Task GetMockExamQuota_Clamps_Remaining_When_UsedCount_Is_Astronomical()
    {
        using var context = CreateDbContext();
        var (course, student, _) = await SeedCourseAndStudentAsync(context, maxMockExamsPerDay: 3);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        context.MockExamQuotas.Add(new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            QuotaDate = todayVn,
            UsedCount = 999,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var queryResult = await handler.Handle(new GetMockExamQuotaQuery(course.Id, student.Id), CancellationToken.None);

        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value.UsedCount.Should().Be(999);
        queryResult.Value.RemainingCount.Should().Be(0, "Remaining không được âm.");
        queryResult.Value.CanStartExam.Should().BeFalse();

        var startHandler = new StartMockExamCommandHandler(context);
        var startResult = await startHandler.Handle(new StartMockExamCommand(course.Id, student.Id, false), CancellationToken.None);

        startResult.IsSuccess.Should().BeFalse();
        startResult.Error.Should().StartWith("QUOTA_EXCEEDED:");
    }

    #endregion

    #region Category 3: Concurrency Safety and Race Conditions

    [Fact(DisplayName = "CHALLENGE-06: Chống Race Condition khi hai yêu cầu SubmitMockExamSession gửi đồng thời")]
    public async Task SubmitMockExamSession_ConcurrentRequests_OnlyOneSucceeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var setupContext = CreateDbContext(dbName);
        var (course, student, rubric) = await SeedCourseAndStudentAsync(setupContext);

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(20),
            Status = "in_progress"
        };
        var pq = await setupContext.PracticeQuestions.FirstAsync(q => q.CourseId == course.Id);
        session.Answers.Add(new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = pq.Id,
            AnswerText = "Clean Architecture gồm Domain, Application, Infrastructure, API",
            Status = "answered"
        });
        setupContext.MockExamSessions.Add(session);
        await setupContext.SaveChangesAsync();

        // Giả lập AI grading có độ trễ 200ms
        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(200);
                return new AiGradingResult
                {
                    Score = 9.0m,
                    ConfidenceScore = 0.95m,
                    IsSuspicious = false,
                    Feedback = "Xuất sắc",
                    CotTrace = "CoT",
                    Details = new List<GradingDetailDto>()
                };
            });

        // Chạy 2 tác vụ nộp bài với 2 context riêng biệt
        using var context1 = CreateDbContext(dbName);
        using var context2 = CreateDbContext(dbName);

        var handler1 = new SubmitMockExamSessionCommandHandler(context1, aiMock.Object);
        var handler2 = new SubmitMockExamSessionCommandHandler(context2, aiMock.Object);

        var command = new SubmitMockExamSessionCommand(session.Id, student.Id);

        // Khởi động cả 2 request đồng thời
        var task1 = handler1.Handle(command, CancellationToken.None);
        var task2 = handler2.Handle(command, CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => !r.IsSuccess && r.Error!.Contains("đã được nộp"));

        // Ít nhất 1 request thành công, và request trùng phải bị từ chối
        successCount.Should().Be(1, "Chỉ duy nhất một request được nộp và chấm điểm.");
        failureCount.Should().Be(1, "Request thứ hai phải bị từ chối do trạng thái đã khóa.");
    }

    [Fact(DisplayName = "CHALLENGE-07: Nhiều sinh viên bắt đầu thi đồng thời trên cùng một môn học không làm hỏng hạn ngạch của nhau")]
    public async Task StartMockExam_MultipleStudents_Concurrently_Isolates_Quotas()
    {
        var dbName = Guid.NewGuid().ToString();
        using var setupContext = CreateDbContext(dbName);
        var (course, _, _) = await SeedCourseAndStudentAsync(setupContext, maxMockExamsPerDay: 3);

        const int studentCount = 10;
        var studentIds = new List<Guid>();

        for (int i = 0; i < studentCount; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"student_concurrent_{i}@fpt.edu.vn",
                FullName = $"Student {i}",
                Role = "student",
                IsActive = true
            };
            setupContext.Users.Add(student);
            studentIds.Add(student.Id);
        }
        await setupContext.SaveChangesAsync();

        // Chạy song song 10 sinh viên bắt đầu thi
        var tasks = studentIds.Select(async sId =>
        {
            using var ctx = CreateDbContext(dbName);
            var handler = new StartMockExamCommandHandler(ctx);
            var cmd = new StartMockExamCommand(course.Id, sId, HasFollowUp: false);
            return await handler.Handle(cmd, CancellationToken.None);
        }).ToList();

        var results = await Task.WhenAll(tasks);

        results.All(r => r.IsSuccess).Should().BeTrue("Tất cả 10 sinh viên phải bắt đầu thi thành công.");
        results.All(r => r.Value.UsedQuota == 1).Should().BeTrue("Mỗi sinh viên chỉ tiêu tốn 1 lượt của riêng mình.");
        results.All(r => r.Value.RemainingQuota == 2).Should().BeTrue("Mỗi sinh viên còn đúng 2 lượt.");

        // Kiểm tra database có đúng 10 bản ghi quota độc lập
        using var verifyContext = CreateDbContext(dbName);
        var quotas = await verifyContext.MockExamQuotas.Where(q => q.CourseId == course.Id).ToListAsync();
        quotas.Should().HaveCount(studentCount);
        quotas.All(q => q.UsedCount == 1).Should().BeTrue();
    }

    [Fact(DisplayName = "CHALLENGE-08: Một sinh viên thi đồng thời trên 5 môn học khác nhau không bị trừ nhầm hạn ngạch")]
    public async Task StartMockExam_SingleStudent_MultipleCourses_Concurrently_Isolates_Quotas()
    {
        var dbName = Guid.NewGuid().ToString();
        using var setupContext = CreateDbContext(dbName);
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "multi_course_student@fpt.edu.vn",
            FullName = "Multi Course Student",
            Role = "student",
            IsActive = true
        };
        setupContext.Users.Add(student);

        var courseIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var course = new Course
            {
                Id = Guid.NewGuid(),
                Code = $"CRS{i:D3}",
                Name = $"Course {i}",
                Credits = 3,
                MaxMockExamsPerDay = 3,
                IsActive = true
            };
            setupContext.Courses.Add(course);
            courseIds.Add(course.Id);

            var rubric = new Rubric
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                Name = $"Rubric {i}",
                TotalMaxScore = 10.0m,
                IsActive = true
            };
            setupContext.Rubrics.Add(rubric);

            var structure = new ExamStructure
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                CreatedBy = student.Id,
                Name = $"Structure {i}",
                TotalQuestions = 5,
                DurationMinutes = 30,
                BloomDistribution = "{\"Remember\": 5}",
                IsActive = true
            };
            setupContext.ExamStructures.Add(structure);

            for (int q = 0; q < 5; q++)
            {
                setupContext.PracticeQuestions.Add(new PracticeQuestion
                {
                    Id = Guid.NewGuid(),
                    CourseId = course.Id,
                    RubricId = rubric.Id,
                    Title = $"Q {q}",
                    Content = $"Content {q}",
                    BloomLevel = "Remember",
                    Difficulty = "easy",
                    IsActive = true
                });
            }
        }
        await setupContext.SaveChangesAsync();

        // Bắt đầu thi trên cả 5 môn cùng lúc
        var tasks = courseIds.Select(async cId =>
        {
            using var ctx = CreateDbContext(dbName);
            var handler = new StartMockExamCommandHandler(ctx);
            var cmd = new StartMockExamCommand(cId, student.Id, HasFollowUp: false);
            return await handler.Handle(cmd, CancellationToken.None);
        }).ToList();

        var results = await Task.WhenAll(tasks);

        results.All(r => r.IsSuccess).Should().BeTrue();
        results.All(r => r.Value.UsedQuota == 1).Should().BeTrue();
        results.All(r => r.Value.RemainingQuota == 2).Should().BeTrue();

        using var verifyContext = CreateDbContext(dbName);
        var studentQuotas = await verifyContext.MockExamQuotas.Where(q => q.StudentId == student.Id).ToListAsync();
        studentQuotas.Should().HaveCount(5);
        studentQuotas.All(q => q.UsedCount == 1).Should().BeTrue();
    }

    #endregion
}
