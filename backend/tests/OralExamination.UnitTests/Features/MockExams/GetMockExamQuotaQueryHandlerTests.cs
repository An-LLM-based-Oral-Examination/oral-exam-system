using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public class GetMockExamQuotaQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. Trả về đầy đủ hạn ngạch K=3 khi sinh viên chưa thi lượt nào trong ngày")]
    public async Task Handle_Should_Return_Full_Quota_When_No_Prior_Exams()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var studentId = Guid.NewGuid();
        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CourseId.Should().Be(course.Id);
        result.Value.StudentId.Should().Be(studentId);
        result.Value.UsedCount.Should().Be(0);
        result.Value.RemainingCount.Should().Be(3);
        result.Value.MaxDailyQuota.Should().Be(3);
        result.Value.CanStartExam.Should().BeTrue();
    }

    [Fact(DisplayName = "2. Trả về số lượt còn lại chính xác khi sinh viên đã thi 2 lượt")]
    public async Task Handle_Should_Return_Remaining_Count_When_Partially_Used()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Apps",
            Credits = 3
        };
        var studentId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var quota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentId,
            QuotaDate = today,
            UsedCount = 2,
            UpdatedAt = DateTime.UtcNow
        };

        context.Courses.Add(course);
        context.MockExamQuotas.Add(quota);
        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UsedCount.Should().Be(2);
        result.Value.RemainingCount.Should().Be(1);
        result.Value.CanStartExam.Should().BeTrue();
    }

    [Fact(DisplayName = "3. Chặn thi khi sinh viên đã đạt giới hạn tối đa K=3 lượt/ngày")]
    public async Task Handle_Should_Block_Exam_When_Quota_Exhausted()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWP391",
            Name = "Application Development Project",
            Credits = 3
        };
        var studentId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var quota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentId,
            QuotaDate = today,
            UsedCount = 3,
            UpdatedAt = DateTime.UtcNow
        };

        context.Courses.Add(course);
        context.MockExamQuotas.Add(quota);
        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UsedCount.Should().Be(3);
        result.Value.RemainingCount.Should().Be(0);
        result.Value.CanStartExam.Should().BeFalse();
    }

    [Fact(DisplayName = "4. Trả về Failure khi CourseId không tồn tại trong hệ thống")]
    public async Task Handle_Should_Return_Failure_When_Course_Not_Found()
    {
        using var context = CreateDbContext();
        var nonExistentCourseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(nonExistentCourseId, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Môn học không tồn tại");
    }

    [Fact(DisplayName = "5. Validator báo lỗi khi CourseId hoặc StudentId để trống")]
    public void Validator_Should_Fail_When_Inputs_Are_Empty()
    {
        var validator = new GetMockExamQuotaQueryValidator();

        var invalidCourseQuery = new GetMockExamQuotaQuery(Guid.Empty, Guid.NewGuid());
        var invalidCourseResult = validator.Validate(invalidCourseQuery);
        invalidCourseResult.IsValid.Should().BeFalse();
        invalidCourseResult.Errors.Should().Contain(e => e.PropertyName == "CourseId");

        var invalidStudentQuery = new GetMockExamQuotaQuery(Guid.NewGuid(), Guid.Empty);
        var invalidStudentResult = validator.Validate(invalidStudentQuery);
        invalidStudentResult.IsValid.Should().BeFalse();
        invalidStudentResult.Errors.Should().Contain(e => e.PropertyName == "StudentId");

        var validQuery = new GetMockExamQuotaQuery(Guid.NewGuid(), Guid.NewGuid());
        var validResult = validator.Validate(validQuery);
        validResult.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "6. Trả về Failure khi môn học đã bị vô hiệu hóa (IsActive = false)")]
    public async Task Handle_Should_Return_Failure_When_Course_Is_Inactive()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            IsActive = false
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var studentId = Guid.NewGuid();
        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("vô hiệu hóa");
    }

    [Fact(DisplayName = "7. Tôn trọng hạn ngạch động do Admin cấu hình cho môn học (MaxMockExamsPerDay = 5)")]
    public async Task Handle_Should_Respect_Custom_Course_Quota_When_MaxMockExamsPerDay_Is_Configured()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Cross-Platform",
            Credits = 3,
            MaxMockExamsPerDay = 5,
            IsActive = true
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var studentId = Guid.NewGuid();
        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.MaxDailyQuota.Should().Be(5);
        result.Value.RemainingCount.Should().Be(5);
        result.Value.UsedCount.Should().Be(0);
        result.Value.CanStartExam.Should().BeTrue();
    }

    [Theory(DisplayName = "8. Fallback về hạn ngạch mặc định K=3 khi MaxMockExamsPerDay <= 0")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task Handle_Should_Fallback_To_Default_Quota_3_When_MaxMockExamsPerDay_Is_Zero_Or_Negative(int configuredQuota)
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWP391",
            Name = "Application Development Project",
            Credits = 3,
            MaxMockExamsPerDay = configuredQuota,
            IsActive = true
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var studentId = Guid.NewGuid();
        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.MaxDailyQuota.Should().Be(3);
        result.Value.RemainingCount.Should().Be(3);
        result.Value.CanStartExam.Should().BeTrue();
    }

    [Fact(DisplayName = "9. Bỏ qua hạn ngạch từ các ngày trước hoặc tương lai, chỉ tính đúng ngày GMT+7 hôm nay")]
    public async Task Handle_Should_Ignore_Quotas_From_Previous_Or_Future_Dates()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            MaxMockExamsPerDay = 3,
            IsActive = true
        };
        var studentId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        var quotaYesterday = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentId,
            QuotaDate = yesterday,
            UsedCount = 3,
            UpdatedAt = DateTime.UtcNow
        };

        var quotaTomorrow = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentId,
            QuotaDate = tomorrow,
            UsedCount = 2,
            UpdatedAt = DateTime.UtcNow
        };

        context.Courses.Add(course);
        context.MockExamQuotas.AddRange(quotaYesterday, quotaTomorrow);
        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UsedCount.Should().Be(0);
        result.Value.RemainingCount.Should().Be(3);
        result.Value.CanStartExam.Should().BeTrue();
    }

    [Fact(DisplayName = "10. Giới hạn RemainingCount tối thiểu là 0 khi UsedCount vượt quá MaxDailyQuota")]
    public async Task Handle_Should_Clamp_Remaining_To_Zero_When_Used_Exceeds_MaxDailyQuota()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            MaxMockExamsPerDay = 3,
            IsActive = true
        };
        var studentId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var quota = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentId,
            QuotaDate = today,
            UsedCount = 5,
            UpdatedAt = DateTime.UtcNow
        };

        context.Courses.Add(course);
        context.MockExamQuotas.Add(quota);
        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);
        var query = new GetMockExamQuotaQuery(course.Id, studentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UsedCount.Should().Be(5);
        result.Value.RemainingCount.Should().Be(0);
        result.Value.CanStartExam.Should().BeFalse();
    }

    [Fact(DisplayName = "11. Cô lập hạn ngạch theo từng sinh viên và từng môn học")]
    public async Task Handle_Should_Isolate_Quota_By_Student_And_Course()
    {
        using var context = CreateDbContext();
        var course1 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            MaxMockExamsPerDay = 3,
            IsActive = true
        };
        var course2 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Cross-Platform",
            Credits = 3,
            MaxMockExamsPerDay = 3,
            IsActive = true
        };

        var student1 = Guid.NewGuid();
        var student2 = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var quotaStudent1Course1 = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course1.Id,
            StudentId = student1,
            QuotaDate = today,
            UsedCount = 3,
            UpdatedAt = DateTime.UtcNow
        };

        var quotaStudent2Course1 = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course1.Id,
            StudentId = student2,
            QuotaDate = today,
            UsedCount = 1,
            UpdatedAt = DateTime.UtcNow
        };

        var quotaStudent1Course2 = new MockExamQuota
        {
            Id = Guid.NewGuid(),
            CourseId = course2.Id,
            StudentId = student1,
            QuotaDate = today,
            UsedCount = 1,
            UpdatedAt = DateTime.UtcNow
        };

        context.Courses.AddRange(course1, course2);
        context.MockExamQuotas.AddRange(quotaStudent1Course1, quotaStudent2Course1, quotaStudent1Course2);
        await context.SaveChangesAsync();

        var handler = new GetMockExamQuotaQueryHandler(context);

        // Student1 trên Course2: còn 2 lượt
        var queryS1C2 = new GetMockExamQuotaQuery(course2.Id, student1);
        var resultS1C2 = await handler.Handle(queryS1C2, CancellationToken.None);
        resultS1C2.IsSuccess.Should().BeTrue();
        resultS1C2.Value.UsedCount.Should().Be(1);
        resultS1C2.Value.RemainingCount.Should().Be(2);
        resultS1C2.Value.CanStartExam.Should().BeTrue();

        // Student2 trên Course1: còn 2 lượt
        var queryS2C1 = new GetMockExamQuotaQuery(course1.Id, student2);
        var resultS2C1 = await handler.Handle(queryS2C1, CancellationToken.None);
        resultS2C1.IsSuccess.Should().BeTrue();
        resultS2C1.Value.UsedCount.Should().Be(1);
        resultS2C1.Value.RemainingCount.Should().Be(2);
        resultS2C1.Value.CanStartExam.Should().BeTrue();
    }
}
