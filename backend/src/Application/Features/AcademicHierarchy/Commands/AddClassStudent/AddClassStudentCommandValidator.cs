using FluentValidation;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.AddClassStudent;

public sealed class AddClassStudentCommandValidator : AbstractValidator<AddClassStudentCommand>
{
    public AddClassStudentCommandValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("ClassId không được để trống.");

        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("ActorUserId không được để trống.");

        RuleFor(x => x)
            .Must(x => (x.StudentId.HasValue && x.StudentId.Value != System.Guid.Empty) ||
                       !string.IsNullOrWhiteSpace(x.StudentCode) ||
                       !string.IsNullOrWhiteSpace(x.Email) ||
                       !string.IsNullOrWhiteSpace(x.Identifier))
            .WithMessage("VALIDATION: Vui lòng cung cấp ít nhất một thông tin nhận diện sinh viên (StudentId, StudentCode, Email hoặc Identifier).");
    }
}
