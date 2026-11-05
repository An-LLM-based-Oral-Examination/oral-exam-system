using FluentValidation;

namespace OralExamination.Application.Features.Appeals.Commands.BatchAssignAppeals;

public sealed class BatchAssignAppealsCommandValidator : AbstractValidator<BatchAssignAppealsCommand>
{
    public BatchAssignAppealsCommandValidator()
    {
        RuleFor(command => command.OfficerId)
            .NotEmpty()
            .WithMessage("Không xác định được người thực hiện phân công.");

        RuleFor(command => command.Assignments)
            .NotEmpty()
            .WithMessage("Danh sách phân công không được để trống.");

        RuleForEach(command => command.Assignments).ChildRules(item =>
        {
            item.RuleFor(i => i.AppealId)
                .NotEmpty()
                .WithMessage("Mã đơn phúc khảo không được để trống.");

            item.RuleFor(i => i.LecturerId)
                .NotEmpty()
                .WithMessage("Mã giảng viên không được để trống.");
        });
    }
}
