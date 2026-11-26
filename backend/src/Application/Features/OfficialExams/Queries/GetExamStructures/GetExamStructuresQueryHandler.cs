using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures;

/// <summary>
/// Handler truy vấn danh sách cấu trúc đề thi (ExamStructure).
/// </summary>
public sealed class GetExamStructuresQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetExamStructuresQuery, Result<List<ExamStructureDto>>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<List<ExamStructureDto>>> Handle(
        GetExamStructuresQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ExamStructures
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.Course != null && e.Course.IsActive)
            .AsQueryable();

        if (request.ActorUserId.HasValue)
        {
            if (request.ActorUserId.Value == Guid.Empty)
            {
                return Result<List<ExamStructureDto>>.Failure(
                    "FORBIDDEN: Không xác định được danh tính người dùng.");
            }

            var actor = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == request.ActorUserId.Value && u.IsActive)
                .Select(u => new { u.Id, u.Role })
                .FirstOrDefaultAsync(cancellationToken);

            if (actor is null)
            {
                return Result<List<ExamStructureDto>>.Failure(
                    "FORBIDDEN: Tài khoản không tồn tại hoặc đã bị vô hiệu hóa.");
            }

            if (!string.IsNullOrWhiteSpace(request.ActorRole)
                && !string.Equals(actor.Role, request.ActorRole.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Result<List<ExamStructureDto>>.Failure(
                    "FORBIDDEN: Vai trò yêu cầu không khớp với tài khoản hiện tại.");
            }

            if (string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
            {
                var activeCourses = await _context.Courses
                    .AsNoTracking()
                    .Include(c => c.AcademicProgram)
                    .Where(c => c.IsActive)
                    .ToListAsync(cancellationToken);

                var ownedCourseIds = activeCourses
                    .Where(c => AcademicCourseOwnership.IsOwnedBy(c, actor.Id))
                    .Select(c => c.Id)
                    .ToList();

                if (ownedCourseIds.Count == 0)
                {
                    return Result<List<ExamStructureDto>>.Success([]);
                }

                query = query.Where(e => ownedCourseIds.Contains(e.CourseId));
            }
            else if (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
            {
                var assignedCourseIds = await _context.CourseLecturers
                    .AsNoTracking()
                    .Where(cl =>
                        cl.LecturerUserId == actor.Id
                        && cl.IsActive
                        && cl.Course != null
                        && cl.Course.IsActive
                        && (cl.CourseOffering == null || cl.CourseOffering.IsEnabled))
                    .Select(cl => cl.CourseId)
                    .ToListAsync(cancellationToken);

                var teachingCourseIds = await _context.Classes
                    .AsNoTracking()
                    .Where(c =>
                        c.LecturerId == actor.Id
                        && c.IsActive
                        && c.Course != null
                        && c.Course.IsActive
                        && (c.CourseOffering == null || c.CourseOffering.IsEnabled))
                    .Select(c => c.CourseId)
                    .ToListAsync(cancellationToken);

                var allowedCourseIds = assignedCourseIds.Union(teachingCourseIds).ToList();
                if (allowedCourseIds.Count == 0)
                {
                    return Result<List<ExamStructureDto>>.Success([]);
                }

                query = query.Where(e => allowedCourseIds.Contains(e.CourseId));
            }
            else if (!string.Equals(actor.Role, UserRole.Admin, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(actor.Role, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase))
            {
                return Result<List<ExamStructureDto>>.Failure(
                    "FORBIDDEN: Vai trò hiện tại không có quyền xem cấu trúc đề thi.");
            }
        }

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            query = query.Where(e => e.CourseId == request.CourseId.Value);
        }

        if (request.StructureId.HasValue && request.StructureId.Value != Guid.Empty)
        {
            query = query.Where(e => e.Id == request.StructureId.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(e => e.IsActive == request.IsActive.Value);
        }

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var structureIds = items.Select(x => x.Id).ToList();
        var scopes = await _context.ExamStructureClos.AsNoTracking().Where(x => structureIds.Contains(x.StructureId)).ToListAsync(cancellationToken);
        var dtos = items.Select(e => new ExamStructureDto(
            e.Id,
            e.CourseId,
            e.Course?.Code ?? string.Empty,
            e.CreatedBy,
            e.Name,
            e.TotalQuestions,
            e.DurationMinutes,
            e.BloomDistribution,
            e.IsActive,
            e.CreatedAt,
            e.RequiredSetCount,
            string.IsNullOrWhiteSpace(e.SlotDefinitions) ? "[]" : e.SlotDefinitions) {
                EasyCount = e.EasyCount, MediumCount = e.MediumCount, HardCount = e.HardCount,
                CloIds = scopes.Where(x => x.StructureId == e.Id).Select(x => x.CloId).ToList() }).ToList();

        return Result<List<ExamStructureDto>>.Success(dtos);
    }
}
