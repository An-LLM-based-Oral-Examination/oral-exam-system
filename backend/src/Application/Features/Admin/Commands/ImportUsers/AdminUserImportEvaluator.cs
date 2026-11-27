using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Admin.DTOs;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Identity;

namespace OralExamination.Application.Features.Admin.Commands.ImportUsers;

internal sealed record EvaluatedImportRow(
    AdminUserImportPreviewRowDto Preview,
    string? EffectivePlainPassword);

internal static partial class AdminUserImportEvaluator
{
    private const string UpperAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowerAlphabet = "abcdefghijkmnopqrstuvwxyz";
    private const string DigitAlphabet = "23456789";
    private const string SymbolAlphabet = "!#$%&*?";
    private const string LetterAlphabet = UpperAlphabet + LowerAlphabet;
    private const string FullAlphabet = UpperAlphabet + LowerAlphabet + DigitAlphabet + SymbolAlphabet;

    [GeneratedRegex("^[a-z0-9._-]+$")]
    private static partial Regex LowerUsernameRegex();

    private static bool StartsWithFormulaTriggerChar(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var trimmedStart = value.TrimStart().TrimStart('\u200B', '\u200C', '\u200D', '\uFEFF');
        if (trimmedStart.Length == 0)
        {
            return false;
        }

        return trimmedStart[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ||
               value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';
    }

    public static string? ValidateDefaultPassword(string? defaultPassword)
    {
        if (string.IsNullOrEmpty(defaultPassword))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(defaultPassword) ||
            defaultPassword.Length < AdminUserImportRules.MinPasswordLength ||
            defaultPassword.Length > AdminUserImportRules.MaxPasswordLength)
        {
            return $"VALIDATION: Mật khẩu mặc định phải có độ dài từ {AdminUserImportRules.MinPasswordLength} đến {AdminUserImportRules.MaxPasswordLength} ký tự và không chỉ gồm khoảng trắng.";
        }

        if (StartsWithFormulaTriggerChar(defaultPassword))
        {
            return "VALIDATION: Mật khẩu mặc định không được bắt đầu bằng các ký tự '=', '+', '-', '@' hoặc ký tự điều khiển để đảm bảo an toàn khi xuất file Excel kết quả.";
        }

        return null;
    }

    public static string MapParseError(AdminUserImportParseResult parseResult)
    {
        var detail = parseResult.Errors.Count > 0
            ? string.Join(" ", parseResult.Errors)
            : "Tệp import không hợp lệ.";

        return parseResult.ErrorKind switch
        {
            AdminUserImportFileErrorKind.PayloadTooLarge => $"PAYLOAD_TOO_LARGE: {detail}",
            AdminUserImportFileErrorKind.UnsupportedMediaType => $"UNSUPPORTED_MEDIA_TYPE: {detail}",
            AdminUserImportFileErrorKind.TooManyRows => $"UNPROCESSABLE: {detail}",
            AdminUserImportFileErrorKind.InvalidFormat => $"UNPROCESSABLE: {detail}",
            _ => $"VALIDATION: {detail}"
        };
    }

    public static async Task<IReadOnlyList<EvaluatedImportRow>> EvaluateAsync(
        IApplicationDbContext context,
        IReadOnlyList<AdminUserImportRawRow> rawRows,
        string? defaultPassword,
        bool generatePasswordsForValidRows,
        CancellationToken cancellationToken)
    {
        var candidateEmails = new HashSet<string>(StringComparer.Ordinal);
        var candidateStudentCodes = new HashSet<string>(StringComparer.Ordinal);
        var candidateUsernames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in rawRows)
        {
            var email = raw.Email?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(email))
            {
                candidateEmails.Add(email);
            }

