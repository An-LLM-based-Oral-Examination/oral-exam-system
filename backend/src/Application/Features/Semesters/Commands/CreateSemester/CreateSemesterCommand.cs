using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Semesters.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Semesters.Commands.CreateSemester;

public sealed record CreateSemesterCommand(
    Guid ActorUserId,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive = true) : IRequest<Result<SemesterItemDto>>;

public sealed class CreateSemesterCommandHandler
    : IRequestHandler<CreateSemesterCommand, Result<SemesterItemDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateSemesterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SemesterItemDto>> Handle(
        CreateSemesterCommand request,
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

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.Semesters
            .AnyAsync(s => s.Code.ToUpper() == normalizedCode, cancellationToken);

        if (exists)
        {
            return Result<SemesterItemDto>.Failure(
                $"CONFLICT: Học kỳ có mã '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var now = DateTime.UtcNow;
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            CreatedAt = now
        };

        _context.Semesters.Add(semester);

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "SEMESTER_CREATED",
            EntityName = "semesters",
            EntityId = semester.Id,
            NewValues = $"{semester.Code} | {semester.Name} ({semester.StartDate:yyyy-MM-dd} -> {semester.EndDate:yyyy-MM-dd})",
            CreatedAt = now
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
            CoursesCount = 0,
            ClassesCount = 0,
            CreatedAt = semester.CreatedAt
        });
    }
}
