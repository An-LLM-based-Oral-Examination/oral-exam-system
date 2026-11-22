using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.CreateAcademicProgram;

public sealed class CreateAcademicProgramCommandHandler(
    IApplicationDbContext context,
    IValidator<CreateAcademicProgramCommand> validator)
    : IRequestHandler<CreateAcademicProgramCommand, Result<AcademicProgramDto>>
{
    public CreateAcademicProgramCommandHandler(IApplicationDbContext context)
        : this(context, new CreateAcademicProgramCommandValidator())
    {
    }

    public async Task<Result<AcademicProgramDto>> Handle(
        CreateAcademicProgramCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AcademicProgramDto>.Failure(
                $"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var trimmedName = request.Name.Trim();

        var duplicateCode = await context.AcademicPrograms
            .AsNoTracking()
            .AnyAsync(p => p.Code.ToUpper() == normalizedCode, cancellationToken);

        if (duplicateCode)
        {
            return Result<AcademicProgramDto>.Failure(
                $"CONFLICT: Mã chương trình đào tạo '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        Guid? departmentHeadId = null;
        string? departmentHeadName = null;

        if (request.DepartmentHeadUserId is Guid requestedHeadId && requestedHeadId != Guid.Empty)
        {
            var departmentHead = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == requestedHeadId, cancellationToken);

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

            departmentHeadId = departmentHead.Id;
            departmentHeadName = departmentHead.FullName;
        }

        var now = DateTime.UtcNow;
        var program = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = trimmedName,
            DepartmentHeadUserId = departmentHeadId,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.AcademicPrograms.Add(program);
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "ACADEMIC_PROGRAM_CREATED",
            EntityName = "academic_programs",
            EntityId = program.Id,
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
            CoursesCount = 0
        });
    }
}
