using System.Text.RegularExpressions;
using FluentValidation;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.UpdateAcademicProgram;

public sealed class UpdateAcademicProgramCommandValidator
    : AbstractValidator<UpdateAcademicProgramCommand>
{
    private static readonly Regex AllowedCodePattern = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    public UpdateAcademicProgramCommandValidator()
    {
        RuleFor(x => x.AcademicProgramId)
            .NotEmpty().WithMessage("Thiếu mã định danh chương trình đào tạo.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã chương trình đào tạo không được để trống.")
            .Must(code => !string.IsNullOrWhiteSpace(code) && code.Trim().Length is >= 2 and <= 20)
            .WithMessage("Mã chương trình đào tạo phải từ 2 đến 20 ký tự.")
            .Must(code => !string.IsNullOrWhiteSpace(code) && AllowedCodePattern.IsMatch(code.Trim()))
            .WithMessage("Mã chương trình đào tạo chỉ được chứa chữ cái, chữ số, dấu gạch ngang (-) hoặc gạch dưới (_).");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên chương trình đào tạo không được để trống.")
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length is >= 2 and <= 200)
            .WithMessage("Tên chương trình đào tạo phải từ 2 đến 200 ký tự.");
    }
}
