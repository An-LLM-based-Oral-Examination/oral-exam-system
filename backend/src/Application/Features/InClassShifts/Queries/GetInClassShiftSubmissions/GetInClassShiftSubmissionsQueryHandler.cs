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

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftSubmissions;

public sealed class GetInClassShiftSubmissionsQueryHandler(
    IApplicationDbContext context)
    : IRequestHandler<GetInClassShiftSubmissionsQuery, Result<IReadOnlyList<InClassExamSubmissionResponse>>>
{
    public async Task<Result<IReadOnlyList<InClassExamSubmissionResponse>>> Handle(
        GetInClassShiftSubmissionsQuery request,
        CancellationToken cancellationToken)
    {
        var shift = await context.InClassExamShifts
            .AsNoTracking()
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<IReadOnlyList<InClassExamSubmissionResponse>>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
        {
            return Result<IReadOnlyList<InClassExamSubmissionResponse>>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        if (actor.Role == UserRole.Lecturer)
        {
            bool isAssigned = shift.LecturerId == actor.Id ||
                shift.Class.LecturerId == actor.Id ||
                await context.CourseLecturers.AnyAsync(
                    cl => cl.CourseId == shift.Class.CourseId && cl.LecturerUserId == actor.Id,
                    cancellationToken);

            if (!isAssigned)
            {
                return Result<IReadOnlyList<InClassExamSubmissionResponse>>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        var rawSubmissions = await context.InClassExamSubmissions
            .AsNoTracking()
            .Include(s => s.Student)
            .Where(s => s.ShiftId == request.ShiftId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync(cancellationToken);

        var questionIds = rawSubmissions
            .Select(s => s.QuestionId)
            .Distinct()
            .ToList();

        var questionMap = new Dictionary<Guid, (string Title, string Content)>();
        if (questionIds.Count > 0)
        {
            var practiceQs = await context.PracticeQuestions
                .AsNoTracking()
                .Where(q => questionIds.Contains(q.Id))
                .Select(q => new { q.Id, q.Title, q.Content })
                .ToListAsync(cancellationToken);

            foreach (var pq in practiceQs)
            {
                questionMap[pq.Id] = (pq.Title, pq.Content);
            }
        }

        var submissions = rawSubmissions
            .Select(s =>
            {
                questionMap.TryGetValue(s.QuestionId, out var qInfo);
                return new InClassExamSubmissionResponse(
                    s.Id,
                    s.ShiftId,
                    s.StudentId,
                    s.Student != null ? s.Student.FullName : null,
                    s.Student != null ? s.Student.Email : null,
                    s.QuestionId,
                    s.Transcript,
                    s.AiScore,
                    s.AiFeedback,
                    s.FinalScore,
                    s.LecturerNotes,
                    s.Status,
                    s.SubmittedAt,
                    s.GradedAt,
                    qInfo.Title,
                    qInfo.Content,
                    s.IsFollowUp,
                    s.ParentSubmissionId,
                    s.FollowUpIndex,
                    s.FollowUpPrompt,
                    s.AiConfidenceScore,
                    s.Student?.StudentCode
                );
            })
            .ToList();

        return Result<IReadOnlyList<InClassExamSubmissionResponse>>.Success(submissions);
    }
}
