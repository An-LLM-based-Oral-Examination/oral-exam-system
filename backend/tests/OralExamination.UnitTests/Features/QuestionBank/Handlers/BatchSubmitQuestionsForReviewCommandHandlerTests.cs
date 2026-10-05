using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank.Handlers;

public class BatchSubmitQuestionsForReviewCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Lecturer, Course Course)> SeedUserAndCourseAsync(
        OralExamDbContext context, 
        string role = "lecturer", 
        bool isActive = true)
    {
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.submit@fpt.edu.vn",
            FullName = "Giảng Viên Đệ Trình",
            Role = role,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            HasFollowUp = true,
            IsActive = true
        };

        context.Users.Add(lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        return (lecturer, course);
    }

    private ExamQuestion CreateExamQuestion(
        Guid courseId,
        Guid lecturerId,
        string sampleAnswer = "Đây là câu trả lời mẫu đạt chuẩn tối thiểu 50 ký tự cho câu hỏi thi vấn đáp phần mềm.",
        decimal rubricTotal = 10.00m,
        int criteriaCount = 2,
        bool hasRubric = true)
    {
        Rubric? rubric = null;

        if (hasRubric)
        {
            rubric = new Rubric
            {
                CourseId = courseId,
                Name = "Barem Rubric Chuẩn 10đ",
                TotalMaxScore = rubricTotal,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            decimal scorePerCriterion = rubricTotal / criteriaCount;
            for (int i = 1; i <= criteriaCount; i++)
            {
                rubric.Criteria.Add(new RubricCriterion
                {
                    CriterionName = $"Tiêu chí {i}",
                    MaxScore = scorePerCriterion,
                    Weight = 100m / criteriaCount,
                    BloomLevel = "understand",
                    OrderIndex = i,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Rubric = rubric!,
            Title = "Câu hỏi thi kết thúc môn PRN231",
            Content = "Trình bày cấu trúc Clean Architecture và luồng xử lý MediatR CQRS.",
            SampleAnswer = sampleAnswer,
            KeyPoints = "[\"Domain thuần khiết\", \"CQRS\", \"Dependency Inversion\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Analyze,
            Source = "manual",
            ApprovalStatus = ApprovalStatus.Draft,
            SubmittedBy = lecturerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact(DisplayName = "1. Handle cập nhật trạng thái mẻ câu hỏi sang SUBMITTED_FOR_REVIEW thành công")]
    public async Task Handle_Should_Submit_Questions_Successfully_When_Valid()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        var q1 = CreateExamQuestion(course.Id, lecturer.Id);
        var q2 = CreateExamQuestion(course.Id, lecturer.Id);
        context.ExamQuestions.AddRange(q1, q2);
        await context.SaveChangesAsync();

        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { q1.Id, q2.Id },
            SubmissionNotes: "Kính gửi Trưởng Bộ Môn phê duyệt 2 câu hỏi mới soạn."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SubmittedCount.Should().Be(2);
        result.Value.ApprovalStatus.Should().Be(ApprovalStatus.SubmittedForReview);
        result.Value.SubmittedBy.Should().Be(lecturer.Id);

        // Kiểm tra trong DB
        var dbQuestions = await context.ExamQuestions
            .Where(q => q.Id == q1.Id || q.Id == q2.Id)
            .ToListAsync();

        dbQuestions.Should().HaveCount(2);
        foreach (var q in dbQuestions)
        {
            q.ApprovalStatus.Should().Be(ApprovalStatus.SubmittedForReview);
            q.SubmittedBy.Should().Be(lecturer.Id);
            q.ReviewNotes.Should().Be("Kính gửi Trưởng Bộ Môn phê duyệt 2 câu hỏi mới soạn.");
        }
    }

    [Fact(DisplayName = "2. Handle trả về Failure khi User không phải là Giảng viên hay Admin (ví dụ: student)")]
    public async Task Handle_Should_Return_Failure_When_User_Is_Not_Lecturer_Or_Admin()
    {
        using var context = CreateDbContext();
        var (student, course) = await SeedUserAndCourseAsync(context, role: "student");
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        var q = CreateExamQuestion(course.Id, student.Id);
        context.ExamQuestions.Add(q);
        await context.SaveChangesAsync();

        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: student.Id,
            QuestionIds: new List<Guid> { q.Id },
            SubmissionNotes: "Sinh viên cố tình nộp đề"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Chỉ Giảng viên ('lecturer') hoặc Quản trị viên mới có quyền");
    }

    [Fact(DisplayName = "3. Handle trả về Failure khi User bị vô hiệu hóa (IsActive == false)")]
    public async Task Handle_Should_Return_Failure_When_User_Is_Inactive()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context, role: "lecturer", isActive: false);
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        var q = CreateExamQuestion(course.Id, lecturer.Id);
        context.ExamQuestions.Add(q);
        await context.SaveChangesAsync();

        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { q.Id },
            SubmissionNotes: "Tài khoản bị khóa"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact(DisplayName = "4. Handle trả về Failure khi có câu hỏi không tồn tại hoặc không khớp CourseId")]
    public async Task Handle_Should_Return_Failure_When_Question_Not_Found_Or_CourseId_Mismatch()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        var q1 = CreateExamQuestion(course.Id, lecturer.Id);
        context.ExamQuestions.Add(q1);
        await context.SaveChangesAsync();

        var nonExistentId = Guid.NewGuid();
        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { q1.Id, nonExistentId },
            SubmissionNotes: "Kèm ID không tồn tại"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Một số câu hỏi không tồn tại hoặc không thuộc môn học đã chọn.");
    }

    [Fact(DisplayName = "5. HARD VERIFICATION GATE: Chặn khi có bất kỳ câu hỏi nào có Model Answer < 50 ký tự")]
    public async Task Handle_Should_Return_Failure_When_Any_Question_Has_Short_Model_Answer()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        var qValid = CreateExamQuestion(course.Id, lecturer.Id);
        var qShortAnswer = CreateExamQuestion(course.Id, lecturer.Id, sampleAnswer: "Câu trả lời quá ngắn dưới 50 ký tự.");
        context.ExamQuestions.AddRange(qValid, qShortAnswer);
        await context.SaveChangesAsync();

        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { qValid.Id, qShortAnswer.Id },
            SubmissionNotes: "Nộp kèm câu ngắn"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Model Answer ngắn hơn 50 ký tự");
    }

    [Fact(DisplayName = "6. HARD VERIFICATION GATE: Chặn khi có bất kỳ câu hỏi nào có Barem Rubric lệch 10.00 điểm")]
    public async Task Handle_Should_Return_Failure_When_Any_Question_Has_Rubric_Not_10_00()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        var qValid = CreateExamQuestion(course.Id, lecturer.Id);
        var qBadRubric = CreateExamQuestion(course.Id, lecturer.Id, rubricTotal: 9.00m);
        context.ExamQuestions.AddRange(qValid, qBadRubric);
        await context.SaveChangesAsync();

        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { qValid.Id, qBadRubric.Id },
            SubmissionNotes: "Nộp kèm rubric lệch điểm"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("lệch 10.00đ");
    }

    [Fact(DisplayName = "7. HARD VERIFICATION GATE: Chặn khi có bất kỳ câu hỏi nào có Barem Rubric ít hơn 2 tiêu chí con")]
    public async Task Handle_Should_Return_Failure_When_Any_Question_Has_Fewer_Than_Two_Rubric_Criteria()
    {
        using var context = CreateDbContext();
        var (lecturer, course) = await SeedUserAndCourseAsync(context);
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        var qOneCriteria = CreateExamQuestion(course.Id, lecturer.Id, criteriaCount: 1);
        context.ExamQuestions.Add(qOneCriteria);
        await context.SaveChangesAsync();

        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { qOneCriteria.Id },
            SubmissionNotes: "Câu hỏi có rubric 1 tiêu chí"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ít hơn 2 tiêu chí");
    }
}
