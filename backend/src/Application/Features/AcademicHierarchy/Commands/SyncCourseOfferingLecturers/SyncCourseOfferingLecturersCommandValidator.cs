using FluentValidation;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.SyncCourseOfferingLecturers;

public sealed class SyncCourseOfferingLecturersCommandValidator
    : AbstractValidator<SyncCourseOfferingLecturersCommand>
{
    public SyncCourseOfferingLecturersCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.CourseOfferingId).NotEmpty();
        RuleFor(x => x.LecturerUserIds).NotNull();
        RuleForEach(x => x.LecturerUserIds).NotEmpty();
    }
}
