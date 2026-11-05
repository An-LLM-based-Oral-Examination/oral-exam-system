using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.AssignProgramDepartmentHead;

public sealed class AssignProgramDepartmentHeadCommandHandler(
    IApplicationDbContext context,
    IValidator<AssignProgramDepartmentHeadCommand> validator)
    : IRequestHandler<AssignProgramDepartmentHeadCommand, Result<AcademicProgramDto>>
{
    public async Task<Result<AcademicProgramDto>> Handle(
        AssignProgramDepartmentHeadCommand request,
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

        var departmentHead = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.DepartmentHeadUserId, cancellationToken);
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

        var now = DateTime.UtcNow;
        var previousHeadId = program.DepartmentHeadUserId;
        program.DepartmentHeadUserId = departmentHead.Id;
        program.UpdatedAt = now;

        // Compatibility bridge: existing lecturer-assignment authorization still reads Course.DepartmentHeadUserId.
        foreach (var course in program.Courses)
        {
            course.DepartmentHeadUserId = departmentHead.Id;
        }

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "ACADEMIC_PROGRAM_DEPARTMENT_HEAD_ASSIGNED",
            EntityName = "academic_programs",
            EntityId = program.Id,
            OldValues = previousHeadId?.ToString(),
            NewValues = departmentHead.Id.ToString(),
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result<AcademicProgramDto>.Success(new AcademicProgramDto
        {
            Id = program.Id,
            Code = program.Code,
            Name = program.Name,
            DepartmentHeadUserId = departmentHead.Id,
            DepartmentHeadName = departmentHead.FullName,
            IsActive = program.IsActive,
            CoursesCount = program.Courses.Count
        });
    }
}
