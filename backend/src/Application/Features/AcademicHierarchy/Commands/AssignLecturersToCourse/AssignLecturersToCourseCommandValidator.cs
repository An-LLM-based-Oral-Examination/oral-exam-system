using FluentValidation;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AssignLecturersToCourse;

public class AssignLecturersToCourseCommandValidator : AbstractValidator<AssignLecturersToCourseCommand>
{
    public AssignLecturersToCourseCommandValidator()
    {
        RuleFor(x => x.CourseId)
            .NotEmpty().WithMessage("Mã môn học (CourseId) không được để trống.");

        RuleFor(x => x.LecturerUserIds)
            .NotEmpty().WithMessage("Danh sách giảng viên không được để trống.")
            .Must(ids => ids != null && ids.Count > 0).WithMessage("Phải cung cấp ít nhất một giảng viên.");

        RuleFor(x => x.AssignedByUserId)
            .NotEmpty().WithMessage("Mã người thực hiện phân công không được để trống.");
    }
}
