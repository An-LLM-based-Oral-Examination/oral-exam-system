using System;
using System.Collections.Generic;
using FluentAssertions;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Enums;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank.Validators;

public class CreateDraftQuestionCommandValidatorTests
{
    private readonly CreateDraftQuestionCommandValidator _validator;

    public CreateDraftQuestionCommandValidatorTests()
    {
        _validator = new CreateDraftQuestionCommandValidator();
    }

    private CreateDraftQuestionCommand CreateValidCommand(
        string? sampleAnswer = "Đây là câu trả lời mẫu đạt chuẩn tối thiểu 50 ký tự cho câu hỏi thi vấn đáp phần mềm.",
        decimal totalRubricScore = 10.00m)
    {
        // Phân bổ 2 tiêu chí sao cho tổng bằng totalRubricScore
        decimal part1 = 5.00m;
        decimal part2 = totalRubricScore - part1;

        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Kiến thức cốt lõi", part1, 50m, "understand", "Mô tả tiêu chí 1", 1),
            new("Khả năng phân tích", part2, 50m, "analyze", "Mô tả tiêu chí 2", 2)
        };

        var rubric = new RubricDraftDto(
            "Barem Rubric Kiến Trúc",
            "Mô tả barem",
            totalRubricScore,
            criteria
        );

