using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftPoolStats;

public sealed class GetInClassShiftPoolStatsQueryHandler(
    IApplicationDbContext context)
    : IRequestHandler<GetInClassShiftPoolStatsQuery, Result<InClassShiftPoolStatsResponse>>
{
    public async Task<Result<InClassShiftPoolStatsResponse>> Handle(
        GetInClassShiftPoolStatsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ClassId == Guid.Empty)
        {
            return Result<InClassShiftPoolStatsResponse>.Failure("ClassId không được để trống.");
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role != UserRole.Lecturer)
        {
            return Result<InClassShiftPoolStatsResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var classEntity = await context.Classes
            .AsNoTracking()
            .Include(c => c.Course)
            .Include(c => c.CourseOffering)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (!InClassShiftAcademicContext.IsOperational(classEntity))
        {
            return Result<InClassShiftPoolStatsResponse>.Failure(InClassShiftErrors.ClassNotFound);
        }

        if (classEntity.LecturerId != actor.Id)
        {
            return Result<InClassShiftPoolStatsResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var activeClos = await context.CourseClos
            .AsNoTracking()
            .Where(c => c.CourseId == classEntity.CourseId && c.IsActive)
            .OrderBy(c => c.OrderIndex)
            .ThenBy(c => c.CloCode)
            .Select(c => new
            {
                c.Id,
                c.CloCode,
                c.Description,
                c.OrderIndex
            })
            .ToListAsync(cancellationToken);

        var approvedQuestions = await context.PracticeQuestions
            .AsNoTracking()
            .Where(q =>
                q.CourseId == classEntity.CourseId &&
                q.ApprovalStatus == ApprovalStatus.Approved &&
                q.IsActive &&
                !q.IsDeleted)
            .Select(q => new
            {
                q.CloId,
                q.Difficulty
            })
            .ToListAsync(cancellationToken);

        int bankEasy = 0;
        int bankMedium = 0;
        int bankHard = 0;

        var cloCountsMap = activeClos.ToDictionary(
            c => c.Id,
            _ => (Easy: 0, Medium: 0, Hard: 0));

        foreach (var q in approvedQuestions)
        {
            if (!InClassShiftPoolConfig.TryNormalizeDifficulty(q.Difficulty, out var diff))
            {
                continue;
            }

            switch (diff)
            {
                case "easy":
                    bankEasy++;
                    break;
                case "medium":
                    bankMedium++;
                    break;
                case "hard":
                    bankHard++;
                    break;
            }

            if (q.CloId.HasValue && cloCountsMap.TryGetValue(q.CloId.Value, out var current))
            {
                cloCountsMap[q.CloId.Value] = diff switch
                {
                    "easy" => (current.Easy + 1, current.Medium, current.Hard),
                    "medium" => (current.Easy, current.Medium + 1, current.Hard),
                    "hard" => (current.Easy, current.Medium, current.Hard + 1),
                    _ => current
                };
            }
        }

        var bankCountsDto = new InClassDifficultyCountsDto(
            bankEasy,
            bankMedium,
            bankHard,
            bankEasy + bankMedium + bankHard);

        var cloStatDtos = activeClos
            .Select(c =>
            {
                var (easy, medium, hard) = cloCountsMap[c.Id];
                return new InClassCloPoolStatDto(
                    c.Id,
                    c.CloCode,
                    c.Description,
                    c.OrderIndex,
                    new InClassDifficultyCountsDto(easy, medium, hard, easy + medium + hard));
            })
            .ToList();

        var response = new InClassShiftPoolStatsResponse(
            classEntity.Id,
            classEntity.Code,
            classEntity.Name,
            classEntity.CourseId,
            classEntity.Course.Code,
            classEntity.Course.Name,
            bankCountsDto,
            cloStatDtos);

        return Result<InClassShiftPoolStatsResponse>.Success(response);
    }
}
