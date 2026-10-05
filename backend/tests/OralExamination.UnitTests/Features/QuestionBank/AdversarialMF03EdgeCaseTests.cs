using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng (Adversarial Edge Case Tests) cho Luồng MF-03.
/// Được thiết kế độc lập bởi Challenger 1 nhằm rà soát các điều kiện cực trị,
/// ma trận ca biên (boundary values), độ chính xác số học thập phân (decimal precision),
/// tính nguyên tử (atomicity) và tính bảo mật phân quyền (RBAC gates).
/// </summary>
public class AdversarialMF03EdgeCaseTests
{
    private readonly CreateDraftQuestionCommandValidator _createValidator = new();
    private readonly BatchSubmitQuestionsForReviewCommandValidator _batchValidator = new();
    private readonly ReviewQuestionDecisionCommandValidator _reviewValidator = new();

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Lecturer, User Head, Course Course)> SeedEntitiesAsync(OralExamDbContext context)
    {
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = $"lecturer.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Giảng Viên Kiểm Thử",
            Role = "lecturer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var head = new User
        {
            Id = Guid.NewGuid(),
            Email = $"head.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Trưởng Bộ Môn Thẩm Định",
            Role = "department_head",
            IsActive = true,
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

        context.Users.AddRange(lecturer, head);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        return (lecturer, head, course);
    }

    private CreateDraftQuestionCommand BuildCreateCommand(
        Guid courseId,
        Guid lecturerId,
        string? sampleAnswer,
        List<RubricCriterionDraftDto> criteria)
    {
        var rubric = new RubricDraftDto(
            Name: "Barem Rubric Đối Kháng",
            Description: "Barem thẩm định ca biên",
            TotalMaxScore: criteria.Sum(c => c.MaxScore),
            Criteria: criteria
        );

        return new CreateDraftQuestionCommand(
            CourseId: courseId,
            LecturerId: lecturerId,
            Title: "Câu hỏi đối kháng kiểm thử ca biên",
            Content: "Nội dung câu hỏi mô phỏng các điều kiện cực trị",
            SampleAnswer: sampleAnswer,
            KeyPoints: new List<string> { "EdgeCase1", "EdgeCase2" },
            Difficulty: QuestionDifficulty.Hard,
            BloomLevel: BloomLevel.Evaluate,
            Source: "manual",
            UsageScope: "official_exam",
            HasFollowUp: false,
            FollowUpPrompt: null,
            Rubric: rubric
        );
    }

    #region 1. BAREM RUBRIC EXTREME DECIMAL PRECISION & ANOMALIES

    [Fact(DisplayName = "ADV-01: Validator PHẢI chặn Barem Rubric 3 tiêu chí có tổng điểm 9.9999m")]
    public void Validator_Should_Reject_Rubric_When_Sum_Is_9_9999m()
    {
        // 3.3333 + 3.3333 + 3.3333 = 9.9999m
        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí A", 3.3333m, 33.33m, "understand", "Mô tả A", 1),
            new("Tiêu chí B", 3.3333m, 33.33m, "apply", "Mô tả B", 2),
            new("Tiêu chí C", 3.3333m, 33.34m, "analyze", "Mô tả C", 3)
        };

        var command = BuildCreateCommand(Guid.NewGuid(), Guid.NewGuid(), new string('X', 50), criteria);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Rubric.Criteria" &&
                                            e.ErrorMessage.Contains("10.00 điểm"));
    }

    [Fact(DisplayName = "ADV-02: Validator PHẢI chặn Barem Rubric 3 tiêu chí có tổng điểm 10.0001m")]
    public void Validator_Should_Reject_Rubric_When_Sum_Is_10_0001m()
    {
        // 3.3334 + 3.3334 + 3.3333 = 10.0001m
        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí A", 3.3334m, 33.33m, "understand", "Mô tả A", 1),
            new("Tiêu chí B", 3.3334m, 33.33m, "apply", "Mô tả B", 2),
            new("Tiêu chí C", 3.3333m, 33.34m, "analyze", "Mô tả C", 3)
        };

        var command = BuildCreateCommand(Guid.NewGuid(), Guid.NewGuid(), new string('X', 50), criteria);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Rubric.Criteria" &&
                                            e.ErrorMessage.Contains("10.00 điểm"));
    }

    [Fact(DisplayName = "ADV-03: Handler PHẢI từ chối tạo câu hỏi khi Barem Rubric có tổng 9.9999m")]
    public async Task Handler_Should_Return_Failure_When_Rubric_Sum_Is_9_9999m()
    {
        using var context = CreateDbContext();
        var (lecturer, _, course) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí 1", 3.3333m, 33.33m, "understand", "Mô tả", 1),
            new("Tiêu chí 2", 3.3333m, 33.33m, "apply", "Mô tả", 2),
            new("Tiêu chí 3", 3.3333m, 33.34m, "analyze", "Mô tả", 3)
        };

        var command = BuildCreateCommand(course.Id, lecturer.Id, new string('X', 50), criteria);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("10.00 điểm");
    }

    [Fact(DisplayName = "ADV-04: Handler PHẢI từ chối tạo câu hỏi khi Barem Rubric có tổng 10.0001m")]
    public async Task Handler_Should_Return_Failure_When_Rubric_Sum_Is_10_0001m()
    {
        using var context = CreateDbContext();
        var (lecturer, _, course) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí 1", 3.3334m, 33.33m, "understand", "Mô tả", 1),
            new("Tiêu chí 2", 3.3334m, 33.33m, "apply", "Mô tả", 2),
            new("Tiêu chí 3", 3.3333m, 33.34m, "analyze", "Mô tả", 3)
        };

        var command = BuildCreateCommand(course.Id, lecturer.Id, new string('X', 50), criteria);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("10.00 điểm");
    }

    [Fact(DisplayName = "ADV-05a: Validator PHẢI chặn tiêu chí điểm âm (-5.00m) dù tổng điểm bù trừ là 10.00m")]
    public void Validator_Should_Reject_Negative_Criteria_Score()
    {
        // 15.00m + (-5.00m) = 10.00m
        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí Dương", 15.00m, 150m, "understand", "Mô tả", 1),
            new("Tiêu chí Âm", -5.00m, -50m, "apply", "Mô tả", 2)
        };

        var command = BuildCreateCommand(Guid.NewGuid(), Guid.NewGuid(), new string('X', 50), criteria);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("lớn hơn 0"));
    }

    [Fact(DisplayName = "ADV-05b: Handler PHẢI chặn và từ chối lưu khi tiêu chí có MaxScore <= 0")]
    public async Task Handler_Should_Return_Failure_When_Criteria_Has_Negative_Score()
    {
        using var context = CreateDbContext();
        var (lecturer, _, course) = await SeedEntitiesAsync(context);
        var handler = new CreateDraftQuestionCommandHandler(context);

        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí Dương", 15.00m, 150m, "understand", "Mô tả", 1),
            new("Tiêu chí Âm", -5.00m, -50m, "apply", "Mô tả", 2)
        };

        var command = BuildCreateCommand(course.Id, lecturer.Id, new string('X', 50), criteria);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("lớn hơn 0");
    }

    [Fact(DisplayName = "ADV-05c: Validator PHẢI chặn tiêu chí điểm 0.00m (10.00m + 0.00m)")]
    public void Validator_Should_Reject_Zero_Criteria_Score()
    {
        // 10.00m + 0.00m = 10.00m
        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí 1", 10.00m, 100m, "understand", "Mô tả", 1),
            new("Tiêu chí 0 điểm", 0.00m, 0m, "apply", "Mô tả", 2)
        };

        var command = BuildCreateCommand(Guid.NewGuid(), Guid.NewGuid(), new string('X', 50), criteria);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("lớn hơn 0"));
    }

    #endregion

    #region 2. MODEL ANSWER BOUNDARY TESTS (49, 50 CHARS, UNICODE WHITESPACE)

    [Fact(DisplayName = "ADV-06: Model Answer đúng 49 ký tự PHẢI bị Validator từ chối")]
    public void Validator_Should_Reject_Model_Answer_With_Exactly_49_Chars()
    {
        string ans49 = new string('M', 49);
        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí 1", 5.00m, 50m, "understand", "Mô tả 1", 1),
            new("Tiêu chí 2", 5.00m, 50m, "apply", "Mô tả 2", 2)
        };

        var command = BuildCreateCommand(Guid.NewGuid(), Guid.NewGuid(), ans49, criteria);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.SampleAnswer) &&
                                            e.ErrorMessage.Contains("50 ký tự trở lên"));
    }

    [Fact(DisplayName = "ADV-07: Model Answer đúng 50 ký tự PHẢI được Validator chấp thuận")]
    public void Validator_Should_Accept_Model_Answer_With_Exactly_50_Chars()
    {
        string ans50 = new string('M', 50);
        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí 1", 5.00m, 50m, "understand", "Mô tả 1", 1),
            new("Tiêu chí 2", 5.00m, 50m, "apply", "Mô tả 2", 2)
        };

        var command = BuildCreateCommand(Guid.NewGuid(), Guid.NewGuid(), ans50, criteria);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-08: Model Answer dài 50 ký tự nhưng có 2 khoảng trắng đầu/cuối (48 ký tự thực) PHẢI bị từ chối")]
    public void Validator_Should_Reject_Model_Answer_When_Length_Is_50_But_Trimmed_Length_Is_48()
    {
        // 1 khoảng trắng đầu + 48 ký tự + 1 khoảng trắng cuối = tổng độ dài chuỗi 50, nhưng Trim() ra 48
        string paddedAnswer = " " + new string('M', 48) + " ";
        paddedAnswer.Length.Should().Be(50);

        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí 1", 5.00m, 50m, "understand", "Mô tả 1", 1),
            new("Tiêu chí 2", 5.00m, 50m, "apply", "Mô tả 2", 2)
        };

        var command = BuildCreateCommand(Guid.NewGuid(), Guid.NewGuid(), paddedAnswer, criteria);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.SampleAnswer) &&
                                            e.ErrorMessage.Contains("50 ký tự trở lên"));
    }

    [Fact(DisplayName = "ADV-09: Model Answer chứa khoảng trắng Unicode (Fullwidth/Ideographic Space) Trim ra < 50 ký tự PHẢI bị từ chối")]
    public void Validator_Should_Reject_Model_Answer_With_Unicode_Ideographic_Whitespace_Padded()
    {
        // \u3000 là Ideographic Space (khoảng trắng toàn phần kiểu Á Đông)
        string unicodePadded = "\u3000" + new string('U', 48) + "\u3000";

        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí 1", 5.00m, 50m, "understand", "Mô tả 1", 1),
            new("Tiêu chí 2", 5.00m, 50m, "apply", "Mô tả 2", 2)
        };

        var command = BuildCreateCommand(Guid.NewGuid(), Guid.NewGuid(), unicodePadded, criteria);

        var result = _createValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.SampleAnswer));
    }

    #endregion

    #region 3. BATCH SUBMIT ATOMIC INTEGRITY (TOÀN BỘ MẺ PHẢI BỊ TỪ CHỐI NẾU CÓ 1 CÂU LỖI)

    [Fact(DisplayName = "ADV-10: Batch Submit chứa 3 câu hỏi (2 câu hợp lệ, 1 câu lỗi Model Answer 40 ký tự) PHẢI bị từ chối toàn bộ mẻ và KHÔNG câu nào được đổi trạng thái")]
    public async Task BatchSubmit_Should_Reject_Entire_Batch_Atomically_When_One_Question_Has_Short_Answer()
    {
        using var context = CreateDbContext();
        var (lecturer, _, course) = await SeedEntitiesAsync(context);
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        // Tạo 3 câu hỏi: Q1 hợp lệ, Q2 câu trả lời 40 ký tự, Q3 hợp lệ
        var q1 = CreateExamQuestionInDb(course.Id, lecturer.Id, "Câu 1", new string('A', 60), 10.00m);
        var q2 = CreateExamQuestionInDb(course.Id, lecturer.Id, "Câu 2 (Lỗi)", new string('B', 40), 10.00m);
        var q3 = CreateExamQuestionInDb(course.Id, lecturer.Id, "Câu 3", new string('C', 70), 10.00m);

        context.ExamQuestions.AddRange(q1, q2, q3);
        await context.SaveChangesAsync();

        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { q1.Id, q2.Id, q3.Id },
            SubmissionNotes: "Nộp mẻ 3 câu hỏi hỗn hợp"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        // Bắt buộc thất bại
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Câu 2 (Lỗi)");
        result.Error.Should().Contain("ngắn hơn 50 ký tự");

        // KIỂM CHỨNG TÍNH NGUYÊN TỬ (ATOMICITY): Cả Q1, Q2, Q3 PHẢI VẪN LÀ DRAFT
        var reloadedQuestions = await context.ExamQuestions
            .Where(q => q.Id == q1.Id || q.Id == q2.Id || q.Id == q3.Id)
            .ToListAsync();

        reloadedQuestions.Should().HaveCount(3);
        foreach (var q in reloadedQuestions)
        {
            q.ApprovalStatus.Should().Be(ApprovalStatus.Draft, 
                $"Câu hỏi {q.Title} không được phép cập nhật sang SUBMITTED_FOR_REVIEW khi mẻ thất bại!");
        }
    }

    [Fact(DisplayName = "ADV-11: Batch Submit chứa 3 câu hỏi (2 câu hợp lệ, 1 câu lỗi Rubric lệch 9.99m) PHẢI bị từ chối toàn bộ mẻ")]
    public async Task BatchSubmit_Should_Reject_Entire_Batch_Atomically_When_One_Question_Has_Faulty_Rubric()
    {
        using var context = CreateDbContext();
        var (lecturer, _, course) = await SeedEntitiesAsync(context);
        var handler = new BatchSubmitQuestionsForReviewCommandHandler(context);

        var q1 = CreateExamQuestionInDb(course.Id, lecturer.Id, "Câu 1", new string('A', 60), 10.00m);
        var q2 = CreateExamQuestionInDb(course.Id, lecturer.Id, "Câu 2 (Rubric 9.99đ)", new string('B', 60), 9.99m);
        var q3 = CreateExamQuestionInDb(course.Id, lecturer.Id, "Câu 3", new string('C', 70), 10.00m);

        context.ExamQuestions.AddRange(q1, q2, q3);
        await context.SaveChangesAsync();

        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            QuestionIds: new List<Guid> { q1.Id, q2.Id, q3.Id },
            SubmissionNotes: "Nộp mẻ kèm rubric lỗi"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Câu 2 (Rubric 9.99đ)");
        result.Error.Should().Contain("lệch 10.00đ");

        // Kiểm chứng tính nguyên tử
        var q1Reloaded = await context.ExamQuestions.FindAsync(q1.Id);
        q1Reloaded!.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
    }

    #endregion

    #region 4. REVIEW DECISION EXTREME EDGE CASES

    [Theory(DisplayName = "ADV-12: Validator PHẢI chặn các Decision viết thường hoặc sai chuẩn ('approved', 'needs_revision', 'PENDING', 'DRAFT')")]
    [InlineData("approved")]
    [InlineData("needs_revision")]
    [InlineData("rejected")]
    [InlineData("PENDING")]
    [InlineData("DRAFT")]
    [InlineData("CANCELLED")]
    [InlineData("PASS")]
    [InlineData(" ")]
    public void ReviewDecisionValidator_Should_Reject_Invalid_Or_Lowercase_Decisions(string invalidDecision)
    {
        var command = new ReviewQuestionDecisionCommand(
            QuestionId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: invalidDecision,
            ReviewNotes: "Ý kiến nhận xét hợp lệ từ Trưởng Bộ Môn.",
            TargetUsageScope: "official_exam"
        );

        var result = _reviewValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewQuestionDecisionCommand.Decision));
    }

    [Fact(DisplayName = "ADV-13: ReviewNotes đúng 9 ký tự PHẢI bị Validator từ chối")]
    public void ReviewDecisionValidator_Should_Reject_Notes_With_9_Characters()
    {
        var command = new ReviewQuestionDecisionCommand(
            QuestionId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "123456789", // 9 ký tự
            TargetUsageScope: "official_exam"
        );

        var result = _reviewValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewQuestionDecisionCommand.ReviewNotes) &&
                                            e.ErrorMessage.Contains("10 ký tự trở lên"));
    }

    [Fact(DisplayName = "ADV-14: ReviewNotes đúng 10 ký tự PHẢI được Validator chấp thuận")]
    public void ReviewDecisionValidator_Should_Accept_Notes_With_10_Characters()
    {
        var command = new ReviewQuestionDecisionCommand(
            QuestionId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "1234567890", // đúng 10 ký tự
            TargetUsageScope: "official_exam"
        );

        var result = _reviewValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ADV-15: Handler PHẢI chặn phê duyệt (APPROVED) nếu câu hỏi trong CSDL có Rubric lệch 9.9999m")]
    public async Task ReviewDecisionHandler_Should_Block_Approval_If_Question_Rubric_Is_9_9999m()
    {
        using var context = CreateDbContext();
        var (_, head, course) = await SeedEntitiesAsync(context);
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var question = CreateExamQuestionInDb(course.Id, head.Id, "Câu hỏi thẩm định", new string('M', 60), 9.9999m);
        question.ApprovalStatus = ApprovalStatus.SubmittedForReview;
        context.ExamQuestions.Add(question);
        await context.SaveChangesAsync();

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: question.Id,
            ReviewerId: head.Id,
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "Cố tình phê duyệt câu hỏi có barem lệch điểm",
            TargetUsageScope: "official_exam"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("lệch 10.00đ");

        var dbQuestion = await context.ExamQuestions.FindAsync(question.Id);
        dbQuestion!.ApprovalStatus.Should().Be(ApprovalStatus.SubmittedForReview);
    }

    [Fact(DisplayName = "ADV-16: Giảng viên ('lecturer') tự ý gọi ReviewDecision thẩm định đề PHẢI bị chặn")]
    public async Task ReviewDecisionHandler_Should_Block_Lecturer_From_Reviewing()
    {
        using var context = CreateDbContext();
        var (lecturer, _, course) = await SeedEntitiesAsync(context);
        var handler = new ReviewQuestionDecisionCommandHandler(context);

        var question = CreateExamQuestionInDb(course.Id, lecturer.Id, "Câu hỏi PRN231", new string('M', 60), 10.00m);
        context.ExamQuestions.Add(question);
        await context.SaveChangesAsync();

        var command = new ReviewQuestionDecisionCommand(
            QuestionId: question.Id,
            ReviewerId: lecturer.Id, // Giảng viên tự thẩm định
            Decision: ApprovalStatus.Approved,
            ReviewNotes: "Giảng viên tự duyệt đề của mình",
            TargetUsageScope: "official_exam"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Chỉ Trưởng Bộ Môn ('department_head') hoặc Quản trị viên");
    }

    #endregion

    private ExamQuestion CreateExamQuestionInDb(
        Guid courseId,
        Guid submittedBy,
        string title,
        string sampleAnswer,
        decimal rubricTotal)
    {
        var rubric = new Rubric
        {
            CourseId = courseId,
            Name = "Barem Rubric",
            TotalMaxScore = rubricTotal,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        rubric.Criteria.Add(new RubricCriterion
        {
            CriterionName = "Tiêu chí 1",
            MaxScore = rubricTotal / 2m,
            Weight = 50m,
            BloomLevel = "understand",
            OrderIndex = 1
        });
        rubric.Criteria.Add(new RubricCriterion
        {
            CriterionName = "Tiêu chí 2",
            MaxScore = rubricTotal / 2m,
            Weight = 50m,
            BloomLevel = "apply",
            OrderIndex = 2
        });

        return new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Rubric = rubric,
            Title = title,
            Content = "Nội dung câu hỏi mô phỏng",
            SampleAnswer = sampleAnswer,
            KeyPoints = "[]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            Source = "manual",
            ApprovalStatus = ApprovalStatus.Draft,
            SubmittedBy = submittedBy,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
