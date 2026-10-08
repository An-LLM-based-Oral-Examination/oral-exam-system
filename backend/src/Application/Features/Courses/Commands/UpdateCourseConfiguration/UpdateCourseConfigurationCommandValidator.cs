using System;
using System.Linq;
using FluentValidation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;

/// <summary>
/// Validator cho lệnh cập nhật cấu hình môn học:
/// - CourseId không rỗng.
/// - TranscriptBufferSeconds thuộc đoạn [10, 300].
/// - MaxFollowUpQuestions thuộc đoạn [1, 2].
/// - ExamInputMode hợp lệ (null hoặc nằm trong ExamInputMode.All).
/// </summary>
public sealed class UpdateCourseConfigurationCommandValidator : AbstractValidator<UpdateCourseConfigurationCommand>
{
    public UpdateCourseConfigurationCommandValidator()
    {
        RuleFor(v => v.CourseId)
            .NotEmpty()
            .WithMessage("Mã môn học không được để trống.");

        RuleFor(v => v.TranscriptBufferSeconds)
            .InclusiveBetween(10, 300)
            .WithMessage("Thời gian đệm chỉnh sửa transcript phải nằm trong khoảng từ 10 đến 300 giây.");

        RuleFor(v => v.MaxFollowUpQuestions)
            .InclusiveBetween(1, 5)
            .WithMessage("Số lượng câu hỏi phụ tối đa phải nằm trong khoảng từ 1 đến 5 câu.");

        RuleFor(v => v.ExamInputMode)
            .Must(m => m == null || ExamInputMode.All.Contains(m))
            .WithMessage("Phương thức thi ExamInputMode không hợp lệ. Chỉ chấp nhận: VoiceOnly, VoiceWithTranscriptEdit.");
    }
}
