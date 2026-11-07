using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.AcademicCatalog.Commands.SyncSemesterCourseOfferings;

public sealed class SyncSemesterCourseOfferingsCommandHandler(
    IApplicationDbContext context,
    IValidator<SyncSemesterCourseOfferingsCommand> validator)
    : IRequestHandler<SyncSemesterCourseOfferingsCommand, Result<SyncCourseOfferingsDto>>
{
    public async Task<Result<SyncCourseOfferingsDto>> Handle(
        SyncSemesterCourseOfferingsCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SyncCourseOfferingsDto>.Failure(
                $"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        var semesterExists = await context.Semesters
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.SemesterId, cancellationToken);
        if (!semesterExists)
        {
            return Result<SyncCourseOfferingsDto>.Failure("NOT_FOUND: Không tìm thấy học kỳ.");
        }

        var selectedIds = request.SelectedCourseIds.ToHashSet();
        var validSelectedCount = await context.Courses
            .AsNoTracking()
            .CountAsync(c => selectedIds.Contains(c.Id) && c.IsActive, cancellationToken);

        if (validSelectedCount != selectedIds.Count)
        {
            return Result<SyncCourseOfferingsDto>.Failure(
                "VALIDATION: Danh sách chọn chứa môn học không tồn tại hoặc đã ngừng hoạt động.");
        }

        var now = DateTime.UtcNow;
        var offerings = await context.CourseOfferings
            .Where(o => o.SemesterId == request.SemesterId)
            .ToListAsync(cancellationToken);

        var createdCount = 0;
        var enabledCount = 0;
        var disabledCount = 0;

        foreach (var offering in offerings)
        {
            var shouldEnable = selectedIds.Contains(offering.CourseId);
            if (offering.IsEnabled == shouldEnable)
            {
                continue;
            }

            offering.IsEnabled = shouldEnable;
            offering.UpdatedAt = now;
            if (shouldEnable) enabledCount++; else disabledCount++;
        }

        var existingCourseIds = offerings.Select(o => o.CourseId).ToHashSet();
        foreach (var courseId in selectedIds.Where(id => !existingCourseIds.Contains(id)))
        {
            context.CourseOfferings.Add(new CourseOffering
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                SemesterId = request.SemesterId,
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            createdCount++;
        }

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "SEMESTER_COURSE_OFFERINGS_SYNCED",
            EntityName = "semesters",
            EntityId = request.SemesterId,
            NewValues = $"SelectedCourseIds={string.Join(',', selectedIds.Order())}",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        var catalog = await context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new CourseCatalogItemDto
            {
                CourseId = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                Credits = c.Credits,
                AcademicProgramId = c.AcademicProgramId,
                AcademicProgramCode = c.AcademicProgram == null ? null : c.AcademicProgram.Code,
                IsCourseActive = c.IsActive,
                CourseOfferingId = c.CourseOfferings
                    .Where(o => o.SemesterId == request.SemesterId)
                    .Select(o => (Guid?)o.Id)
                    .FirstOrDefault(),
                IsSelected = c.CourseOfferings
                    .Any(o => o.SemesterId == request.SemesterId && o.IsEnabled)
            })
            .ToListAsync(cancellationToken);

        return Result<SyncCourseOfferingsDto>.Success(new SyncCourseOfferingsDto
        {
            SemesterId = request.SemesterId,
            CreatedCount = createdCount,
            EnabledCount = enabledCount,
            DisabledCount = disabledCount,
            Courses = catalog
        });
    }
}