        return new CreateDraftQuestionCommand(
            CourseId: Guid.NewGuid(),
            LecturerId: Guid.NewGuid(),
            Title: "Giải thích Clean Architecture 4 tầng trong .NET 8",
            Content: "Trình bày sự phân tách giữa Domain, Application, Infrastructure và Presentation layer.",
            SampleAnswer: sampleAnswer,
            KeyPoints: new List<string> { "Domain thuần khiết", "Application CQRS", "Dependency Inversion" },
            Difficulty: QuestionDifficulty.Medium,
            BloomLevel: BloomLevel.Analyze,
            Source: "manual",
            UsageScope: "official_exam",
            HasFollowUp: true,
            FollowUpPrompt: "Tại sao Domain không được phụ thuộc trực tiếp vào EF Core?",
            Rubric: rubric
        );
    }

    [Fact(DisplayName = "1. Pass khi dữ liệu câu hỏi hợp lệ và Model Answer đúng 50 ký tự")]
    public void Validate_Should_Pass_When_SampleAnswer_Has_Exactly_50_Characters()
    {
        // 50 ký tự chính xác
        string sampleAnswer50 = new string('A', 50);
        var command = CreateValidCommand(sampleAnswer: sampleAnswer50);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "2. Pass khi Model Answer dài hơn 50 ký tự")]
    public void Validate_Should_Pass_When_SampleAnswer_Has_More_Than_50_Characters()
    {
        string sampleAnswer150 = new string('B', 150);
        var command = CreateValidCommand(sampleAnswer: sampleAnswer150);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "3. Pass khi SampleAnswer là null hoặc rỗng (chưa cung cấp câu trả lời mẫu khi tạo nháp)")]
    public void Validate_Should_Pass_When_SampleAnswer_Is_Null_Or_Empty()
    {
        var commandWithNull = CreateValidCommand(sampleAnswer: null);
        var resultNull = _validator.Validate(commandWithNull);
        resultNull.IsValid.Should().BeTrue();

        var commandWithEmpty = CreateValidCommand(sampleAnswer: "");
        var resultEmpty = _validator.Validate(commandWithEmpty);
        resultEmpty.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "4. Fail khi Model Answer được cung cấp nhưng ngắn hơn 50 ký tự (49 ký tự)")]
    public void Validate_Should_Fail_When_SampleAnswer_Has_Fewer_Than_50_Characters()
    {
        string sampleAnswer49 = new string('C', 49);
        var command = CreateValidCommand(sampleAnswer: sampleAnswer49);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.SampleAnswer) &&
                                            e.ErrorMessage.Contains("50 ký tự trở lên"));
    }

    [Fact(DisplayName = "5. Fail khi CourseId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_CourseId_Is_Empty()
    {
        var command = CreateValidCommand() with { CourseId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.CourseId));
    }

    [Fact(DisplayName = "6. Fail khi LecturerId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_LecturerId_Is_Empty()
    {
        var command = CreateValidCommand() with { LecturerId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.LecturerId));
    }

    [Fact(DisplayName = "7. Fail khi Title rỗng hoặc vượt quá 300 ký tự")]
    public void Validate_Should_Fail_When_Title_Is_Empty_Or_Too_Long()
    {
        var commandEmpty = CreateValidCommand() with { Title = "" };
        var resultEmpty = _validator.Validate(commandEmpty);
        resultEmpty.IsValid.Should().BeFalse();
        resultEmpty.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.Title));

        var commandTooLong = CreateValidCommand() with { Title = new string('X', 301) };
        var resultTooLong = _validator.Validate(commandTooLong);
        resultTooLong.IsValid.Should().BeFalse();
        resultTooLong.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.Title));
    }

    [Fact(DisplayName = "8. Fail khi Content câu hỏi rỗng")]
    public void Validate_Should_Fail_When_Content_Is_Empty()
    {
        var command = CreateValidCommand() with { Content = "   " };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.Content));
    }

    [Fact(DisplayName = "9. Fail khi Difficulty không nằm trong tập ('easy', 'medium', 'hard')")]
    public void Validate_Should_Fail_When_Difficulty_Is_Invalid()
    {
        var command = CreateValidCommand() with { Difficulty = "super_hard" };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.Difficulty));
    }

    [Fact(DisplayName = "10. Fail khi BloomLevel không hợp lệ theo thang Bloom")]
    public void Validate_Should_Fail_When_BloomLevel_Is_Invalid()
    {
        var command = CreateValidCommand() with { BloomLevel = "expert" };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDraftQuestionCommand.BloomLevel));
    }

    [Fact(DisplayName = "11. RÀNG BUỘC BẤT BIẾN: Pass khi tổng điểm các tiêu chí Barem Rubric đúng chính xác 10.00 điểm")]
    public void Validate_Should_Pass_When_Rubric_Criteria_Sum_Equals_Exactly_10_00()
    {
        // 3 tiêu chí: 4.0 + 3.0 + 3.0 = 10.00
        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí 1", 4.00m, 40m, "understand", "Mô tả 1", 1),
            new("Tiêu chí 2", 3.00m, 30m, "analyze", "Mô tả 2", 2),
            new("Tiêu chí 3", 3.00m, 30m, "apply", "Mô tả 3", 3)
        };

        var rubric = new RubricDraftDto("Rubric chuẩn 10đ", "Mô tả", 10.00m, criteria);
        var command = CreateValidCommand() with { Rubric = rubric };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "12. RÀNG BUỘC BẤT BIẾN: Fail khi tổng điểm Barem Rubric < 10.00 (ví dụ 9.50)")]
    public void Validate_Should_Fail_When_Rubric_Criteria_Sum_Is_Less_Than_10_00()
    {
        var command = CreateValidCommand(totalRubricScore: 9.50m);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Rubric.Criteria" &&
                                            e.ErrorMessage.Contains("10.00 điểm"));
    }

    [Fact(DisplayName = "13. RÀNG BUỘC BẤT BIẾN: Fail khi tổng điểm Barem Rubric > 10.00 (ví dụ 10.50)")]
    public void Validate_Should_Fail_When_Rubric_Criteria_Sum_Is_Greater_Than_10_00()
    {
        var command = CreateValidCommand(totalRubricScore: 10.50m);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Rubric.Criteria" &&
                                            e.ErrorMessage.Contains("10.00 điểm"));
    }

    [Fact(DisplayName = "14. Fail khi Barem Rubric có ít hơn 2 tiêu chí con")]
    public void Validate_Should_Fail_When_Rubric_Has_Fewer_Than_Two_Criteria()
    {
        var criteria = new List<RubricCriterionDraftDto>
        {
            new("Tiêu chí duy nhất", 10.00m, 100m, "understand", "Chỉ có 1 tiêu chí", 1)
        };

        var rubric = new RubricDraftDto("Rubric 1 tiêu chí", "Mô tả", 10.00m, criteria);
        var command = CreateValidCommand() with { Rubric = rubric };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Rubric.Criteria" &&
                                            e.ErrorMessage.Contains("tối thiểu 2 tiêu chí"));
    }

    [Fact(DisplayName = "15. Fail khi Tên Barem Rubric rỗng")]
    public void Validate_Should_Fail_When_Rubric_Name_Is_Empty()
    {
        var validCommand = CreateValidCommand();
        var rubricWithoutName = validCommand.Rubric with { Name = "   " };
        var command = validCommand with { Rubric = rubricWithoutName };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Rubric.Name");
    }
}
