using FluentValidation;
using OralExamination.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OralExamination.Application.Features.Practice.Commands.StartPracticeSession;

public sealed class StartPracticeSessionCommandValidator : AbstractValidator<StartPracticeSessionCommand>
{
    private static readonly string[] AllowedDifficulties = ["easy", "medium", "hard", "progressive"];
    private static readonly string[] AllowedSingleDifficulties = ["easy", "medium", "hard"];
    private readonly IApplicationDbContext? _context;

    public StartPracticeSessionCommandValidator(IApplicationDbContext? context = null)
    {
        _context = context;

        RuleFor(v => v.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(v => v.CourseId)
            .NotEmpty().WithMessage("CourseId không được để trống.");

        RuleFor(v => v.Difficulty)
            .NotEmpty().WithMessage("Độ khó không được để trống.")
            .Must(d => !string.IsNullOrWhiteSpace(d) && AllowedDifficulties.Contains(d.Trim().ToLowerInvariant()))
            .WithMessage("Độ khó không hợp lệ. Chỉ chấp nhận các giá trị: easy, medium, hard, progressive.")
            .When(v => v.Difficulties == null || !v.Difficulties.Any());

        When(v => v.Difficulties != null && v.Difficulties.Any(), () =>
        {
            RuleForEach(v => v.Difficulties)
                .Must(d => !string.IsNullOrWhiteSpace(d) && AllowedSingleDifficulties.Contains(d.Trim().ToLowerInvariant()))
                .WithMessage("Độ khó không hợp lệ. Chỉ chấp nhận các giá trị: easy, medium, hard.");
        });

        When(v => v.QuestionCount.HasValue, () =>
        {
            RuleFor(v => v.QuestionCount)
                .GreaterThan(0)
                .WithMessage("Số lượng câu hỏi luyện tập phải lớn hơn 0.");
        });

        When(v => v.IsFullSession, () =>
        {
            RuleFor(v => v.QuestionCount)
                .NotNull().WithMessage(_ => GetMixedRangeMessage())
                .Must(IsWithinMixedQuestionRange)
                .WithMessage(_ => GetMixedRangeMessage());
        });

        When(v => string.Equals(v.Difficulty?.Trim(), "progressive", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(v => v.QuestionCount)
                .Must(count => !count.HasValue || IsWithinMixedQuestionRange(count))
                .WithMessage(_ => GetMixedRangeMessage());
        });
    }

    private (int Min, int Max) ResolveMixedQuestionBounds()
    {
        int minAllowed = 3;
        int maxAllowed = 10;

        if (_context != null)
        {
            try
            {
                var minConfig = _context.SystemConfigs.FirstOrDefault(c => c.Key == "MinMixedPracticeQuestions");
                if (minConfig != null && int.TryParse(minConfig.Value, out var parsedMin) && parsedMin > 0)
                {
                    minAllowed = parsedMin;
                }

                var maxConfig = _context.SystemConfigs.FirstOrDefault(c => c.Key == "MaxMixedPracticeQuestions");
                if (maxConfig != null && int.TryParse(maxConfig.Value, out var parsedMax) && parsedMax >= minAllowed)
                {
                    maxAllowed = parsedMax;
                }
            }
            catch
            {
                // Fallback an toàn về khoảng mặc định [3, 10]
            }
        }

        return (minAllowed, maxAllowed);
    }

    private bool IsWithinMixedQuestionRange(int? count)
    {
        if (!count.HasValue)
        {
            return false;
        }

        var (min, max) = ResolveMixedQuestionBounds();
        return count.Value >= min && count.Value <= max;
    }

    private string GetMixedRangeMessage()
    {
        var (min, max) = ResolveMixedQuestionBounds();
        return $"Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ {min} đến {max} câu.";
    }
}
