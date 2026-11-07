using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;

public sealed class CreateClassCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IValidator<CreateClassCommand> validator)
    : IRequestHandler<CreateClassCommand, Result<CreateClassResponseDto>>
{
    private const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<Result<CreateClassResponseDto>> Handle(
        CreateClassCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CreateClassResponseDto>.Failure(
                $"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var codeExists = await context.Classes
            .AnyAsync(c => c.Code.ToUpper() == normalizedCode, cancellationToken);
        if (codeExists)
        {
            return Result<CreateClassResponseDto>.Failure(
                "CONFLICT: Mã lớp học đã tồn tại trong hệ thống.");
        }

        CourseOffering? courseOffering = null;
        Course course;
        Semester semester;

        if (request.CourseOfferingId is { } offeringId && offeringId != Guid.Empty)
        {
            courseOffering = await context.CourseOfferings
                .Include(o => o.Course)
                    .ThenInclude(c => c.AcademicProgram)
                .Include(o => o.Semester)
                .FirstOrDefaultAsync(o => o.Id == offeringId, cancellationToken);

            if (courseOffering is null)
            {
                return Result<CreateClassResponseDto>.Failure(
                    "NOT_FOUND: Không tìm thấy course offering.");
            }

            if (!courseOffering.IsEnabled || !courseOffering.Course.IsActive)
            {
                return Result<CreateClassResponseDto>.Failure(
                    "VALIDATION: Course offering đang bị vô hiệu hóa.");
            }

            if ((request.CourseId != Guid.Empty && request.CourseId != courseOffering.CourseId) ||
                (request.SemesterId != Guid.Empty && request.SemesterId != courseOffering.SemesterId))
            {
                return Result<CreateClassResponseDto>.Failure(
                    "VALIDATION: CourseId hoặc SemesterId không khớp với CourseOfferingId.");
            }

            course = courseOffering.Course;
            semester = courseOffering.Semester;
        }
        else
        {
            var legacyCourse = await context.Courses
                .Include(c => c.AcademicProgram)
                .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
            if (legacyCourse is null)
            {
                return Result<CreateClassResponseDto>.Failure("NOT_FOUND: Không tìm thấy môn học.");
            }
            course = legacyCourse;

            var legacySemester = await context.Semesters
                .FirstOrDefaultAsync(s => s.Id == request.SemesterId, cancellationToken);
            if (legacySemester is null)
            {
                return Result<CreateClassResponseDto>.Failure("NOT_FOUND: Không tìm thấy học kỳ.");
            }
            semester = legacySemester;
        }

        var lecturer = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.LecturerId, cancellationToken);
        if (lecturer is null)
        {
            return Result<CreateClassResponseDto>.Failure("NOT_FOUND: Không tìm thấy giảng viên.");
        }

        if (!lecturer.IsActive)
        {
            return Result<CreateClassResponseDto>.Failure(
                "FORBIDDEN: Tài khoản giảng viên đang bị vô hiệu hóa.");
        }

        if (courseOffering is not null &&
            !string.Equals(lecturer.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateClassResponseDto>.Failure(
                "FORBIDDEN: Lớp của course offering phải do một giảng viên được phân công sở hữu.");
        }

        if (courseOffering is null &&
            !string.Equals(lecturer.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(lecturer.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(lecturer.Role, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateClassResponseDto>.Failure(
                "FORBIDDEN: Người dùng được chỉ định không có vai trò phù hợp để sở hữu lớp.");
        }

        var (actorRole, actorIdFromRole) = ParseActorRoleAndId(request.ActorRole);
        var actorUserId = request.ActorUserId is { } explicitActorId && explicitActorId != Guid.Empty
            ? explicitActorId
            : actorIdFromRole ?? request.LecturerId;

        if (string.Equals(actorRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) &&
            actorUserId != request.LecturerId)
        {
            return Result<CreateClassResponseDto>.Failure(
                "FORBIDDEN: Giảng viên không được tạo lớp mạo danh giảng viên khác.");
        }

        var owningDepartmentHeadId = course.AcademicProgram?.DepartmentHeadUserId ?? course.DepartmentHeadUserId;
        if (string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            owningDepartmentHeadId != actorUserId)
        {
            return Result<CreateClassResponseDto>.Failure(
                "FORBIDDEN: Trưởng bộ môn chỉ được thao tác trên course offering thuộc ngành mình quản lý.");
        }

        if (courseOffering is not null)
        {
            var isAssignedLecturer = await context.CourseLecturers
                .AnyAsync(a => a.CourseOfferingId == courseOffering.Id &&
                               a.LecturerUserId == request.LecturerId &&
                               a.IsActive, cancellationToken);
            if (!isAssignedLecturer)
            {
                return Result<CreateClassResponseDto>.Failure(
                    "FORBIDDEN: Giảng viên chưa được phân công active vào course offering này.");
            }
        }
        else
        {
            var hasAssignments = await context.CourseLecturers
                .AnyAsync(a => a.CourseId == course.Id &&
                               a.IsActive &&
                               (a.SemesterId == null || a.SemesterId == semester.Id), cancellationToken);

            if (string.Equals(actorRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) || hasAssignments)
            {
                var isAssignedLecturer = await context.CourseLecturers
                    .AnyAsync(a => a.CourseId == course.Id &&
                                   a.LecturerUserId == request.LecturerId &&
                                   a.IsActive &&
                                   (a.SemesterId == null || a.SemesterId == semester.Id), cancellationToken);
                var isCourseDepartmentHead = owningDepartmentHeadId == request.LecturerId;

                if (!isAssignedLecturer && !isCourseDepartmentHead &&
                    !string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<CreateClassResponseDto>.Failure(
                        "FORBIDDEN: Giảng viên chưa được Trưởng bộ môn phân công quản lý môn học này.");
                }
            }
        }

        var joinCode = await ResolveJoinCodeAsync(request.JoinCode, cancellationToken);
        if (joinCode is null)
        {
            return Result<CreateClassResponseDto>.Failure(
                "CONFLICT: Mã tham gia lớp đã tồn tại. Vui lòng chọn mã khác.");
        }

        var plainPassword = string.IsNullOrWhiteSpace(request.Password)
            ? null
            : request.Password.Trim();
        var passwordHash = plainPassword is null
            ? null
            : passwordHasher.Hash(plainPassword);

        var now = DateTime.UtcNow;
        var newClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            JoinCode = joinCode,
            JoinPasswordHash = passwordHash,
            JoinPasswordPlain = plainPassword,
            CourseId = course.Id,
            SemesterId = semester.Id,
            CourseOfferingId = courseOffering?.Id,
            LecturerId = lecturer.Id,
            MaxStudents = request.MaxStudents,
            IsJoinEnabled = request.IsJoinEnabled,
            AllowSelfEnrollment = request.AllowSelfEnrollment,
            IsActive = true,
            CreatedAt = now
        };

        context.Classes.Add(newClass);
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actorUserId,
            Action = "CLASS_CREATED",
            EntityName = "classes",
            EntityId = newClass.Id,
            NewValues = $"CourseOfferingId={courseOffering?.Id};Code={newClass.Code}",
            CreatedAt = now
        });
        await context.SaveChangesAsync(cancellationToken);

        return Result<CreateClassResponseDto>.Success(new CreateClassResponseDto(
            newClass.Id,
            newClass.Code,
            newClass.Name,
            newClass.JoinCode,
            course.Id,
            course.Code,
            course.Name,
            semester.Id,
            semester.Code,
            lecturer.Id,
            lecturer.FullName,
            newClass.MaxStudents,
            HasPassword: passwordHash is not null,
            newClass.IsJoinEnabled,
            newClass.AllowSelfEnrollment,
            newClass.CreatedAt,
            newClass.CourseOfferingId));
    }

    private async Task<string?> ResolveJoinCodeAsync(
        string? requestedJoinCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedJoinCode))
        {
            var normalized = requestedJoinCode.Trim().ToUpperInvariant();
            var exists = await context.Classes
                .AnyAsync(c => c.JoinCode.ToUpper() == normalized, cancellationToken);
            return exists ? null : normalized;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[8];
            for (var index = 0; index < chars.Length; index++)
            {
                chars[index] = JoinCodeAlphabet[RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length)];
            }

            var candidate = new string(chars);
            var exists = await context.Classes
                .AnyAsync(c => c.JoinCode.ToUpper() == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    private static (string? Role, Guid? UserId) ParseActorRoleAndId(string? rawActorRole)
    {
        if (string.IsNullOrWhiteSpace(rawActorRole))
        {
            return (null, null);
        }

        var separatorIndex = rawActorRole.IndexOf(':');
        if (separatorIndex > 0)
        {
            var rolePart = rawActorRole[..separatorIndex].Trim();
            var idPart = rawActorRole[(separatorIndex + 1)..].Trim();
            if (Guid.TryParse(idPart, out var parsedId) && parsedId != Guid.Empty)
            {
                return (rolePart, parsedId);
            }
        }

        return (rawActorRole.Trim(), null);
    }
}
