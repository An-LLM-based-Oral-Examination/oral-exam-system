using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;

public class CreateClassCommandHandler(
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
            return Result<CreateClassResponseDto>.Failure(validation.Errors[0].ErrorMessage);
        }

        var normalizedCode = request.Code.Trim();

        var codeExists = await context.Classes
            .AnyAsync(c => c.Code.ToLower() == normalizedCode.ToLower(), cancellationToken);

        if (codeExists)
        {
            return Result<CreateClassResponseDto>.Failure("Mã lớp học đã tồn tại trong hệ thống.");
        }

        var course = await context.Courses
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course is null)
        {
            return Result<CreateClassResponseDto>.Failure("Không tìm thấy môn học.");
        }

        var semester = await context.Semesters
            .FirstOrDefaultAsync(s => s.Id == request.SemesterId, cancellationToken);

        if (semester is null)
        {
            return Result<CreateClassResponseDto>.Failure("Không tìm thấy học kỳ.");
        }

        var lecturer = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.LecturerId && u.IsActive, cancellationToken);

        if (lecturer is null)
        {
            return Result<CreateClassResponseDto>.Failure("Không tìm thấy giảng viên.");
        }

        if (!string.Equals(lecturer.Role, "lecturer", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(lecturer.Role, "department_head", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(lecturer.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateClassResponseDto>.Failure("FORBIDDEN: Người dùng được chỉ định không có vai trò Giảng viên.");
        }

        var (actorRole, actorUserId) = ParseActorRoleAndId(request.ActorRole);

        if (string.Equals(actorRole, "department_head", StringComparison.OrdinalIgnoreCase) &&
            course.DepartmentHeadUserId.HasValue)
        {
            var effectiveDeptHeadId = actorUserId ??
                                      (string.Equals(lecturer.Role, "department_head", StringComparison.OrdinalIgnoreCase)
                                          ? request.LecturerId
                                          : (Guid?)null);

            if (effectiveDeptHeadId.HasValue &&
                effectiveDeptHeadId.Value != Guid.Empty &&
                course.DepartmentHeadUserId.Value != effectiveDeptHeadId.Value)
            {
                return Result<CreateClassResponseDto>.Failure(
                    "FORBIDDEN: Trưởng bộ môn chỉ được phép tạo lớp học phần cho môn học do mình quản lý.");
            }
        }

        // Kiểm tra phân công giảng viên theo môn học và học kỳ (course_lecturers - R5) khi người gọi là giảng viên hoặc môn học đã có phân công
        var hasCourseAssignments = await context.CourseLecturers
            .AnyAsync(cl => cl.CourseId == request.CourseId &&
                            cl.IsActive &&
                            (cl.SemesterId == null || cl.SemesterId == request.SemesterId), cancellationToken);

        if (string.Equals(actorRole, "lecturer", StringComparison.OrdinalIgnoreCase) || hasCourseAssignments)
        {
            var isAssignedLecturer = await context.CourseLecturers
                .AnyAsync(cl => cl.CourseId == request.CourseId &&
                                cl.LecturerUserId == request.LecturerId &&
                                cl.IsActive &&
                                (cl.SemesterId == null || cl.SemesterId == request.SemesterId), cancellationToken);
            var isCourseDeptHead = course.DepartmentHeadUserId.HasValue && course.DepartmentHeadUserId.Value == request.LecturerId;

            if (!isAssignedLecturer && !isCourseDeptHead && !string.Equals(actorRole, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return Result<CreateClassResponseDto>.Failure(
                    "FORBIDDEN: Giảng viên chưa được Trưởng bộ môn phân công quản lý môn học này (course_lecturers).");
            }
        }

        string joinCode;
        if (!string.IsNullOrWhiteSpace(request.JoinCode))
        {
            joinCode = request.JoinCode.Trim().ToUpperInvariant();
            var joinCodeExists = await context.Classes
                .AnyAsync(c => c.JoinCode.ToUpper() == joinCode, cancellationToken);

            if (joinCodeExists)
            {
                return Result<CreateClassResponseDto>.Failure("Mã tham gia lớp (JoinCode) đã tồn tại. Vui lòng chọn mã khác.");
            }
        }
        else
        {
            joinCode = await GenerateUniqueJoinCodeAsync(cancellationToken);
        }

        string? passwordHash = null;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            passwordHash = passwordHasher.Hash(request.Password.Trim());
        }

        var newClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            JoinCode = joinCode,
            JoinPasswordHash = passwordHash,
            CourseId = request.CourseId,
            SemesterId = request.SemesterId,
            LecturerId = request.LecturerId,
            MaxStudents = request.MaxStudents,
            IsJoinEnabled = request.IsJoinEnabled,
            AllowSelfEnrollment = request.AllowSelfEnrollment,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.Add(newClass);
        await context.SaveChangesAsync(cancellationToken);

        var response = new CreateClassResponseDto(
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
            HasPassword: !string.IsNullOrWhiteSpace(passwordHash),
            newClass.IsJoinEnabled,
            newClass.AllowSelfEnrollment,
            newClass.CreatedAt);

        return Result<CreateClassResponseDto>.Success(response);
    }

    private async Task<string> GenerateUniqueJoinCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[8];
            for (var i = 0; i < chars.Length; i++)
            {
                var idx = RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length);
                chars[i] = JoinCodeAlphabet[idx];
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
