using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng độc lập cho Milestone 3 (R3 MF-02 Timed Mock Exam & Daily Quota Guard Consistency).
/// Được thiết kế và thực thi độc lập bởi Empirical Challenger 2 (challenger_m3_2).
/// 
/// Trọng tâm đối kháng & ứng suất (Stress & Adversarial Challenge):
/// 1. Deactivated courses (IsActive = false) trên toàn bộ tổ hợp tham số (Query, Command, Follow-up, Quota records, Question banks).
/// 2. Oracle Probing Parity: Xác minh môn học không tồn tại và môn học bị vô hiệu hóa trả về kết quả hoàn toàn đồng nhất,
///    ngăn chặn kẻ tấn công dò quét (probe) sự tồn tại của các môn học ẩn/bị vô hiệu hóa.
/// 3. Ranh giới Guid (Guid.Empty, Random Guids) trong Validators và Handlers.
/// 4. Cách ly sinh viên (Student Isolation) và môn học (Course Isolation) dưới điều kiện ứng suất tải cao (Concurrent/Multi-tenant Stress).
/// 5. Ranh giới múi giờ GMT+7 (DateOnly) và bảo toàn hạn ngạch giữa các ngày.
/// </summary>
public class AdversarialMilestone3QuotaChallengerTests
{
    private readonly GetMockExamQuotaQueryValidator _quotaQueryValidator = new();
    private readonly StartMockExamCommandValidator _startExamCommandValidator = new();

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(Course Course, User Student, List<PracticeQuestion> PracticeQuestions)> SeedCourseWithQuestionsAsync(
        OralExamDbContext context,
        bool isCourseActive = true,
        int maxMockExamsPerDay = 3,
        int questionCount = 8)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"SWD_{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant(),
            Name = "Software Architecture & Design",
            Credits = 3,
            MaxMockExamsPerDay = maxMockExamsPerDay,
            IsActive = isCourseActive
        };
        context.Courses.Add(course);

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Sinh Viên Thử Nghiệm",
            Role = "student",
            IsActive = true
        };
        context.Users.Add(student);

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric Chuẩn 10.0",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        context.Rubrics.Add(rubric);

        var criterion = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Kiến trúc Clean Architecture",
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
            Name = $"Cấu trúc đề {course.Code}",
            TotalQuestions = 5,
            DurationMinutes = 30,
            BloomDistribution = "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}",
            IsActive = true
        };
        context.ExamStructures.Add(structure);

        var bloomLevels = new[] { "Remember", "Understand", "Apply", "Analyze", "Evaluate" };
        var practiceQuestions = new List<PracticeQuestion>();
        for (int i = 0; i < questionCount; i++)
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

        await context.SaveChangesAsync();
        return (course, student, practiceQuestions);
    }

    #region 1. Deactivated Courses (IsActive = false) Across Request Permutations

    [Theory(DisplayName = "ADV-M3-01: GetMockExamQuotaQueryHandler từ chối mọi môn học IsActive = false bất kể lịch sử quota đã thi")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task GetQuota_DeactivatedCourse_FailsAcrossAllQuotaUsageHistory(int existingUsedCount)
    {
        using var context = CreateDbContext();
        var (course, student, _) = await SeedCourseWithQuestionsAsync(context, isCourseActive: false);

        if (existingUsedCount > 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            context.MockExamQuotas.Add(new MockExamQuota
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                StudentId = student.Id,
                QuotaDate = today,
                UsedCount = existingUsedCount,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, student.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");
    }

    [Theory(DisplayName = "ADV-M3-02: StartMockExamCommandHandler từ chối môn học IsActive = false với cả 2 tùy chọn HasFollowUp")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartMockExam_DeactivatedCourse_FailsRegardlessOfFollowUpOption(bool hasFollowUp)
    {
        using var context = CreateDbContext();
        var (course, student, _) = await SeedCourseWithQuestionsAsync(context, isCourseActive: false);

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: hasFollowUp);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");

        // Khẳng định không có MockExamSession nào được tạo
        var sessionsCount = await context.MockExamSessions.CountAsync(s => s.CourseId == course.Id);
        sessionsCount.Should().Be(0);

        // Khẳng định không có Quota nào bị trừ
        var quotaCount = await context.MockExamQuotas.CountAsync(q => q.CourseId == course.Id);
        quotaCount.Should().Be(0);
    }

    [Fact(DisplayName = "ADV-M3-03: Môn học bị vô hiệu hóa khi kho câu hỏi rỗng vẫn fail tại bước kiểm tra môn học trước tiên")]
    public async Task StartMockExam_DeactivatedCourse_FailsAtCourseCheckBeforeQuestionCountCheck()
    {
        using var context = CreateDbContext();
        // Tạo môn học IsActive = false và 0 câu hỏi
        var (course, student, _) = await SeedCourseWithQuestionsAsync(context, isCourseActive: false, questionCount: 0);

        var handler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(course.Id, student.Id, HasFollowUp: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        // Thông báo lỗi phải là lỗi môn học, KHÔNG lộ thông tin thiếu câu hỏi
        result.Error.Should().Be("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");
    }

    #endregion

    #region 2. Anti-Probing & Oracle Leakage Parity (Non-Existent vs Deactivated)

    [Fact(DisplayName = "ADV-M3-04: GetMockExamQuotaQueryHandler trả thông điệp lỗi strictly identical giữa môn không tồn tại và môn bị vô hiệu hóa")]
    public async Task GetQuota_NonExistentAndDeactivatedCourses_YieldStrictlyIdenticalFailure()
    {
        using var context = CreateDbContext();
        var (inactiveCourse, student, _) = await SeedCourseWithQuestionsAsync(context, isCourseActive: false);
        var nonExistentCourseId = Guid.NewGuid();

        var handler = new GetMockExamQuotaQueryHandler(context);

        // 1. Query với CourseId không tồn tại
        var nonExistentQuery = new GetMockExamQuotaQuery(nonExistentCourseId, student.Id);
        var nonExistentResult = await handler.Handle(nonExistentQuery, CancellationToken.None);

        // 2. Query với CourseId tồn tại nhưng IsActive = false
        var inactiveQuery = new GetMockExamQuotaQuery(inactiveCourse.Id, student.Id);
        var inactiveResult = await handler.Handle(inactiveQuery, CancellationToken.None);

        // Xác minh kết quả giống hệt nhau 100%, không cho phép Oracle Probing
        nonExistentResult.IsSuccess.Should().BeFalse();
        inactiveResult.IsSuccess.Should().BeFalse();

        nonExistentResult.Error.Should().Be(inactiveResult.Error);
        nonExistentResult.Error.Should().Be("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");
    }

    [Fact(DisplayName = "ADV-M3-05: StartMockExamCommandHandler trả thông điệp lỗi strictly identical giữa môn không tồn tại và môn bị vô hiệu hóa")]
    public async Task StartMockExam_NonExistentAndDeactivatedCourses_YieldStrictlyIdenticalFailure()
    {
        using var context = CreateDbContext();
        var (inactiveCourse, student, _) = await SeedCourseWithQuestionsAsync(context, isCourseActive: false);
        var nonExistentCourseId = Guid.NewGuid();

        var handler = new StartMockExamCommandHandler(context);

        // 1. Command với CourseId không tồn tại
        var nonExistentCmd = new StartMockExamCommand(nonExistentCourseId, student.Id, HasFollowUp: false);
        var nonExistentResult = await handler.Handle(nonExistentCmd, CancellationToken.None);

        // 2. Command với CourseId tồn tại nhưng IsActive = false
        var inactiveCmd = new StartMockExamCommand(inactiveCourse.Id, student.Id, HasFollowUp: false);
        var inactiveResult = await handler.Handle(inactiveCmd, CancellationToken.None);

        // Xác minh không rò rỉ bất kỳ thông tin nào
        nonExistentResult.IsSuccess.Should().BeFalse();
        inactiveResult.IsSuccess.Should().BeFalse();

        nonExistentResult.Error.Should().Be(inactiveResult.Error);
        nonExistentResult.Error.Should().Be("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");
    }

    #endregion

    #region 3. Guid Boundaries & Validation Defense-in-Depth

    [Fact(DisplayName = "ADV-M3-06: GetMockExamQuotaQueryValidator chặn đứng Guid.Empty cho CourseId và StudentId")]
    public void GetQuotaValidator_Rejects_EmptyGuids()
    {
        var emptyCourseQuery = new GetMockExamQuotaQuery(Guid.Empty, Guid.NewGuid());
        var res1 = _quotaQueryValidator.Validate(emptyCourseQuery);
        res1.IsValid.Should().BeFalse();
        res1.Errors.Should().Contain(e => e.PropertyName == nameof(GetMockExamQuotaQuery.CourseId));

        var emptyStudentQuery = new GetMockExamQuotaQuery(Guid.NewGuid(), Guid.Empty);
        var res2 = _quotaQueryValidator.Validate(emptyStudentQuery);
        res2.IsValid.Should().BeFalse();
        res2.Errors.Should().Contain(e => e.PropertyName == nameof(GetMockExamQuotaQuery.StudentId));

        var bothEmptyQuery = new GetMockExamQuotaQuery(Guid.Empty, Guid.Empty);
        var res3 = _quotaQueryValidator.Validate(bothEmptyQuery);
        res3.IsValid.Should().BeFalse();
        res3.Errors.Should().HaveCount(2);
    }

    [Fact(DisplayName = "ADV-M3-07: StartMockExamCommandValidator chặn đứng Guid.Empty cho CourseId và StudentId")]
    public void StartMockExamValidator_Rejects_EmptyGuids()
    {
        var emptyCourseCmd = new StartMockExamCommand(Guid.Empty, Guid.NewGuid(), false);
        var res1 = _startExamCommandValidator.Validate(emptyCourseCmd);
        res1.IsValid.Should().BeFalse();
        res1.Errors.Should().Contain(e => e.PropertyName == nameof(StartMockExamCommand.CourseId));

        var emptyStudentCmd = new StartMockExamCommand(Guid.NewGuid(), Guid.Empty, false);
        var res2 = _startExamCommandValidator.Validate(emptyStudentCmd);
        res2.IsValid.Should().BeFalse();
        res2.Errors.Should().Contain(e => e.PropertyName == nameof(StartMockExamCommand.StudentId));

        var bothEmptyCmd = new StartMockExamCommand(Guid.Empty, Guid.Empty, true);
        var res3 = _startExamCommandValidator.Validate(bothEmptyCmd);
        res3.IsValid.Should().BeFalse();
        res3.Errors.Should().HaveCount(2);
    }

    [Fact(DisplayName = "ADV-M3-08: Handlers xử lý an toàn Guid.Empty mà không ném ngoại lệ hệ thống")]
    public async Task Handlers_SafelyHandle_GuidEmpty_WithoutThrowing()
    {
        using var context = CreateDbContext();

        var queryHandler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(Guid.Empty, Guid.Empty);
        var queryResult = await queryHandler.Handle(query, CancellationToken.None);

        queryResult.IsSuccess.Should().BeFalse();
        queryResult.Error.Should().Be("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");

        var commandHandler = new StartMockExamCommandHandler(context);
        var command = new StartMockExamCommand(Guid.Empty, Guid.Empty, false);
        var commandResult = await commandHandler.Handle(command, CancellationToken.None);

        commandResult.IsSuccess.Should().BeFalse();
        commandResult.Error.Should().Be("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");
    }

    #endregion

    #region 4. Student & Course Isolation Under Stress

    [Fact(DisplayName = "ADV-M3-09: 20 sinh viên cùng truy vấn quota trên 5 môn học độc lập không bị tranh chấp hoặc rò rỉ dữ liệu")]
    public async Task Stress_MultiStudent_MultiCourse_Quota_StrictIsolation()
    {
        using var context = CreateDbContext();

        // Khởi tạo 5 môn học
        var courses = new List<Course>();
        for (int c = 0; c < 5; c++)
        {
            var course = new Course
            {
                Id = Guid.NewGuid(),
                Code = $"CRS{c + 1}",
                Name = $"Môn học thứ {c + 1}",
                Credits = 3,
                MaxMockExamsPerDay = 3 + c, // 3, 4, 5, 6, 7
                IsActive = true
            };
            courses.Add(course);
            context.Courses.Add(course);
        }

        // Khởi tạo 20 sinh viên
        var students = new List<User>();
        for (int s = 0; s < 20; s++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"student{s + 1}@fpt.edu.vn",
                FullName = $"Sinh Viên {s + 1}",
                Role = "student",
                IsActive = true
            };
            students.Add(student);
            context.Users.Add(student);
        }

        // Sinh viên 1 dùng hết quota trên môn 1
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        context.MockExamQuotas.Add(new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = courses[0].Id,
            StudentId = students[0].Id,
            QuotaDate = today,
            UsedCount = courses[0].MaxMockExamsPerDay,
            UpdatedAt = DateTime.UtcNow
        });

        // Sinh viên 2 dùng 1 lượt trên môn 2
        context.MockExamQuotas.Add(new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = courses[1].Id,
            StudentId = students[1].Id,
            QuotaDate = today,
            UsedCount = 1,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);

        // Chạy song song 100 queries kết hợp 20 sinh viên x 5 môn học
        var tasks = new List<Task<Result<MockExamQuotaDto>>>();
        foreach (var student in students)
        {
            foreach (var course in courses)
            {
                tasks.Add(handler.Handle(new GetMockExamQuotaQuery(course.Id, student.Id), CancellationToken.None));
            }
        }

        var results = await Task.WhenAll(tasks);

        // Toàn bộ 100 queries phải thành công
        results.All(r => r.IsSuccess).Should().BeTrue();

        // Kiểm tra đúng Sinh viên 1 trên Môn 1 hết quota
        var s1c1 = results.First(r => r.Value.StudentId == students[0].Id && r.Value.CourseId == courses[0].Id).Value;
        s1c1.UsedCount.Should().Be(3);
        s1c1.RemainingCount.Should().Be(0);
        s1c1.CanStartExam.Should().BeFalse();

        // Nhưng Sinh viên 1 trên Môn 2 (quota = 4) vẫn còn nguyên 4 lượt
        var s1c2 = results.First(r => r.Value.StudentId == students[0].Id && r.Value.CourseId == courses[1].Id).Value;
        s1c2.UsedCount.Should().Be(0);
        s1c2.RemainingCount.Should().Be(4);
        s1c2.CanStartExam.Should().BeTrue();

        // Sinh viên 2 trên Môn 2 đã dùng 1, còn 3 lượt (quota = 4)
        var s2c2 = results.First(r => r.Value.StudentId == students[1].Id && r.Value.CourseId == courses[1].Id).Value;
        s2c2.UsedCount.Should().Be(1);
        s2c2.RemainingCount.Should().Be(3);
        s2c2.CanStartExam.Should().BeTrue();

        // Sinh viên 2 trên Môn 1 còn nguyên 3 lượt (quota = 3)
        var s2c1 = results.First(r => r.Value.StudentId == students[1].Id && r.Value.CourseId == courses[0].Id).Value;
        s2c1.UsedCount.Should().Be(0);
        s2c1.RemainingCount.Should().Be(3);
        s2c1.CanStartExam.Should().BeTrue();

        // Các sinh viên còn lại (s3..s20) có 0 lượt dùng trên toàn bộ các môn
        var otherResults = results.Where(r => r.Value.StudentId != students[0].Id && r.Value.StudentId != students[1].Id);
        otherResults.All(r => r.Value.UsedCount == 0 && r.Value.CanStartExam).Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-M3-10: Ranh giới múi giờ GMT+7 - Quota của hôm qua và ngày mai tuyệt đối không ảnh hưởng đến ngày hôm nay")]
    public async Task DateBoundary_GMT7_OnlyMatchesExactVietnamDate()
    {
        using var context = CreateDbContext();
        var (course, student, _) = await SeedCourseWithQuestionsAsync(context, isCourseActive: true, maxMockExamsPerDay: 3);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);
        var tomorrow = today.AddDays(1);
        var twoDaysLater = today.AddDays(2);

        // Thêm các quota ở các ngày khác nhau cho cùng sinh viên và môn học
        context.MockExamQuotas.AddRange(
            new MockExamQuota { Id = Guid.NewGuid(), CourseId = course.Id, StudentId = student.Id, QuotaDate = twoDaysAgo, UsedCount = 3, UpdatedAt = DateTime.UtcNow },
            new MockExamQuota { Id = Guid.NewGuid(), CourseId = course.Id, StudentId = student.Id, QuotaDate = yesterday, UsedCount = 3, UpdatedAt = DateTime.UtcNow },
            new MockExamQuota { Id = Guid.NewGuid(), CourseId = course.Id, StudentId = student.Id, QuotaDate = tomorrow, UsedCount = 3, UpdatedAt = DateTime.UtcNow },
            new MockExamQuota { Id = Guid.NewGuid(), CourseId = course.Id, StudentId = student.Id, QuotaDate = twoDaysLater, UsedCount = 3, UpdatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var queryHandler = new GetMockExamQuotaQueryHandler(context);
        var queryResult = await queryHandler.Handle(new GetMockExamQuotaQuery(course.Id, student.Id), CancellationToken.None);

        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value.UsedCount.Should().Be(0, "Hôm nay chưa thi lượt nào dù các ngày khác đã dùng hết.");
        queryResult.Value.RemainingCount.Should().Be(3);
        queryResult.Value.CanStartExam.Should().BeTrue();

        // Thực hiện thi lượt đầu tiên của ngày hôm nay
        var commandHandler = new StartMockExamCommandHandler(context);
        var startResult = await commandHandler.Handle(new StartMockExamCommand(course.Id, student.Id, false), CancellationToken.None);

        startResult.IsSuccess.Should().BeTrue();
        startResult.Value.UsedQuota.Should().Be(1);
        startResult.Value.RemainingQuota.Should().Be(2);

        // Kiểm tra bản ghi quota hôm nay được tạo riêng biệt
        var todayQuota = await context.MockExamQuotas
            .FirstOrDefaultAsync(q => q.CourseId == course.Id && q.StudentId == student.Id && q.QuotaDate == today);

        todayQuota.Should().NotBeNull();
        todayQuota!.UsedCount.Should().Be(1);

        // Tổng cộng phải có đúng 5 bản ghi quota cho 5 ngày riêng biệt
        var allQuotas = await context.MockExamQuotas
            .Where(q => q.CourseId == course.Id && q.StudentId == student.Id)
            .ToListAsync();
        allQuotas.Should().HaveCount(5);
    }

    [Fact(DisplayName = "ADV-M3-11: Cấu hình động MaxMockExamsPerDay từ 1 đến 100 được áp dụng chính xác cho từng môn học")]
    public async Task DynamicQuota_VaryingConfigurations_AppliedAccurately()
    {
        using var context = CreateDbContext();

        var testQuotas = new[] { 1, 2, 4, 7, 10, 50, 100 };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.dynamic@fpt.edu.vn",
            FullName = "Dynamic Quota Student",
            Role = "student",
            IsActive = true
        };
        context.Users.Add(student);

        var queryHandler = new GetMockExamQuotaQueryHandler(context);

        foreach (var expectedQuota in testQuotas)
        {
            var course = new Course
            {
                Id = Guid.NewGuid(),
                Code = $"DYN_{expectedQuota}",
                Name = $"Course with quota {expectedQuota}",
                Credits = 3,
                MaxMockExamsPerDay = expectedQuota,
                IsActive = true
            };
            context.Courses.Add(course);
            await context.SaveChangesAsync();

            var result = await queryHandler.Handle(new GetMockExamQuotaQuery(course.Id, student.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.MaxDailyQuota.Should().Be(expectedQuota);
            result.Value.RemainingCount.Should().Be(expectedQuota);
            result.Value.UsedCount.Should().Be(0);
            result.Value.CanStartExam.Should().BeTrue();
        }
    }

    #endregion
}
