using System;
using FluentValidation;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;

/// <summary>
/// Validator cho GetMockExamQuotaQuery.
/// Đảm bảo CourseId và StudentId hợp lệ.
/// </summary>
public sealed class GetMockExamQuotaQueryValidator : AbstractValidator<GetMockExamQuotaQuery>
{
    public GetMockExamQuotaQueryValidator()
    {
        RuleFor(x => x.CourseId)
            .NotEmpty()
            .WithMessage("Mã môn học (CourseId) không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty()
            .WithMessage("Mã sinh viên (StudentId) không được để trống.");
    }
}
