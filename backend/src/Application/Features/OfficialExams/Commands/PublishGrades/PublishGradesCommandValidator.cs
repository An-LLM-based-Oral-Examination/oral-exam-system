using System;
using FluentValidation;

namespace OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;

/// <summary>
/// Validator cho lệnh công bố điểm ca thi:
/// - ShiftId không được để trống.
/// - LecturerId không được để trống.
/// </summary>
public sealed class PublishGradesCommandValidator : AbstractValidator<PublishGradesCommand>
{
    public PublishGradesCommandValidator()
    {
        RuleFor(v => v.ShiftId)
            .NotEmpty()
            .WithMessage("Mã ca thi không được để trống.");

        RuleFor(v => v.LecturerId)
            .NotEmpty()
            .WithMessage("Mã giảng viên công bố điểm không được để trống.");
    }
}
