using FluentValidation;

namespace OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;

public sealed class AssignAppealLecturerCommandValidator : AbstractValidator<AssignAppealLecturerCommand>
{
    public AssignAppealLecturerCommandValidator()
    {
        RuleFor(x => x.AppealId).NotEmpty();
        RuleFor(x => x.DepartmentHeadId).NotEmpty();
        RuleFor(x => x.LecturerId).NotEmpty();
    }
}
