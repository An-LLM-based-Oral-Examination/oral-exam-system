using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Auth.Commands.GoogleLogin;

public sealed partial class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, Result<GoogleLoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleIdTokenValidator _googleIdTokenValidator;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IValidator<GoogleLoginCommand> _validator;

    public GoogleLoginCommandHandler(
        IApplicationDbContext context,
        IGoogleIdTokenValidator googleIdTokenValidator,
        IAccessTokenIssuer accessTokenIssuer,
        IValidator<GoogleLoginCommand> validator)
    {
        _context = context;
        _googleIdTokenValidator = googleIdTokenValidator;
        _accessTokenIssuer = accessTokenIssuer;
        _validator = validator;
    }

    public async Task<Result<GoogleLoginResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra đầu vào
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<GoogleLoginResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        // 2. Xác minh chữ ký ID Token của Google
        GoogleUserIdentity identity;
        try
        {
            identity = await _googleIdTokenValidator.ValidateAsync(request.IdToken!, cancellationToken);
        }
        catch (GoogleIdTokenRejectedException)
        {
            return Result<GoogleLoginResponse>.Failure("ID Token của Google không hợp lệ.");
        }

        // 3. Tìm user trong bảng users theo email
        var email = identity.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                FullName = identity.FullName,
                StudentCode = TryExtractFptStudentCode(email),
                MustChangePassword = false,
                Role = UserRole.Student,
                IsActive = true
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else if (!user.IsActive)
        {
            return Result<GoogleLoginResponse>.Failure("Tài khoản đã bị khóa.");
        }
        else if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Đảm bảo sinh viên đăng nhập qua Google được tự động ghi danh vào các lớp học đang mở
        if (string.Equals(user.Role, UserRole.Student, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureStudentEnrolledInActiveClassesAsync(user.Id, cancellationToken);
        }

        // 4. Cấp access token JWT (15 phút) + refresh token (7 ngày)
        var accessToken = _accessTokenIssuer.Issue(user.Id, user.Email, user.FullName, user.Role);

        var rawRefreshToken = GenerateRawToken();
        var refreshTokenHash = HashToken(rawRefreshToken);

        var refreshTokenEntity = new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(AuthTokenDefaults.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new GoogleLoginResponse(
            accessToken.Token,
            rawRefreshToken,
            "Bearer",
            accessToken.ExpiresInSeconds,
            new GoogleLoginUserDto(user.Id, user.Email, user.FullName, user.StudentCode, user.Role));

        return Result<GoogleLoginResponse>.Success(response);
    }

    private async Task EnsureStudentEnrolledInActiveClassesAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var activeClassIds = await _context.Classes
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (activeClassIds.Count == 0)
        {
            return;
        }

        var enrolledClassIds = (await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var added = false;
        foreach (var classId in activeClassIds)
        {
            if (!enrolledClassIds.Contains(classId))
            {
                _context.ClassEnrollments.Add(new ClassEnrollment
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    StudentId = studentId,
                    Status = "enrolled",
                    EnrolledAt = DateTime.UtcNow
                });
                added = true;
            }
        }

        if (added)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static string? TryExtractFptStudentCode(string email)
    {
        var match = FptStudentEmailRegex().Match(email);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex(@"([a-zA-Z]{2}\d{6})@fpt\.edu\.vn$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FptStudentEmailRegex();

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public static string GenerateRawToken()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
