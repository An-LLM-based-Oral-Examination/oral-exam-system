using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OralExamination.Domain.Identity;

/// <summary>
/// Username sinh viên: tối đa 3 chữ cái đầu của tên, chữ cái đầu của họ,
/// chữ cái đầu của tên đệm (nếu có), rồi mã sinh viên. Bỏ dấu, viết thường.
/// Nguyễn Trọng Tốt + SE181863 = totntse181863.
/// </summary>
public static class StudentUsername
{
    public const int GivenNameLength = 3;
    public const int StudentCodeLength = 8;
    public const int MaxLength = GivenNameLength + 2 + StudentCodeLength;

    private static readonly Regex StudentCodePattern = new("^[a-z]{2}[0-9]{6}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

        var code = AsciiLettersAndDigits(Fold(studentCode));
        if (!StudentCodePattern.IsMatch(code))
        {
            return false;
        }

        var givenName = tokens[^1];
        var givenPart = givenName.Length <= GivenNameLength
            ? givenName
            : givenName[..GivenNameLength];
        var familyInitial = tokens[0][0];
        var middleInitial = tokens.Count >= 3 ? tokens[1][0].ToString() : string.Empty;
        username = string.Concat(givenPart, familyInitial, middleInitial, code);
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

    private static string AsciiLettersAndDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
