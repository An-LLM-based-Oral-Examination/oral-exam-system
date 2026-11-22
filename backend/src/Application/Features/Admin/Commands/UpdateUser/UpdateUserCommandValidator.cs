using System;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using FluentValidation;

namespace OralExamination.Application.Features.Admin.Commands.UpdateUser;

public sealed partial class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    [GeneratedRegex("^[a-zA-Z0-9._-]+$")]
    private static partial Regex UsernameRegex();

    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Mã người dùng không được để trống.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .Must(IsCanonicalEmail).WithMessage("Định dạng email không hợp lệ.")
            .Must(email => string.IsNullOrWhiteSpace(email) || email.Trim().Length <= 255)
            .WithMessage("Email không được vượt quá 255 ký tự.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length >= 2)
            .WithMessage("Họ và tên phải có ít nhất 2 ký tự.")
            .Must(name => string.IsNullOrWhiteSpace(name) || name.Trim().Length <= 150)
            .WithMessage("Họ và tên không được vượt quá 150 ký tự.");

        RuleFor(x => x.StudentCode)
            .Must(code => string.IsNullOrWhiteSpace(code) || code.Trim().Length <= 20)
            .WithMessage("Mã sinh viên không được vượt quá 20 ký tự.");

        When(x => !string.IsNullOrWhiteSpace(x.Username), () =>
        {
            RuleFor(x => x.Username)
                .Must(username => username!.Trim().Length is >= 3 and <= 32)
                .WithMessage("Tên đăng nhập phải có độ dài từ 3 đến 32 ký tự.")
                .Must(username => UsernameRegex().IsMatch(username!.Trim()))
                .WithMessage("Tên đăng nhập chỉ được chứa chữ cái, chữ số, dấu chấm (.), gạch dưới (_) và gạch ngang (-).");
        });
    }

    private static bool IsCanonicalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return true;
        }

        var trimmed = email.Trim();
        return !trimmed.Any(char.IsWhiteSpace) &&
               MailAddress.TryCreate(trimmed, out var parsed) &&
               string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase);
    }
}
