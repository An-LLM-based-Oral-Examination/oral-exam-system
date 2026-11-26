using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OralExamination.Domain.Identity;

/// <summary>
/// Username sinh viên chuẩn FPT: tên cuối đầy đủ (bỏ dấu, viết hoa chữ cái đầu) +
/// chữ cái đầu viết hoa của họ và toàn bộ tên đệm + mã sinh viên viết hoa.
/// Ví dụ: Nguyễn Quang Thành + SE184236 = ThanhNQSE184236;
/// Nguyễn Thị Cẩm Hương + SE181863 = HuongNTCSE181863.
/// </summary>
public static class StudentUsername
{
    public const int StudentCodeLength = 8;
    public const int MaxLength = 50;

    private static readonly Regex StudentCodePattern = new("^[a-z]{2}[0-9]{6}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StrictStudentCodePattern = new("^[A-Za-z]{2}[0-9]{6}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Normalize(string? value)
    {
        return Fold(value).Replace(" ", "", StringComparison.Ordinal);
    }

    public static bool TryCreate(string? fullName, string? studentCode, out string username)
    {
        username = string.Empty;
        var tokens = NameTokens(fullName);
        if (tokens.Count < 2)
        {
            return false;
        }

        var trimmedCode = studentCode?.Trim() ?? string.Empty;
        if (!StrictStudentCodePattern.IsMatch(trimmedCode))
        {
            return false;
        }

        var givenName = tokens[^1];
        var givenPart = givenName.Length == 1
            ? char.ToUpperInvariant(givenName[0]).ToString()
            : string.Concat(char.ToUpperInvariant(givenName[0]), givenName[1..]);

        var initialsBuilder = new StringBuilder(tokens.Count - 1);
        for (var i = 0; i < tokens.Count - 1; i++)
        {
            initialsBuilder.Append(char.ToUpperInvariant(tokens[i][0]));
        }

        var candidate = string.Concat(givenPart, initialsBuilder.ToString(), trimmedCode.ToUpperInvariant());
        if (candidate.Length > MaxLength)
        {
            return false;
        }

        username = candidate;
        return true;
    }

    public static bool TryExtractStudentCode(string? username, out string studentCode)
    {
        studentCode = string.Empty;
        var normalized = Normalize(username);
        if (normalized.Length < 2 + StudentCodeLength || normalized.Length > MaxLength)
        {
            return false;
        }

        var code = normalized[^StudentCodeLength..];
        if (!StudentCodePattern.IsMatch(code))
        {
            return false;
        }

        studentCode = code;
        return true;
    }

    private static List<string> NameTokens(string? fullName)
    {
        var tokens = new List<string>();
        foreach (var part in Fold(fullName).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var letters = AsciiLetters(part);
            if (letters.Length > 0)
            {
                tokens.Add(letters);
            }
        }

        return tokens;
    }

    private static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (character == 'đ' || character == 'Đ')
            {
                builder.Append('d');
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                builder.Append(' ');
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string AsciiLetters(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= 'a' and <= 'z')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
