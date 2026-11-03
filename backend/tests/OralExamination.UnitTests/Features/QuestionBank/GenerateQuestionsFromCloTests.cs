using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromClo;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank;

public class GenerateQuestionsFromCloTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"GenerateQuestionsFromClo_{Guid.NewGuid()}")
            .Options;

        return new OralExamDbContext(options);
    }

    private class FakeAiQuestionGenerationService : IAiQuestionGenerationService
    {
        public List<GeneratedQuestionDraftDto> ResultQuestions { get; set; } = new();

        public Task<List<GeneratedQuestionDraftDto>> GenerateQuestionsAsync(
            AiQuestionGenerationParams parameters,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultQuestions);
        }
    }

    private static (Course Course, User Lecturer, List<CourseClo> Clos) SeedData(OralExamDbContext db, string userRole = UserRole.Lecturer, bool isCourseActive = true)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        db.Semesters.Add(semester);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = isCourseActive
        };
        db.Courses.Add(course);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "tester@fpt.edu.vn",
            FullName = "Nguyễn Văn Test",
            Role = userRole,
            IsActive = true
        };
        db.Users.Add(user);

        var clos = new List<CourseClo>
        {
            new() { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Hiểu kiến trúc .NET 8", BloomLevel = "Understand", OrderIndex = 1, IsActive = true },
            new() { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "Vận dụng Clean Architecture", BloomLevel = "Apply", OrderIndex = 2, IsActive = true },
            new() { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO3", Description = "Phân tích hiệu năng", BloomLevel = "Analyze", OrderIndex = 3, IsActive = true }
        };
        db.CourseClos.AddRange(clos);

        db.SaveChanges();
        return (course, user, clos);
    }

    [Fact(DisplayName = "1. Trả về Failure khi không tìm thấy môn học hoặc môn học bị vô hiệu hóa")]
    public async Task Handle_Should_Fail_When_Course_Not_Found_Or_Inactive()
    {
        await using var db = CreateDbContext();
        var (course, user, _) = SeedData(db, isCourseActive: false);

        var aiService = new FakeAiQuestionGenerationService();
        var handler = new GenerateQuestionsFromCloCommandHandler(db, aiService);

        var command = new GenerateQuestionsFromCloCommand(
            course.Id,
            user.Id,
            new List<string> { "CLO1" },
            new List<string> { "Clean Architecture" },
            3,
            UsageScope.Exam,
            new List<string> { BloomLevel.Understand },
            new DifficultyDistributionDto(1, 1, 1)
        );

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không tìm thấy môn học tương ứng hoặc môn học đã bị khóa.");
    }

    [Fact(DisplayName = "2. Trả về Failure khi người yêu cầu không mang vai trò Giảng viên, Trưởng BM hoặc Admin")]
    public async Task Handle_Should_Fail_When_Requester_Is_Unauthorized()
    {
        await using var db = CreateDbContext();
        var (course, student, _) = SeedData(db, userRole: UserRole.Student);

        var aiService = new FakeAiQuestionGenerationService();
        var handler = new GenerateQuestionsFromCloCommandHandler(db, aiService);

        var command = new GenerateQuestionsFromCloCommand(
            course.Id,
            student.Id,
            new List<string> { "CLO1" },
            new List<string> { "Clean Architecture" },
            3,
            UsageScope.Exam,
            new List<string> { BloomLevel.Understand },
            new DifficultyDistributionDto(1, 1, 1)
        );

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Chỉ Giảng viên ('lecturer'), Trưởng Bộ Môn ('department_head') hoặc Quản trị viên");
    }

    [Fact(DisplayName = "3. Trả về Failure khi các CLO được chọn không tồn tại trong CSDL 3NF course_clos")]
    public async Task Handle_Should_Fail_When_Selected_Clos_Not_Found_In_Database()
    {
        await using var db = CreateDbContext();
        var (course, lecturer, _) = SeedData(db, userRole: UserRole.Lecturer);

        var aiService = new FakeAiQuestionGenerationService();
        var handler = new GenerateQuestionsFromCloCommandHandler(db, aiService);

        var command = new GenerateQuestionsFromCloCommand(
            course.Id,
            lecturer.Id,
            new List<string> { "NON_EXISTENT_CLO" },
            new List<string> { "Clean Architecture" },
            3,
            UsageScope.Exam,
            new List<string> { BloomLevel.Understand },
            new DifficultyDistributionDto(1, 1, 1)
        );

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không tìm thấy Chuẩn đầu ra (CLO) hợp lệ nào trong cơ sở dữ liệu");
    }

    [Fact(DisplayName = "4. Sinh câu hỏi thành công từ course_clos và phát hiện cờ trùng lặp isFlagged với ngân hàng câu hỏi")]
    public async Task Handle_Should_Succeed_And_Flag_Duplicate_Questions()
    {
        await using var db = CreateDbContext();
        var (course, lecturer, _) = SeedData(db, userRole: UserRole.Lecturer);

        // Thêm câu hỏi đã có sẵn trong ngân hàng để kiểm tra cờ trùng lặp
        var existingRubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Barem Test",
            TotalMaxScore = 10.00m,
            IsActive = true
        };
        db.Rubrics.Add(existingRubric);

        db.ExamQuestions.Add(new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = existingRubric.Id,
            Title = "Giải thích nguyên lý Dependency Inversion trong ASP.NET Core",
            Content = "Nội dung câu hỏi...",
            Difficulty = "medium",
            BloomLevel = "Understand",
            Source = QuestionSource.Manual,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var aiService = new FakeAiQuestionGenerationService
        {
            ResultQuestions = new List<GeneratedQuestionDraftDto>
            {
                new(
                    "draft-1",
                    "CLO1",
                    "Giải thích nguyên lý Dependency Inversion trong ASP.NET Core", // Trùng tiêu đề!
                    "Nội dung câu hỏi 1",
                    "medium",
                    "Understand",
                    QuestionSource.AiGenerated,
                    UsageScope.Exam,
                    "Model answer mẫu với độ dài lớn hơn 50 ký tự để thỏa mãn barem FPTU.",
                    new List<string> { "IoC", "DI Container" },
                    false,
                    null,
                    new RubricDraftDto("Rubric 1", "Desc", 10.00m, new List<RubricCriterionDraftDto>())
                ),
                new(
                    "draft-2",
                    "CLO2",
                    "Thiết kế kiến trúc Clean Architecture cho hệ thống Microservices", // Câu mới hoàn toàn
                    "Nội dung câu hỏi 2",
                    "hard",
                    "Apply",
                    QuestionSource.AiGenerated,
                    UsageScope.Exam,
                    "Model answer mẫu với độ dài lớn hơn 50 ký tự để thỏa mãn barem FPTU.",
                    new List<string> { "Domain Layer", "Application Layer" },
                    false,
                    null,
                    new RubricDraftDto("Rubric 2", "Desc", 10.00m, new List<RubricCriterionDraftDto>())
                )
            }
        };

        var handler = new GenerateQuestionsFromCloCommandHandler(db, aiService);

        var command = new GenerateQuestionsFromCloCommand(
            course.Id,
            lecturer.Id,
            new List<string> { "CLO1", "CLO2" },
            new List<string> { "Clean Architecture", "Dependency Injection" },
            2,
            UsageScope.Exam,
            new List<string> { BloomLevel.Understand, BloomLevel.Apply },
            new DifficultyDistributionDto(0, 1, 1)
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CourseCode.Should().Be("PRN231");
        result.Value.GeneratedCount.Should().Be(2);
        result.Value.Source.Should().Be(QuestionSource.AiGenerated);

        var q1 = result.Value.Questions.First(q => q.TempId == "draft-1");
        q1.IsFlagged.Should().BeTrue("Câu 1 trùng với câu hỏi đã có trong ExamQuestions");
        q1.FlagReason.Should().Contain("Phát hiện câu hỏi có nội dung tương đồng cao");

        var q2 = result.Value.Questions.First(q => q.TempId == "draft-2");
        q2.IsFlagged.Should().BeFalse("Câu 2 là câu hỏi mới không trùng lặp");
        q2.FlagReason.Should().BeNull();
    }

    [Fact(DisplayName = "5. Validator khóa chặt phân bổ độ khó: easy + medium + hard == numberOfQuestions")]
    public void Validator_Should_Enforce_Difficulty_Distribution_Invariant()
    {
        var validator = new GenerateQuestionsFromCloCommandValidator();

        var invalidCommand = new GenerateQuestionsFromCloCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<string> { "CLO1" },
            new List<string> { "Topic 1" },
            3,
            UsageScope.Exam,
            new List<string> { BloomLevel.Understand },
            new DifficultyDistributionDto(1, 1, 2) // 1+1+2 = 4 != 3!
        );

        var invalidResult = validator.Validate(invalidCommand);
        invalidResult.IsValid.Should().BeFalse();
        invalidResult.Errors.Should().Contain(e => e.ErrorMessage.Contains("Tổng phân bổ độ khó (easy + medium + hard) phải bằng chính xác số lượng câu hỏi"));

        var validCommand = new GenerateQuestionsFromCloCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<string> { "CLO1" },
            new List<string> { "Topic 1" },
            3,
            UsageScope.Exam,
            new List<string> { BloomLevel.Understand },
            new DifficultyDistributionDto(1, 1, 1) // 1+1+1 = 3 == 3!
        );

        var validResult = validator.Validate(validCommand);
        validResult.IsValid.Should().BeTrue();
    }
}
