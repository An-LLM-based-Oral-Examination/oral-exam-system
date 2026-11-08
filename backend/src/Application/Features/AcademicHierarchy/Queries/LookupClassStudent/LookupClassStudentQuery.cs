using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.LookupClassStudent;

public sealed record LookupClassStudentQuery(string? Query) : IRequest<Result<ClassStudentLookupDto>>;

public sealed class LookupClassStudentQueryHandler(IApplicationDbContext context)
    : IRequestHandler<LookupClassStudentQuery, Result<ClassStudentLookupDto>>
{
    private static readonly Regex StudentCodePattern = new(
        "^[A-Z]{2}[0-9]{6}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<Result<ClassStudentLookupDto>> Handle(
        LookupClassStudentQuery request,
        CancellationToken cancellationToken)
    {
        var term = (request.Query ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return Result<ClassStudentLookupDto>.Failure("VALIDATION: Nhập đủ mã sinh viên hoặc email.");
        }

        UserMatch? match;
        if (IsEmail(term))
        {
            var email = term.ToLowerInvariant();
            match = await context.Users
                .AsNoTracking()
                .Where(user => user.Email == email || user.Email == term)
                .Select(user => new UserMatch(user.Id, user.FullName, user.Email, user.StudentCode, user.Role, user.IsActive))
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            var code = term.ToUpperInvariant();
            if (!StudentCodePattern.IsMatch(code))
            {
                return Result<ClassStudentLookupDto>.Failure("VALIDATION: Nhập đủ mã sinh viên hoặc email.");
            }

            match = await context.Users
                .AsNoTracking()
                .Where(user => user.StudentCode == code || user.StudentCode == term)
                .Select(user => new UserMatch(user.Id, user.FullName, user.Email, user.StudentCode, user.Role, user.IsActive))
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (match is null || !string.Equals(match.Role, UserRole.Student, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ClassStudentLookupDto>.Failure("NOT_FOUND: Không tìm thấy sinh viên với mã hoặc email này.");
        }

        if (!match.IsActive)
        {
            return Result<ClassStudentLookupDto>.Failure("VALIDATION: Tài khoản sinh viên đang bị khóa.");
        }

        return Result<ClassStudentLookupDto>.Success(
            new ClassStudentLookupDto(match.Id, match.FullName, match.Email, match.StudentCode));
    }

    private static bool IsEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 3 && value.IndexOf('.', at + 1) > at + 1;
    }

    private sealed record UserMatch(
        Guid Id,
        string FullName,
        string Email,
        string? StudentCode,
        string Role,
        bool IsActive);
}
