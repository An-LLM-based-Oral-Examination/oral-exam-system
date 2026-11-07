using FluentValidation;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.SyncSemesterCourseOfferings;

public sealed class SyncSemesterCourseOfferingsCommandValidator
    : AbstractValidator<SyncSemesterCourseOfferingsCommand>
{
    public SyncSemesterCourseOfferingsCommandValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.SelectedCourseIds).NotNull();
        RuleForEach(x => x.SelectedCourseIds).NotEmpty();
    }
}
