using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;

public sealed class ResetClassPasswordCommandHandler
    : IRequestHandler<ResetClassPasswordCommand, Result<ResetClassPasswordResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher? _passwordHasher;
    private readonly IValidator<ResetClassPasswordCommand> _validator;

    public ResetClassPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IValidator<ResetClassPasswordCommand> validator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _validator = validator;
    }

    public ResetClassPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher)
        : this(context, passwordHasher, new ResetClassPasswordCommandValidator())
    {
    }

    public ResetClassPasswordCommandHandler(IApplicationDbContext context)
        : this(context, null!, new ResetClassPasswordCommandValidator())
    {
    }

    public async Task<Result<ResetClassPasswordResponseDto>> Handle(
        ResetClassPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ResetClassPasswordResponseDto>.Failure($"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        var targetClass = await _context.Classes
            .Include(c => c.Course)
                .ThenInclude(c => c.AcademicProgram)
            .Include(c => c.CourseOffering)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (targetClass is null)
        {
            return Result<ResetClassPasswordResponseDto>.Failure("NOT_FOUND: Không tìm thấy lớp học phần.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is not null && !actor.IsActive)
        {
            return Result<ResetClassPasswordResponseDto>.Failure("FORBIDDEN: Tài khoản người thực hiện đang bị khóa hoặc vô hiệu hóa.");
        }

        var effectiveRole = !string.IsNullOrWhiteSpace(request.ActorRole)
            ? request.ActorRole
            : actor?.Role;

        var isAdmin = string.Equals(effectiveRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase);
        var isClassLecturer = targetClass.LecturerId.HasValue && targetClass.LecturerId.Value == request.ActorUserId;
        var departmentHeadId = targetClass.Course.AcademicProgram?.DepartmentHeadUserId
                               ?? targetClass.Course.DepartmentHeadUserId;
        var isCourseDeptHead = departmentHeadId == request.ActorUserId;

        if (!isAdmin && !isClassLecturer && !isCourseDeptHead)
        {
            return Result<ResetClassPasswordResponseDto>.Failure(
                "FORBIDDEN: Chỉ Giảng viên phụ trách lớp học, Trưởng bộ môn của môn học hoặc Admin mới có quyền đặt lại mật khẩu lớp học phần.");
        }

        if (!string.IsNullOrWhiteSpace(request.NewJoinCode))
        {
            var normalizedJoinCode = request.NewJoinCode.Trim().ToUpperInvariant();
            var joinCodeExists = await _context.Classes
                .AnyAsync(c => c.Id != targetClass.Id && c.JoinCode.ToUpper() == normalizedJoinCode, cancellationToken);

            if (joinCodeExists)
            {
                return Result<ResetClassPasswordResponseDto>.Failure(
                    "CONFLICT: Mã tham gia lớp (JoinCode) đã tồn tại. Vui lòng chọn mã khác.");
            }

            targetClass.JoinCode = normalizedJoinCode;
        }

        if (request.NewPassword is not null)
        {
            var trimmedPassword = request.NewPassword.Trim();
            if (targetClass.CourseOfferingId.HasValue &&
                targetClass.AllowSelfEnrollment &&
                string.IsNullOrWhiteSpace(trimmedPassword))
            {
                return Result<ResetClassPasswordResponseDto>.Failure(
                    "VALIDATION: Lớp của course offering phải duy trì mật khẩu tham gia cố định.");
            }

            targetClass.JoinPasswordHash = string.IsNullOrWhiteSpace(trimmedPassword)
                ? null
                : HashPassword(trimmedPassword);
        }

        if (request.IsJoinEnabled.HasValue)
        {
            targetClass.IsJoinEnabled = request.IsJoinEnabled.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var response = new ResetClassPasswordResponseDto(
            targetClass.Id,
            targetClass.Code,
            targetClass.Name,
            targetClass.JoinCode,
            HasPassword: !string.IsNullOrWhiteSpace(targetClass.JoinPasswordHash),
            targetClass.IsJoinEnabled,
            DateTime.UtcNow);

        return Result<ResetClassPasswordResponseDto>.Success(response);
    }

    private string HashPassword(string password)
    {
        if (_passwordHasher is not null)
        {
            return _passwordHasher.Hash(password);
        }

        const int iterations = 210_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }
}
