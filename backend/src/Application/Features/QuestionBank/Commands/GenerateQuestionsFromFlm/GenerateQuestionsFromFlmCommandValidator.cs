using System.Linq;
using FluentValidation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromFlm;

public sealed class GenerateQuestionsFromFlmCommandValidator : AbstractValidator<GenerateQuestionsFromFlmCommand>
{
    public GenerateQuestionsFromFlmCommandValidator()
    {
        RuleFor(v => v.CourseId)
            .NotEmpty().WithMessage("Mã môn học (CourseId) không được để trống.");

        RuleFor(v => v.RequesterUserId)
            .NotEmpty().WithMessage("Định danh người yêu cầu không được để trống.");

        RuleFor(v => v.NumberOfQuestions)
            .InclusiveBetween(1, 10).WithMessage("Số lượng câu hỏi sinh trong 1 đợt phải từ 1 đến 10.");

        RuleFor(v => v.SelectedCloCodes)
            .NotEmpty().WithMessage("Cần chọn ít nhất 1 mã chuẩn đầu ra (CLO).");

        RuleFor(v => v.Topics)
            .NotEmpty().WithMessage("Cần chọn ít nhất 1 chủ đề bài học.");

        RuleFor(v => v.UsageScope)
            .Must(s => UsageScope.All.Contains(s))
            .WithMessage("Phạm vi sử dụng phải là 'practice', 'exam', hoặc 'shared'.");

        RuleFor(v => v.TargetBloomLevels)
            .NotEmpty().WithMessage("Cần chọn ít nhất 1 bậc nhận thức Bloom.")
            .Must(levels => levels.All(l => BloomLevel.All.Contains(l)))
            .WithMessage("Bậc nhận thức Bloom không hợp lệ.");

        RuleFor(v => v.DifficultyDistribution)
            .NotNull().WithMessage("Cần cung cấp phân bổ độ khó.")
            .Must((cmd, dist) => dist.Easy >= 0 && dist.Medium >= 0 && dist.Hard >= 0 &&
                                 (dist.Easy + dist.Medium + dist.Hard) == cmd.NumberOfQuestions)
            .WithMessage("Tổng phân bổ độ khó (easy + medium + hard) phải bằng chính xác số lượng câu hỏi yêu cầu.");
    }
}
