using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Semesters.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Semesters.Commands.UpdateSemester;

public sealed record UpdateSemesterCommand(
    Guid ActorUserId,
    Guid SemesterId,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive) : IRequest<Result<SemesterItemDto>>;

public sealed class UpdateSemesterCommandHandler
    : IRequestHandler<UpdateSemesterCommand, Result<SemesterItemDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateSemesterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SemesterItemDto>> Handle(
        UpdateSemesterCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<SemesterItemDto>.Failure(
                "VALIDATION: Mã học kỳ (Code) và tên học kỳ (Name) không được để trống.");
        }

        if (request.EndDate <= request.StartDate)
        {
            return Result<SemesterItemDto>.Failure(
                "VALIDATION: Ngày kết thúc học kỳ (EndDate) bắt buộc phải sau ngày bắt đầu (StartDate).");
        }

        var semester = await _context.Semesters
            .Include(s => s.Courses)
            .Include(s => s.Classes)
            .FirstOrDefaultAsync(s => s.Id == request.SemesterId, cancellationToken);

        if (semester is null)
        {
            return Result<SemesterItemDto>.Failure(
                "NOT_FOUND: Không tìm thấy học kỳ trong hệ thống.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var duplicateCode = await _context.Semesters
            .AnyAsync(s => s.Id != semester.Id && s.Code.ToUpper() == normalizedCode, cancellationToken);

        if (duplicateCode)
        {
            return Result<SemesterItemDto>.Failure(
                $"CONFLICT: Mã học kỳ '{normalizedCode}' đã được sử dụng bởi học kỳ khác.");
        }

        var oldValues = $"{semester.Code} | {semester.Name} | active={semester.IsActive}";
        semester.Code = normalizedCode;
        semester.Name = request.Name.Trim();
        semester.StartDate = request.StartDate;
        semester.EndDate = request.EndDate;
        semester.IsActive = request.IsActive;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "SEMESTER_UPDATED",
            EntityName = "semesters",
            EntityId = semester.Id,
            OldValues = oldValues,
            NewValues = $"{semester.Code} | {semester.Name} | active={semester.IsActive}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<SemesterItemDto>.Success(new SemesterItemDto
        {
            Id = semester.Id,
            Code = semester.Code,
            Name = semester.Name,
            StartDate = semester.StartDate,
            EndDate = semester.EndDate,
            IsActive = semester.IsActive,
            CoursesCount = semester.Courses.Count,
            ClassesCount = semester.Classes.Count,
            CreatedAt = semester.CreatedAt
        });
    }
}
