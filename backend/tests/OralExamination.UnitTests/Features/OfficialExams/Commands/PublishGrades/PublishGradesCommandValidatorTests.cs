using System;
using FluentAssertions;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams.Commands.PublishGrades;

public class PublishGradesCommandValidatorTests
{
    private readonly PublishGradesCommandValidator _validator = new();

    [Fact(DisplayName = "1. Validator chấp thuận khi ShiftId và LecturerId đều hợp lệ")]
    public void Validator_Should_Pass_When_Command_Is_Valid()
    {
        var command = new PublishGradesCommand(
            ShiftId: Guid.NewGuid(),
            LecturerId: Guid.NewGuid()
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "2. Validator chặn khi ShiftId là Guid.Empty")]
    public void Validator_Should_Fail_When_ShiftId_Is_Empty()
    {
        var command = new PublishGradesCommand(
            ShiftId: Guid.Empty,
            LecturerId: Guid.NewGuid()
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(PublishGradesCommand.ShiftId));
    }

    [Fact(DisplayName = "3. Validator chặn khi LecturerId là Guid.Empty")]
    public void Validator_Should_Fail_When_LecturerId_Is_Empty()
    {
        var command = new PublishGradesCommand(
            ShiftId: Guid.NewGuid(),
            LecturerId: Guid.Empty
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(PublishGradesCommand.LecturerId));
    }

    [Fact(DisplayName = "4. Validator chặn khi cả ShiftId và LecturerId đều rỗng")]
    public void Validator_Should_Fail_When_Both_Ids_Are_Empty()
    {
        var command = new PublishGradesCommand(
            ShiftId: Guid.Empty,
            LecturerId: Guid.Empty
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }
}
