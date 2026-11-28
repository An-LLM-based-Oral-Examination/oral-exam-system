using FluentValidation;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourse;

public sealed class UpdateCourseCommandValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseCommandValidator()
    {
        RuleFor(x => x.CourseId)
            .NotEmpty().WithMessage("Định danh môn học (CourseId) không được để trống.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã môn học (Code) không được để trống.")
            .MaximumLength(20).WithMessage("Mã môn học không được vượt quá 20 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên môn học (Name) không được để trống.")
            .MaximumLength(200).WithMessage("Tên môn học không được vượt quá 200 ký tự.");

        RuleFor(x => x.Credits)
            .InclusiveBetween(1, 10).WithMessage("Số tín chỉ (Credits) phải nằm trong khoảng từ 1 đến 10.");

        RuleFor(x => x.MaxMockExamsPerDay)
            .InclusiveBetween(1, 20).WithMessage("Hạn ngạch thi thử mỗi ngày (MaxMockExamsPerDay) phải từ 1 đến 20.");

        RuleFor(x => x.MockExamDurationMinutes)
            .GreaterThanOrEqualTo(1).WithMessage("Thời lượng thi thử (MockExamDurationMinutes) phải lớn hơn hoặc bằng 1 phút.");

        RuleFor(x => x.MockExamTotalQuestions)
            .InclusiveBetween(1, 100).WithMessage("Tổng số câu hỏi thi thử (MockExamTotalQuestions) phải từ 1 đến 100 câu.");
    }
}
