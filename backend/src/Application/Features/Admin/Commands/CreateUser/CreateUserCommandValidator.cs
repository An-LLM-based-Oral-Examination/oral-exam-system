using System;
using System.Linq;
using FluentValidation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Admin.Commands.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private static readonly string[] AllowedRoles = UserRole.All;

    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MinimumLength(2).WithMessage("Họ và tên phải có ít nhất 2 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có độ dài tối thiểu 8 ký tự.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Vai trò (Role) không được để trống.")
            .Must(role => !string.IsNullOrWhiteSpace(role) && AllowedRoles.Contains(role.Trim().ToLowerInvariant()))
            .WithMessage($"Vai trò không hợp lệ. Chỉ chấp nhận một trong 6 roles: {string.Join(", ", AllowedRoles)}.");

        RuleFor(x => x.StudentCode)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.Role) && string.Equals(x.Role.Trim(), UserRole.Student, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Mã sinh viên (StudentCode) là bắt buộc đối với vai trò student.");
    }
}
