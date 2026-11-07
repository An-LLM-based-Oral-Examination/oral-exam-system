using FluentValidation;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;

public class CreateClassCommandValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã lớp (Code) không được để trống.")
            .MaximumLength(50).WithMessage("Mã lớp không được vượt quá 50 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên lớp (Name) không được để trống.")
            .MaximumLength(150).WithMessage("Tên lớp không được vượt quá 150 ký tự.");

        When(x => !x.CourseOfferingId.HasValue || x.CourseOfferingId == Guid.Empty, () =>
        {
            RuleFor(x => x.CourseId)
                .NotEmpty().WithMessage("Mã môn học (CourseId) không được để trống.");

            RuleFor(x => x.SemesterId)
                .NotEmpty().WithMessage("Mã học kỳ (SemesterId) không được để trống.");
        });

        RuleFor(x => x.LecturerId)
            .NotEmpty().WithMessage("Mã giảng viên phụ trách (LecturerId) không được để trống.");

        RuleFor(x => x.MaxStudents)
            .GreaterThan(0).WithMessage("Số lượng sinh viên tối đa phải lớn hơn 0.")
            .LessThanOrEqualTo(200).WithMessage("Số lượng sinh viên tối đa không được vượt quá 200.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Lớp tạo từ course offering phải có mật khẩu tham gia cố định.")
            .MinimumLength(8).WithMessage("Mật khẩu tham gia lớp phải có ít nhất 8 ký tự.")
            .When(x => x.CourseOfferingId.HasValue && x.CourseOfferingId != Guid.Empty && x.AllowSelfEnrollment);
    }
}
