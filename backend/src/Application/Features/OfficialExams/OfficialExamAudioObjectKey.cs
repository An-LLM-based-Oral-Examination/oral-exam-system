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

    [GeneratedRegex("[^A-Z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex StudentCodeCharacters();
}
