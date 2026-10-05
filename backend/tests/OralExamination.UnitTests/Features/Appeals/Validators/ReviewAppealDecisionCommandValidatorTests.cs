using System;
using FluentAssertions;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using Xunit;

namespace OralExamination.UnitTests.Features.Appeals.Validators;

public class ReviewAppealDecisionCommandValidatorTests
{
    private readonly ReviewAppealDecisionCommandValidator _validator = new();

    private ReviewAppealDecisionCommand CreateValidCommand(string decision = "APPROVED", decimal? proposedScore = 8.5m) => new(
        AppealId: Guid.NewGuid(),
        ReviewerId: Guid.NewGuid(),
        Decision: decision,
        ProposedScore: proposedScore,
        ReviewNotes: "Sau khi nghe lại bản ghi âm và đối soát transcript, hội đồng quyết định nâng điểm lên 8.5."
    );

    [Fact(DisplayName = "1. Pass khi phê duyệt (APPROVED) với điểm đề xuất hợp lệ")]
    public void Validate_Should_Pass_When_Approved_With_Valid_Score()
    {
        var command = CreateValidCommand("APPROVED", 8.5m);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "2. Pass khi từ chối (REJECTED) với điểm đề xuất là null")]
    public void Validate_Should_Pass_When_Rejected_Without_Score()
    {
        var command = CreateValidCommand("REJECTED", null) with
        {
            ReviewNotes = "Câu trả lời của sinh viên hoàn toàn thiếu ý chính theo barem rubric 10.0."
        };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "3. Fail khi AppealId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_AppealId_Is_Empty()
    {
        var command = CreateValidCommand() with { AppealId = Guid.Empty };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.AppealId));
    }

    [Fact(DisplayName = "4. Fail khi ReviewerId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_ReviewerId_Is_Empty()
    {
        var command = CreateValidCommand() with { ReviewerId = Guid.Empty };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.ReviewerId));
    }

    [Theory(DisplayName = "5. Fail khi Decision không phải APPROVED hoặc REJECTED")]
    [InlineData("PENDING")]
    [InlineData("IN_REVIEW")]
    [InlineData("CANCELLED")]
    [InlineData("unknown")]
    [InlineData("")]
    public void Validate_Should_Fail_When_Decision_Is_Invalid(string decision)
    {
        var command = CreateValidCommand(decision, 7.0m);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.Decision));
    }

    [Theory(DisplayName = "6. Fail khi ReviewNotes rỗng hoặc ngắn hơn 10 ký tự")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789")]
    public void Validate_Should_Fail_When_ReviewNotes_Is_Invalid(string? notes)
    {
        var command = CreateValidCommand() with { ReviewNotes = notes! };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.ReviewNotes));
    }

    [Fact(DisplayName = "7. Fail khi Decision là APPROVED nhưng ProposedScore bị null")]
    public void Validate_Should_Fail_When_Approved_But_ProposedScore_Is_Null()
    {
        var command = CreateValidCommand("APPROVED", null);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.ProposedScore));
    }

    [Fact(DisplayName = "8. Fail khi ProposedScore âm (< 0.00m)")]
    public void Validate_Should_Fail_When_ProposedScore_Is_Negative()
    {
        var command = CreateValidCommand("APPROVED", -0.5m);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.ProposedScore));
    }

    [Fact(DisplayName = "9. Fail khi ProposedScore vượt quá 10.00m")]
    public void Validate_Should_Fail_When_ProposedScore_Exceeds_10()
    {
        var command = CreateValidCommand("APPROVED", 10.5m);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ReviewAppealDecisionCommand.ProposedScore));
    }

    [Theory(DisplayName = "10. Pass khi ProposedScore nằm chính xác tại ranh giới biên (0.00m và 10.00m)")]
    [InlineData(0.0)]
    [InlineData(10.0)]
    public void Validate_Should_Pass_When_ProposedScore_Is_At_Boundaries(double score)
    {
        var command = CreateValidCommand("APPROVED", (decimal)score);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
