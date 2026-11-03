using FluentValidation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourseClo;

public sealed class UpdateCourseCloCommandValidator : AbstractValidator<UpdateCourseCloCommand>
{
    private static readonly string[] AllowedBloomLevels =
    [
        BloomLevel.Remember,
        BloomLevel.Understand,
        BloomLevel.Apply,
        BloomLevel.Analyze,
        BloomLevel.Evaluate,
        BloomLevel.Create
    ];

    public UpdateCourseCloCommandValidator()
    {
        RuleFor(x => x.CourseId)
            .NotEmpty().WithMessage("CourseId không được để trống.");

        RuleFor(x => x.CloId)
            .NotEmpty().WithMessage("CloId không được để trống.");

        RuleFor(x => x.CloCode)
            .NotEmpty().WithMessage("Mã chuẩn đầu ra (CloCode) không được để trống.")
            .MaximumLength(30).WithMessage("Mã chuẩn đầu ra không được vượt quá 30 ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả chuẩn đầu ra không được để trống.")
            .MaximumLength(1000).WithMessage("Mô tả chuẩn đầu ra không được vượt quá 1000 ký tự.");

        RuleFor(x => x.BloomLevel)
            .NotEmpty().WithMessage("Cấp độ Bloom không được để trống.")
            .Must(level => AllowedBloomLevels.Contains(level))
            .WithMessage($"Cấp độ Bloom không hợp lệ. Cho phép: {string.Join(", ", AllowedBloomLevels)}");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Thứ tự hiển thị (OrderIndex) phải lớn hơn hoặc bằng 1.");
    }
}
