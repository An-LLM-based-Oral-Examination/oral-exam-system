using FluentValidation;

namespace OralExamination.Application.Features.Courses.Commands.CreateCourse;

public sealed class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã môn học (Code) không được để trống.")
            .MaximumLength(50).WithMessage("Mã môn học không được vượt quá 50 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên môn học (Name) không được để trống.")
            .MaximumLength(255).WithMessage("Tên môn học không được vượt quá 255 ký tự.");

        RuleFor(x => x.Credits)
            .InclusiveBetween(1, 10).WithMessage("Số tín chỉ (Credits) phải nằm trong khoảng từ 1 đến 10.");

        RuleFor(x => x.MaxMockExamsPerDay)
            .InclusiveBetween(1, 20).WithMessage("Hạn ngạch thi thử mỗi ngày (MaxMockExamsPerDay) phải từ 1 đến 20.");

        RuleFor(x => x.MockExamDurationMinutes)
            .InclusiveBetween(5, 180).WithMessage("Thời lượng thi thử (MockExamDurationMinutes) phải từ 5 đến 180 phút.");

        RuleFor(x => x.MockExamTotalQuestions)
            .InclusiveBetween(1, 20).WithMessage("Tổng số câu hỏi thi thử (MockExamTotalQuestions) phải từ 1 đến 20 câu.");
    }
}
