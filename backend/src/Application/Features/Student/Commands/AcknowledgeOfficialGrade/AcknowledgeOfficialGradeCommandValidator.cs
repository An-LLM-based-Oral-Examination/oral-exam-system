using FluentValidation;

namespace OralExamination.Application.Features.Student.Commands.AcknowledgeOfficialGrade;

/// <summary>
/// Validator kiểm tra đầu vào lệnh xác nhận điểm thi thật của sinh viên.
/// </summary>
public sealed class AcknowledgeOfficialGradeCommandValidator : AbstractValidator<AcknowledgeOfficialGradeCommand>
{
    public AcknowledgeOfficialGradeCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty()
            .WithMessage("Mã vé thi (TicketId) không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty()
            .WithMessage("Mã sinh viên (StudentId) không được để trống.");
    }
}
