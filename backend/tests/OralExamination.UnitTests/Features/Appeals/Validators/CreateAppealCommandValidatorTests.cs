using System;
using FluentAssertions;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using Xunit;

namespace OralExamination.UnitTests.Features.Appeals.Validators;

public class CreateAppealCommandValidatorTests
{
    private readonly CreateAppealCommandValidator _validator = new();

    private CreateAppealCommand CreateValidCommand() => new(
        StudentId: Guid.NewGuid(),
        TicketId: Guid.NewGuid(),
        SubmissionId: null,
        Reason: "Em xin phúc khảo câu 1 vì câu trả lời của em đã nêu đủ 3 tầng Clean Architecture nhưng AI chấm điểm chưa thỏa đáng."
    );

    [Fact(DisplayName = "1. Pass khi dữ liệu đơn phúc khảo đầy đủ và hợp lệ")]
    public void Validate_Should_Pass_When_Command_Is_Valid()
    {
        var command = CreateValidCommand();
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "2. Fail khi StudentId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_StudentId_Is_Empty()
    {
        var command = CreateValidCommand() with { StudentId = Guid.Empty };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.StudentId));
    }

    [Fact(DisplayName = "3. Fail khi TicketId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_TicketId_Is_Empty()
    {
        var command = CreateValidCommand() with { TicketId = Guid.Empty };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.TicketId));
    }

    [Theory(DisplayName = "4. Fail khi Reason rỗng, null hoặc chỉ chứa khoảng trắng")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_When_Reason_Is_Empty_Or_Whitespace(string? reason)
    {
        var command = CreateValidCommand() with { Reason = reason! };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.Reason));
    }

    [Fact(DisplayName = "5. Fail khi Reason ngắn hơn 10 ký tự (9 ký tự)")]
    public void Validate_Should_Fail_When_Reason_Is_Shorter_Than_10_Chars()
    {
        var command = CreateValidCommand() with { Reason = "123456789" };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.Reason));
    }

    [Fact(DisplayName = "6. Pass khi Reason đạt đúng chính xác 10 ký tự")]
    public void Validate_Should_Pass_When_Reason_Has_Exactly_10_Chars()
    {
        var command = CreateValidCommand() with { Reason = "1234567890" };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "7. Fail khi Reason dài hơn 2000 ký tự (2001 ký tự)")]
    public void Validate_Should_Fail_When_Reason_Exceeds_2000_Chars()
    {
        var command = CreateValidCommand() with { Reason = new string('A', 2001) };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateAppealCommand.Reason));
    }

    [Fact(DisplayName = "8. Pass khi Reason đạt đúng chính xác 2000 ký tự")]
    public void Validate_Should_Pass_When_Reason_Has_Exactly_2000_Chars()
    {
        var command = CreateValidCommand() with { Reason = new string('A', 2000) };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
