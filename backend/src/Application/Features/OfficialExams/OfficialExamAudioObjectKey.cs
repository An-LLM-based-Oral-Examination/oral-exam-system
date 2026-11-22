using System;
using System.Text.RegularExpressions;

namespace OralExamination.Application.Features.OfficialExams;

public static partial class OfficialExamAudioObjectKey
{
    public static string Create(Guid shiftId, Guid ticketId, int seatNumber, string studentCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentCode);
        var normalizedStudentCode = StudentCodeCharacters()
            .Replace(studentCode.Trim().ToUpperInvariant(), string.Empty);

        if (string.IsNullOrWhiteSpace(normalizedStudentCode))
        {
            throw new ArgumentException("Mã số sinh viên không hợp lệ.", nameof(studentCode));
        }

        var fileName = $"{seatNumber:D2}_{normalizedStudentCode}.webm";
        return $"official-exams/{shiftId:D}/{ticketId:D}/{fileName}";
    }

    public static string CreateForQuestion(
        Guid shiftId,
        Guid ticketId,
        int seatNumber,
        string studentCode,
        int orderIndex,
        int followUpIndex = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentCode);
        if (orderIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(orderIndex), "Thứ tự câu hỏi phải lớn hơn hoặc bằng 1.");
        }

        if (followUpIndex < 0 || followUpIndex > OfficialExamRules.MaxFollowUpQuestions)
        {
            throw new ArgumentOutOfRangeException(nameof(followUpIndex), "Chỉ số câu hỏi phụ phải từ 0 đến 5.");
        }

        var normalizedStudentCode = StudentCodeCharacters()
            .Replace(studentCode.Trim().ToUpperInvariant(), string.Empty);

        if (string.IsNullOrWhiteSpace(normalizedStudentCode))
        {
            throw new ArgumentException("Mã số sinh viên không hợp lệ.", nameof(studentCode));
        }

        var fileName = followUpIndex == 0
            ? $"{seatNumber:D2}_{normalizedStudentCode}_Q{orderIndex:D2}.webm"
            : $"{seatNumber:D2}_{normalizedStudentCode}_Q{orderIndex:D2}_FU{followUpIndex}.webm";

        return $"official-exams/{shiftId:D}/{ticketId:D}/{fileName}";
    }

    [GeneratedRegex("[^A-Z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex StudentCodeCharacters();
}
