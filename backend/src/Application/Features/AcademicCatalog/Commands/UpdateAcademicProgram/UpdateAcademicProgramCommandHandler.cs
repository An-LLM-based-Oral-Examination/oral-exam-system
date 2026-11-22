using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.UpdateAcademicProgram;

public sealed class UpdateAcademicProgramCommandHandler(
    IApplicationDbContext context,
    IValidator<UpdateAcademicProgramCommand> validator)
    : IRequestHandler<UpdateAcademicProgramCommand, Result<AcademicProgramDto>>
{
    public UpdateAcademicProgramCommandHandler(IApplicationDbContext context)
        : this(context, new UpdateAcademicProgramCommandValidator())
    {
    }

    public async Task<Result<AcademicProgramDto>> Handle(
        UpdateAcademicProgramCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AcademicProgramDto>.Failure(
                $"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        var program = await context.AcademicPrograms
            .Include(p => p.Courses)
            .FirstOrDefaultAsync(p => p.Id == request.AcademicProgramId, cancellationToken);

        if (program is null)
        {
            return Result<AcademicProgramDto>.Failure("NOT_FOUND: Không tìm thấy ngành đào tạo.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var trimmedName = request.Name.Trim();

        var duplicateCode = await context.AcademicPrograms
            .AsNoTracking()
            .AnyAsync(p => p.Id != request.AcademicProgramId && p.Code.ToUpper() == normalizedCode, cancellationToken);

        if (duplicateCode)
        {
            return Result<AcademicProgramDto>.Failure(
                $"CONFLICT: Mã chương trình đào tạo '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var oldSummary =
            $"code={program.Code};name={program.Name};departmentHeadUserId={program.DepartmentHeadUserId?.ToString() ?? "null"};isActive={program.IsActive}";

        string? departmentHeadName = null;
        if (request.DepartmentHeadUserId.HasValue)
        {
            if (request.DepartmentHeadUserId.Value == Guid.Empty)
            {
                program.DepartmentHeadUserId = null;
            }
            else
            {
                var departmentHead = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.DepartmentHeadUserId.Value, cancellationToken);

                if (departmentHead is null)
                {
                    return Result<AcademicProgramDto>.Failure("NOT_FOUND: Không tìm thấy người dùng Trưởng bộ môn.");
                }

                if (!departmentHead.IsActive ||
                    !string.Equals(departmentHead.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<AcademicProgramDto>.Failure(
                        "VALIDATION: Người dùng phải là Trưởng bộ môn đang hoạt động.");
                }

                var headChanged = program.DepartmentHeadUserId != departmentHead.Id;
                program.DepartmentHeadUserId = departmentHead.Id;
                departmentHeadName = departmentHead.FullName;

                if (headChanged)
                {
                    foreach (var course in program.Courses)
                    {
                        course.DepartmentHeadUserId = departmentHead.Id;
                    }
                }
            }
        }
        else if (program.DepartmentHeadUserId.HasValue)
        {
            departmentHeadName = await context.Users
                .AsNoTracking()
                .Where(u => u.Id == program.DepartmentHeadUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        program.Code = normalizedCode;
        program.Name = trimmedName;
        program.IsActive = request.IsActive;
        program.UpdatedAt = now;

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "ACADEMIC_PROGRAM_UPDATED",
            EntityName = "academic_programs",
            EntityId = program.Id,
            OldValues = oldSummary,
            NewValues = $"code={program.Code};name={program.Name};departmentHeadUserId={program.DepartmentHeadUserId?.ToString() ?? "null"};isActive={program.IsActive}",
            CreatedAt = now
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (AcademicProgramConflictDetector.IsDuplicateCodeViolation(ex))
        {
            return Result<AcademicProgramDto>.Failure(
                $"CONFLICT: Mã chương trình đào tạo '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        return Result<AcademicProgramDto>.Success(new AcademicProgramDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            DepartmentHeadUserId = program.DepartmentHeadUserId,
            DepartmentHeadName = departmentHeadName,
            IsActive = program.IsActive,
            CoursesCount = program.Courses.Count
        });
    }
}
