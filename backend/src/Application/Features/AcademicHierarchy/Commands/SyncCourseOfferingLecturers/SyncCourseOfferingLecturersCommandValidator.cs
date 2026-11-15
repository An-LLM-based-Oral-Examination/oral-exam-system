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
        RuleFor(x => x.LecturerUserIds).Must(ids => ids == null || ids.Count <= 200)
            .WithMessage("Chỉ phân công tối đa 200 giảng viên trong một lần.");
        RuleForEach(x => x.LecturerUserIds).NotEmpty();
    }
}