            var code = raw.StudentCode?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(code))
            {
                candidateStudentCodes.Add(code);
            }

            var role = raw.Role?.Trim().ToLowerInvariant();
            if (role == UserRole.Student &&
                !string.IsNullOrWhiteSpace(raw.FullName) &&
                !string.IsNullOrWhiteSpace(code) &&
                StudentUsername.TryCreate(raw.FullName.Trim(), code, out var generatedUsername))
            {
                candidateUsernames.Add(generatedUsername.ToLowerInvariant());
            }
            else if (!string.IsNullOrWhiteSpace(raw.Username))
            {
                candidateUsernames.Add(raw.Username.Trim().ToLowerInvariant());
            }
        }

        var dbEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dbStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dbUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (candidateEmails.Count > 0 || candidateStudentCodes.Count > 0 || candidateUsernames.Count > 0)
        {
            var emailList = candidateEmails.ToList();
            var codeList = candidateStudentCodes.ToList();
            var usernameList = candidateUsernames.ToList();

            var existingUsers = await context.Users
                .AsNoTracking()
                .Where(u =>
                    (emailList.Count > 0 && emailList.Contains(u.Email.ToLower())) ||
                    (codeList.Count > 0 && u.StudentCode != null && codeList.Contains(u.StudentCode.ToUpper())) ||
                    (usernameList.Count > 0 && u.Username != null && usernameList.Contains(u.Username.ToLower())))
                .Select(u => new { u.Email, u.StudentCode, u.Username })
                .ToListAsync(cancellationToken);

            foreach (var existing in existingUsers)
            {
                if (!string.IsNullOrWhiteSpace(existing.Email))
                {
                    dbEmails.Add(existing.Email.Trim());
                }

                if (!string.IsNullOrWhiteSpace(existing.StudentCode))
                {
                    dbStudentCodes.Add(existing.StudentCode.Trim());
                }

                if (!string.IsNullOrWhiteSpace(existing.Username))
                {
                    dbUsernames.Add(existing.Username.Trim());
                }
            }
        }

        var fileEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasDefaultPassword = !string.IsNullOrEmpty(defaultPassword);

        var evaluated = new List<EvaluatedImportRow>(rawRows.Count);

        foreach (var raw in rawRows)
        {
            var trimmedEmail = raw.Email?.Trim() ?? string.Empty;
            var normalizedEmail = trimmedEmail.ToLowerInvariant();
            var trimmedFullName = raw.FullName?.Trim() ?? string.Empty;
            var normalizedRole = raw.Role?.Trim().ToLowerInvariant() ?? string.Empty;
            var trimmedStudentCode = raw.StudentCode?.Trim().ToUpperInvariant();
            var trimmedUsername = raw.Username?.Trim();
            var rawPassword = raw.Password;

            string? validationError = null;
            string? resolvedStudentCode = null;
            string? resolvedUsername = null;

            if (string.IsNullOrWhiteSpace(trimmedEmail))
            {
                validationError = "Email không được để trống.";
            }
            else if (trimmedEmail.Length > 255)
            {
                validationError = "Email không được vượt quá 255 ký tự.";
            }
            else if (trimmedEmail.Any(char.IsWhiteSpace) ||
                     !MailAddress.TryCreate(trimmedEmail, out var parsedEmail) ||
                     !string.Equals(parsedEmail.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase))
            {
                validationError = "Định dạng email không hợp lệ.";
            }
            else if (StartsWithFormulaTriggerChar(trimmedEmail))
            {
                validationError = "Email không được bắt đầu bằng các ký tự '=', '+', '-', '@' hoặc ký tự điều khiển.";
            }
            else if (trimmedFullName.Length is < 2 or > 150)
            {
                validationError = "Họ và tên phải có độ dài từ 2 đến 150 ký tự.";
            }
            else if (StartsWithFormulaTriggerChar(trimmedFullName))
            {
                validationError = "Họ và tên không được bắt đầu bằng các ký tự '=', '+', '-', '@' hoặc ký tự điều khiển.";
            }
            else if (string.IsNullOrWhiteSpace(normalizedRole))
            {
                validationError = "Vai trò không được để trống.";
            }
            else if (string.Equals(normalizedRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
            {
                validationError = "Không cho phép import hàng loạt tài khoản vai trò admin. Vui lòng tạo thủ công.";
            }
            else if (!AdminUserImportRules.AllowedRoles.Contains(normalizedRole))
            {
                validationError = $"Vai trò '{raw.Role}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AdminUserImportRules.AllowedRoles)}.";
            }
            else if (normalizedRole == UserRole.Student)
            {
                if (!string.IsNullOrWhiteSpace(trimmedUsername))
                {
                    validationError = "Tài khoản sinh viên tự động sinh Username từ họ tên và MSSV, không được nhập Username thủ công.";
                }
                else if (string.IsNullOrWhiteSpace(trimmedStudentCode))
                {
                    validationError = "Mã sinh viên (MSSV) là bắt buộc đối với vai trò student.";
                }
                else if (trimmedStudentCode.Length > 20)
                {
                    validationError = "Mã sinh viên không được vượt quá 20 ký tự.";
                }
                else if (!StudentUsername.TryCreate(trimmedFullName, trimmedStudentCode, out var generatedUsername))
                {
                    validationError = "Không thể tự động sinh Username sinh viên. Họ và tên phải có ít nhất 2 từ chữ cái và MSSV đúng định dạng 2 chữ cái + 6 chữ số (VD: SE184236).";
                }
                else
                {
                    resolvedStudentCode = trimmedStudentCode;
                    resolvedUsername = generatedUsername;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(trimmedStudentCode))
                {
                    validationError = "Chỉ tài khoản sinh viên mới được phép nhập Mã sinh viên (MSSV).";
                }
                else if (!string.IsNullOrWhiteSpace(trimmedUsername))
                {
                    var lowerUsername = trimmedUsername.ToLowerInvariant();
                    if (lowerUsername.Length is < 3 or > 32)
                    {
                        validationError = "Tên đăng nhập phải có độ dài từ 3 đến 32 ký tự.";
                    }
                    else if (StartsWithFormulaTriggerChar(lowerUsername))
                    {
                        validationError = "Tên đăng nhập không được bắt đầu bằng các ký tự '=', '+', '-', '@' hoặc ký tự điều khiển.";
                    }
                    else if (!LowerUsernameRegex().IsMatch(lowerUsername))
                    {
                        validationError = "Tên đăng nhập chỉ được chứa chữ cái, chữ số, dấu chấm (.), gạch dưới (_) và gạch ngang (-).";
                    }
                    else
                    {
                        resolvedUsername = lowerUsername;
                    }
                }
            }

            if (validationError is null && !string.IsNullOrEmpty(rawPassword))
            {
                if (string.IsNullOrWhiteSpace(rawPassword) ||
                    rawPassword.Length < AdminUserImportRules.MinPasswordLength ||
                    rawPassword.Length > AdminUserImportRules.MaxPasswordLength)
                {
                    validationError = $"Mật khẩu trong file phải có độ dài từ {AdminUserImportRules.MinPasswordLength} đến {AdminUserImportRules.MaxPasswordLength} ký tự.";
                }
                else if (StartsWithFormulaTriggerChar(rawPassword))
                {
                    validationError = "Mật khẩu trong file không được bắt đầu bằng các ký tự '=', '+', '-', '@' hoặc ký tự điều khiển.";
                }
            }

            if (validationError is not null)
            {
                evaluated.Add(new EvaluatedImportRow(
                    new AdminUserImportPreviewRowDto
                    {
                        RowNumber = raw.RowNumber,
                        Email = normalizedEmail,
                        FullName = trimmedFullName,
                        Role = normalizedRole,
                        StudentCode = string.IsNullOrWhiteSpace(trimmedStudentCode) ? null : trimmedStudentCode,
                        Username = resolvedUsername ?? (string.IsNullOrWhiteSpace(trimmedUsername) ? null : trimmedUsername),
                        PasswordSource = "NONE",
                        IsValid = false,
                        Status = "INVALID",
                        ErrorMessage = validationError
                    },
                    null));
                continue;
            }

            string? duplicateReason = null;
            if (dbEmails.Contains(normalizedEmail))
            {
                duplicateReason = "Email đã tồn tại trong hệ thống.";
            }
            else if (fileEmails.Contains(normalizedEmail))
            {
                duplicateReason = "Trùng email với một dòng hợp lệ khác trong file.";
            }
            else if (resolvedStudentCode is not null && dbStudentCodes.Contains(resolvedStudentCode))
            {
                duplicateReason = "Mã sinh viên đã tồn tại trong hệ thống.";
            }
            else if (resolvedStudentCode is not null && fileStudentCodes.Contains(resolvedStudentCode))
            {
                duplicateReason = "Trùng mã sinh viên với một dòng hợp lệ khác trong file.";
            }
            else if (resolvedUsername is not null && dbUsernames.Contains(resolvedUsername))
            {
                duplicateReason = "Tên đăng nhập đã tồn tại trong hệ thống.";
            }
            else if (resolvedUsername is not null && fileUsernames.Contains(resolvedUsername))
            {
                duplicateReason = "Trùng tên đăng nhập với một dòng hợp lệ khác trong file.";
            }

            if (duplicateReason is not null)
            {
                evaluated.Add(new EvaluatedImportRow(
                    new AdminUserImportPreviewRowDto
                    {
                        RowNumber = raw.RowNumber,
                        Email = normalizedEmail,
                        FullName = trimmedFullName,
                        Role = normalizedRole,
                        StudentCode = resolvedStudentCode,
                        Username = resolvedUsername,
                        PasswordSource = "NONE",
                        IsValid = false,
                        Status = "DUPLICATE",
                        ErrorMessage = duplicateReason
                    },
                    null));
                continue;
            }

            fileEmails.Add(normalizedEmail);
            if (resolvedStudentCode is not null)
            {
                fileStudentCodes.Add(resolvedStudentCode);
            }

            if (resolvedUsername is not null)
            {
                fileUsernames.Add(resolvedUsername);
            }

            string passwordSource;
            string? effectivePassword = null;

            if (!string.IsNullOrEmpty(rawPassword))
            {
                passwordSource = "ROW";
                effectivePassword = rawPassword;
            }
            else if (hasDefaultPassword)
            {
                passwordSource = "DEFAULT";
                effectivePassword = defaultPassword;
            }
            else
            {
                passwordSource = "AUTO_GENERATED";
                if (generatePasswordsForValidRows)
                {
                    effectivePassword = GenerateStrongPassword();
                }
            }

            evaluated.Add(new EvaluatedImportRow(
                new AdminUserImportPreviewRowDto
                {
                    RowNumber = raw.RowNumber,
                    Email = normalizedEmail,
                    FullName = trimmedFullName,
                    Role = normalizedRole,
                    StudentCode = resolvedStudentCode,
                    Username = resolvedUsername,
                    PasswordSource = passwordSource,
                    IsValid = true,
                    Status = "VALID",
                    ErrorMessage = null
                },
                effectivePassword));
        }

        return evaluated;
    }

    public static string GenerateStrongPassword()
    {
        var chars = new char[AdminUserImportRules.GeneratedPasswordLength];
        // Position 0 is guaranteed to be an ASCII letter so Excel never interprets it as formula/number.
        chars[0] = LetterAlphabet[RandomNumberGenerator.GetInt32(LetterAlphabet.Length)];
        chars[1] = UpperAlphabet[RandomNumberGenerator.GetInt32(UpperAlphabet.Length)];
        chars[2] = LowerAlphabet[RandomNumberGenerator.GetInt32(LowerAlphabet.Length)];
        chars[3] = DigitAlphabet[RandomNumberGenerator.GetInt32(DigitAlphabet.Length)];
        chars[4] = SymbolAlphabet[RandomNumberGenerator.GetInt32(SymbolAlphabet.Length)];

        for (var i = 5; i < chars.Length; i++)
        {
            chars[i] = FullAlphabet[RandomNumberGenerator.GetInt32(FullAlphabet.Length)];
        }

        // Fisher-Yates shuffle on indices 1..len-1 keeping index 0 a letter.
        for (var i = chars.Length - 1; i >= 2; i--)
        {
            var j = RandomNumberGenerator.GetInt32(1, i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    public static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is not null)
        {
            var prop = inner.GetType().GetProperty("SqlState");
            if (prop is not null)
            {
                var sqlState = prop.GetValue(inner) as string;
                if (sqlState == "23505")
                {
                    return true;
                }
            }

            var message = inner.Message;
            if (message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("ix_users", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return ex.Message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }
}
