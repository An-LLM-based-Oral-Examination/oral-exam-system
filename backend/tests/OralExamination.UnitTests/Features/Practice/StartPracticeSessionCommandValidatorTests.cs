using System;
using FluentAssertions;
using FluentValidation.TestHelper;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class StartPracticeSessionCommandValidatorTests
{
    private readonly StartPracticeSessionCommandValidator _validator = new();

    private static StartPracticeSessionCommand CreateValidCommand(int questionCount = 5, string difficulty = "medium") => new(
        StudentId: Guid.NewGuid(),
        CourseId: Guid.NewGuid(),
        Difficulty: difficulty,
        QuestionCount: questionCount,
        IsFullSession: false,
        Topic: "Clean Architecture"
    );

    [Theory(DisplayName = "1. Pass khi QuestionCount hợp lệ lớn hơn 0 (1, 5, 10, 20)")]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Validate_Should_Pass_When_QuestionCount_Is_Valid(int questionCount)
    {
        var command = CreateValidCommand(questionCount);
        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.QuestionCount);
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "2. Fail khi QuestionCount nhỏ hơn hoặc bằng 0 (0, -1, -5)")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Validate_Should_Fail_When_QuestionCount_Is_Zero_Or_Negative(int questionCount)
    {
        var command = CreateValidCommand(questionCount);
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QuestionCount)
            .WithErrorMessage("Số lượng câu hỏi luyện tập phải lớn hơn 0.");
        result.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "3. Pass khi Difficulty hợp lệ không phân biệt hoa thường (easy, medium, hard, Easy, MEDIUM)")]
    [InlineData("easy")]
    [InlineData("medium")]
    [InlineData("hard")]
    [InlineData("Easy")]
    [InlineData("MEDIUM")]
    [InlineData("HARD")]
    [InlineData("progressive")]
    [InlineData("Progressive")]
    public void Validate_Should_Pass_When_Difficulty_Is_Valid(string difficulty)
    {
        var command = CreateValidCommand(difficulty: difficulty);
        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Difficulty);
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "4. Fail khi Difficulty là null, rỗng hoặc khoảng trắng")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_When_Difficulty_Is_Null_Or_Empty(string? difficulty)
    {
        var command = CreateValidCommand(difficulty: difficulty!);
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Difficulty);
        result.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "5. Fail khi Difficulty không nằm trong danh sách (easy, medium, hard)")]
    [InlineData("expert")]
    [InlineData("super_hard")]
    [InlineData("invalid")]
    [InlineData("kho")]
    public void Validate_Should_Fail_When_Difficulty_Is_Not_Allowed(string difficulty)
    {
        var command = CreateValidCommand(difficulty: difficulty);
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Difficulty)
            .WithErrorMessage("Độ khó không hợp lệ. Chỉ chấp nhận các giá trị: easy, medium, hard, progressive.");
        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "6. Fail khi StudentId là Guid.Empty")]
    public void Validate_Should_Fail_When_StudentId_Is_Empty()
    {
        var command = CreateValidCommand() with { StudentId = Guid.Empty };
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.StudentId)
            .WithErrorMessage("StudentId không được để trống.");
        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "7. Pass khi StudentId hợp lệ")]
    public void Validate_Should_Pass_When_StudentId_Is_Valid()
    {
        var command = CreateValidCommand();
        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.StudentId);
    }

    [Fact(DisplayName = "8. Fail khi CourseId là Guid.Empty")]
    public void Validate_Should_Fail_When_CourseId_Is_Empty()
    {
        var command = CreateValidCommand() with { CourseId = Guid.Empty };
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CourseId)
            .WithErrorMessage("CourseId không được để trống.");
        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "9. Pass khi CourseId hợp lệ")]
    public void Validate_Should_Pass_When_CourseId_Is_Valid()
    {
        var command = CreateValidCommand();
        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.CourseId);
    }

    [Fact(DisplayName = "10. Pass toàn diện khi toàn bộ thuộc tính của lệnh bắt đầu luyện tập đều hợp lệ")]
    public void Validate_Should_Pass_When_All_Properties_Are_Valid()
    {
        var command = CreateValidCommand(questionCount: 5, difficulty: "easy");
        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "11. Pass khi Difficulty là progressive và QuestionCount nằm trong khoảng [3, 10]")]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    public void Validate_Should_Pass_When_Progressive_And_QuestionCount_Between_3_And_10(int questionCount)
    {
        var command = CreateValidCommand(questionCount: questionCount, difficulty: "progressive");
        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.QuestionCount);
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "12. Fail khi Difficulty là progressive và QuestionCount nhỏ hơn 3")]
    [InlineData(1)]
    [InlineData(2)]
    public void Validate_Should_Fail_When_Progressive_And_QuestionCount_Less_Than_3(int questionCount)
    {
        var command = CreateValidCommand(questionCount: questionCount, difficulty: "progressive");
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QuestionCount)
            .WithErrorMessage("Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ 3 đến 10 câu.");
        result.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "13. Fail khi Difficulty là progressive và QuestionCount lớn hơn 10")]
    [InlineData(11)]
    [InlineData(15)]
    public void Validate_Should_Fail_When_Progressive_And_QuestionCount_Greater_Than_10(int questionCount)
    {
        var command = CreateValidCommand(questionCount: questionCount, difficulty: "progressive");
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QuestionCount)
            .WithErrorMessage("Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ 3 đến 10 câu.");
        result.IsValid.Should().BeFalse();
    }
}
