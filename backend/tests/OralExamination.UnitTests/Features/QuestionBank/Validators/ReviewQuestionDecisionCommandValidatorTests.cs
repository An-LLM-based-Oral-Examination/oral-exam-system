using System;
using FluentAssertions;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank.Validators;

public class ReviewQuestionDecisionCommandValidatorTests
{
    private readonly ReviewQuestionDecisionCommandValidator _validator;

    public ReviewQuestionDecisionCommandValidatorTests()
    {
        _validator = new ReviewQuestionDecisionCommandValidator();
    }

    private ReviewQuestionDecisionCommand CreateValidCommand(
        string decision = "APPROVED",
        string reviewNotes = "Câu hỏi và barem rubric đạt chuẩn chất lượng của Bộ Môn.")
    {
        return new ReviewQuestionDecisionCommand(
            QuestionId: Guid.NewGuid(),
            ReviewerId: Guid.NewGuid(),
            Decision: decision,
            ReviewNotes: reviewNotes,
            TargetUsageScope: "official_exam"
        );
    }

    [Theory(DisplayName = "1. Pass khi Decision là 1 trong 3 trạng thái hợp lệ: APPROVED, NEEDS_REVISION, REJECTED")]
    [InlineData("APPROVED")]
    [InlineData("NEEDS_REVISION")]
    [InlineData("REJECTED")]
    public void Validate_Should_Pass_When_Decision_Is_Valid(string validDecision)
    {
        var command = CreateValidCommand(decision: validDecision);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "2. Pass khi ReviewNotes đạt đúng chính xác 10 ký tự")]
    public void Validate_Should_Pass_When_ReviewNotes_Has_Exactly_10_Characters()
    {
        string tenCharsNotes = "1234567890";
        var command = CreateValidCommand(reviewNotes: tenCharsNotes);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "3. Fail khi QuestionId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_QuestionId_Is_Empty()
    {
        var command = CreateValidCommand() with { QuestionId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewQuestionDecisionCommand.QuestionId) &&
                                            e.ErrorMessage.Contains("Mã câu hỏi"));
    }

    [Fact(DisplayName = "4. Fail khi ReviewerId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_ReviewerId_Is_Empty()
    {
        var command = CreateValidCommand() with { ReviewerId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewQuestionDecisionCommand.ReviewerId) &&
                                            e.ErrorMessage.Contains("cán bộ thẩm định"));
    }

    [Theory(DisplayName = "5. Fail khi Decision không thuộc tập giá trị cho phép ('APPROVED', 'NEEDS_REVISION', 'REJECTED')")]
    [InlineData("PENDING")]
    [InlineData("DRAFT")]
    [InlineData("SUBMITTED_FOR_REVIEW")]
    [InlineData("PASS")]
    [InlineData("")]
    [InlineData("invalid_decision")]
    public void Validate_Should_Fail_When_Decision_Is_Invalid(string invalidDecision)
    {
        var command = CreateValidCommand(decision: invalidDecision);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewQuestionDecisionCommand.Decision) &&
                                            e.ErrorMessage.Contains("APPROVED, NEEDS_REVISION hoặc REJECTED"));
    }

    [Fact(DisplayName = "6. Fail khi ReviewNotes bị để trống hoặc rỗng")]
    public void Validate_Should_Fail_When_ReviewNotes_Is_Empty()
    {
        var command = CreateValidCommand(reviewNotes: "");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewQuestionDecisionCommand.ReviewNotes));
    }

    [Fact(DisplayName = "7. Fail khi ReviewNotes ngắn hơn 10 ký tự (9 ký tự)")]
    public void Validate_Should_Fail_When_ReviewNotes_Is_Shorter_Than_10_Characters()
    {
        string shortNotes = "123456789"; // 9 ký tự
        var command = CreateValidCommand(reviewNotes: shortNotes);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewQuestionDecisionCommand.ReviewNotes) &&
                                            e.ErrorMessage.Contains("10 ký tự"));
    }
}
