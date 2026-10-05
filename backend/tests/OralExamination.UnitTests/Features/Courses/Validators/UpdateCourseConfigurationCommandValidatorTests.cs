using System;
using FluentAssertions;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using Xunit;

namespace OralExamination.UnitTests.Features.Courses.Validators;

public class UpdateCourseConfigurationCommandValidatorTests
{
    private readonly UpdateCourseConfigurationCommandValidator _validator = new();

    private UpdateCourseConfigurationCommand CreateValidCommand() => new(
        CourseId: Guid.NewGuid(),
        TranscriptBufferSeconds: 60,
        MaxFollowUpQuestions: 1,
        HasFollowUp: true
    );

    [Fact(DisplayName = "1. Pass khi cấu hình môn học hợp lệ theo khoảng quy chuẩn")]
    public void Validate_Should_Pass_When_Command_Is_Valid()
    {
        var command = CreateValidCommand();
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "2. Fail khi CourseId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_CourseId_Is_Empty()
    {
        var command = CreateValidCommand() with { CourseId = Guid.Empty };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.CourseId));
    }

    [Theory(DisplayName = "3. Fail khi TranscriptBufferSeconds nằm ngoài đoạn [10, 300]")]
    [InlineData(9)]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(301)]
    [InlineData(500)]
    public void Validate_Should_Fail_When_Buffer_Seconds_Is_Out_Of_Range(int bufferSeconds)
    {
        var command = CreateValidCommand() with { TranscriptBufferSeconds = bufferSeconds };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.TranscriptBufferSeconds));
    }

    [Theory(DisplayName = "4. Pass khi TranscriptBufferSeconds nằm trong đoạn [10, 300]")]
    [InlineData(10)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(300)]
    public void Validate_Should_Pass_When_Buffer_Seconds_Is_In_Range(int bufferSeconds)
    {
        var command = CreateValidCommand() with { TranscriptBufferSeconds = bufferSeconds };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "5. Fail khi MaxFollowUpQuestions nằm ngoài đoạn [1, 5]")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(10)]
    public void Validate_Should_Fail_When_Max_Follow_Up_Is_Out_Of_Range(int maxFollowUp)
    {
        var command = CreateValidCommand() with { MaxFollowUpQuestions = maxFollowUp };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.MaxFollowUpQuestions));
    }

    [Theory(DisplayName = "6. Pass khi MaxFollowUpQuestions nằm trong đoạn [1, 5]")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Validate_Should_Pass_When_Max_Follow_Up_Is_In_Range(int maxFollowUp)
    {
        var command = CreateValidCommand() with { MaxFollowUpQuestions = maxFollowUp };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }
}
