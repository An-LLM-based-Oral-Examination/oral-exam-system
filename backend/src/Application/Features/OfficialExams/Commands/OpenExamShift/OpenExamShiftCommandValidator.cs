using FluentValidation;

namespace OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;

public sealed class OpenExamShiftCommandValidator : AbstractValidator<OpenExamShiftCommand>
{
    public OpenExamShiftCommandValidator()
    {
        RuleFor(command => command.ActorUserId)
            .NotEmpty()
            .WithMessage("Không xác định được người mở ca thi.");

        RuleFor(command => command.SessionId)
            .NotEmpty()
            .WithMessage("Kỳ thi không được để trống.");

        RuleFor(command => command.ShiftName)
            .NotEmpty()
            .WithMessage("Tên ca thi không được để trống.")
            .MaximumLength(100)
            .WithMessage("Tên ca thi không được dài quá 100 ký tự.");

        RuleFor(command => command.RoomLab)
            .NotEmpty()
            .WithMessage("Phòng lab không được để trống.")
            .MaximumLength(50)
            .WithMessage("Phòng lab không được dài quá 50 ký tự.");

        RuleFor(command => command.ProctorUserId)
            .NotEmpty()
            .WithMessage("Người coi thi không được để trống.");

        RuleFor(command => command.EndTime)
            .GreaterThan(command => command.StartTime)
            .WithMessage("Giờ kết thúc phải sau giờ bắt đầu.");
    }
}
