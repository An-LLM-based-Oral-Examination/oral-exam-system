using System;
using System.Linq;
using FluentValidation;
using OralExamination.Application.Common.Validation;
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
            .Must((cmd, seconds) => (IsAdminQuotaUpdate(cmd) && seconds == 0) || (seconds >= 10 && seconds <= 300))
            .WithMessage("Thời gian đệm chỉnh sửa transcript phải nằm trong khoảng từ 10 đến 300 giây.");

        RuleFor(v => v.MaxFollowUpQuestions)
            .Must((cmd, count) => (IsAdminQuotaUpdate(cmd) && count == 0) || (count >= 1 && count <= 5))
            .WithMessage("Số lượng câu hỏi phụ tối đa phải nằm trong khoảng từ 1 đến 5 câu.");

        RuleFor(v => v.ExamInputMode)
            .Must((cmd, mode) =>
                (IsAdminQuotaUpdate(cmd) && string.IsNullOrWhiteSpace(mode)) ||
                mode == null ||
                ExamInputMode.All.Contains(mode))
            .WithMessage("Phương thức thi ExamInputMode không hợp lệ. Chỉ chấp nhận: VoiceOnly, VoiceWithTranscriptEdit.");

        When(v => v.MaxMockExamsPerDay.HasValue, () =>
        {
            RuleFor(v => v.MaxMockExamsPerDay!.Value)
                .InclusiveBetween(1, 20)
                .WithMessage("Hạn ngạch thi thử mỗi ngày phải nằm trong khoảng từ 1 đến 20 lượt.");
        });

        When(v => v.MockExamDurationMinutes.HasValue, () =>
        {
            RuleFor(v => v.MockExamDurationMinutes)
                .GreaterThanOrEqualTo(1)
                .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
                .WithMessage("Thời lượng thi thử (MockExamDurationMinutes) phải lớn hơn hoặc bằng 1 phút.");
        });

        When(v => v.MockExamTotalQuestions.HasValue, () =>
        {
            RuleFor(v => v.MockExamTotalQuestions)
                .InclusiveBetween(1, 100)
                .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
                .WithMessage("Tổng số câu hỏi thi thử (MockExamTotalQuestions) phải từ 1 đến 100 câu.");
        });
    }

    private static bool IsAdminQuotaUpdate(UpdateCourseConfigurationCommand command) =>
        string.Equals(command.ActorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase) &&
        command.MaxMockExamsPerDay.HasValue;
}
