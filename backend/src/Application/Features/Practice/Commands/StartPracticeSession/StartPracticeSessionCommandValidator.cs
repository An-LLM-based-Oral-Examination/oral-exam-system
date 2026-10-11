using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OralExamination.Application.Features.Practice.Commands.StartPracticeSession;

public sealed class StartPracticeSessionCommandValidator : AbstractValidator<StartPracticeSessionCommand>
{
    private static readonly string[] AllowedDifficulties = ["easy", "medium", "hard", "progressive"];
    private static readonly string[] AllowedSingleDifficulties = ["easy", "medium", "hard"];

    public StartPracticeSessionCommandValidator()
    {
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
                .NotNull().WithMessage("Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ 3 đến 10 câu.")
                .InclusiveBetween(3, 10)
                .WithMessage("Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ 3 đến 10 câu.");
        });

        When(v => string.Equals(v.Difficulty?.Trim(), "progressive", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(v => v.QuestionCount)
                .InclusiveBetween(3, 10)
                .WithMessage("Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ 3 đến 10 câu.");
        });
    }
}
