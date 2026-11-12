using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftById;

public sealed class GetInClassShiftByIdQueryHandler(
    IApplicationDbContext context)
    : IRequestHandler<GetInClassShiftByIdQuery, Result<InClassShiftDetailResponse>>
{
    public async Task<Result<InClassShiftDetailResponse>> Handle(
        GetInClassShiftByIdQuery request,
        CancellationToken cancellationToken)
    {
        await InClassShiftLifecycle.CloseDueAsync(
            context,
            DateTime.UtcNow,
            cancellationToken,
            shiftId: request.ShiftId);

        var shift = await context.InClassExamShifts
            .AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Lecturer)
            .Include(s => s.Participants)
                .ThenInclude(p => p.Student)
            .Include(s => s.Submissions)
                .ThenInclude(sub => sub.Student)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<InClassShiftDetailResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive)
        {
            return Result<InClassShiftDetailResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        if (actor.Role == UserRole.Student)
        {
            bool isEnrolled = await context.ClassEnrollments.AnyAsync(
                e => e.ClassId == shift.ClassId && e.StudentId == actor.Id && e.Status == "enrolled",
                cancellationToken);

            if (!isEnrolled)
            {
                return Result<InClassShiftDetailResponse>.Failure(InClassShiftErrors.StudentNotEnrolled);
            }
        }
        else if (actor.Role == UserRole.Lecturer)
        {
            bool isAssigned = shift.LecturerId == actor.Id ||
                shift.Class.LecturerId == actor.Id ||
                await context.CourseLecturers.AnyAsync(
                    cl => cl.CourseId == shift.Class.CourseId && cl.LecturerUserId == actor.Id,
                    cancellationToken);

            if (!isAssigned)
            {
                return Result<InClassShiftDetailResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        // BOLA & Privacy Enforcement: Sinh viên chỉ được xem dữ liệu của chính mình
        bool isStudent = actor.Role == UserRole.Student;
        bool canStudentViewScores = !isStudent || shift.IsLocked || shift.Status == "FINALIZED" || shift.IsAutoPublishGrades;
        bool canStudentViewLecturerNotes = !isStudent || shift.IsLocked || shift.Status == "FINALIZED";

        var visibleParticipants = isStudent
            ? shift.Participants.Where(p => p.StudentId == actor.Id).ToList()
            : shift.Participants.ToList();

        var visibleSubmissions = isStudent
            ? shift.Submissions.Where(s => s.StudentId == actor.Id).ToList()
            : shift.Submissions.ToList();

        var questionIds = visibleParticipants
            .SelectMany(p =>
            {
                var ids = EnterInClassShiftCommandHandler.ParseGuidList(p.AssignedQuestionIdsJson);
                if (p.AssignedQuestionId.HasValue)
                {
                    ids.Add(p.AssignedQuestionId.Value);
                }
                return ids;
            })
            .Concat(visibleSubmissions.Select(s => s.QuestionId))
            .Distinct()
            .ToList();

        var questionMap = new Dictionary<Guid, InClassAssignedQuestionDto>();
        if (questionIds.Count > 0)
        {
            var practiceQs = await context.PracticeQuestions
                .AsNoTracking()
                .Where(q => questionIds.Contains(q.Id))
                .Select(q => new InClassAssignedQuestionDto(
                    q.Id,
                    q.Title,
                    q.Content,
                    q.Difficulty.ToString().ToLowerInvariant(),
                    q.BloomLevel.ToString().ToLowerInvariant()))
                .ToListAsync(cancellationToken);

            foreach (var pq in practiceQs)
            {
                questionMap[pq.QuestionId] = pq;
            }
        }

        var selectedPoolIds = EnterInClassShiftCommandHandler.ParseGuidList(shift.SelectedQuestionIdsJson);

        var participants = visibleParticipants
            .OrderBy(p => p.JoinedAt)
            .Select(p =>
            {
                var pIds = EnterInClassShiftCommandHandler.ParseGuidList(p.AssignedQuestionIdsJson);
                if (pIds.Count == 0 && p.AssignedQuestionId.HasValue)
                {
                    pIds.Add(p.AssignedQuestionId.Value);
                }

                var pDtos = pIds
                    .Where(id => questionMap.ContainsKey(id))
                    .Select(id => questionMap[id])
                    .ToList();

                InClassAssignedQuestionDto? firstAssignedQ = null;
                if (p.AssignedQuestionId.HasValue && questionMap.TryGetValue(p.AssignedQuestionId.Value, out var qDto))
                {
                    firstAssignedQ = qDto;
                }
                else if (pDtos.Count > 0)
                {
                    firstAssignedQ = pDtos[0];
                }

                return new InClassShiftParticipantResponse(
                    p.Id,
                    p.ShiftId,
                    p.StudentId,
                    p.Student?.FullName,
                    p.Student?.Email,
                    p.Status,
                    p.JoinedAt,
                    p.AssignedQuestionId,
                    firstAssignedQ,
                    pIds,
                    pDtos,
                    canStudentViewScores ? p.TotalAiScore : null,
                    canStudentViewScores ? p.TotalFinalScore : null,
                    p.Student?.StudentCode
                );
            })
            .ToList();

        var submissions = visibleSubmissions
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s =>
            {
                questionMap.TryGetValue(s.QuestionId, out var qDto);
                return new InClassExamSubmissionResponse(
                    s.Id,
                    s.ShiftId,
                    s.StudentId,
                    s.Student?.FullName,
                    s.Student?.Email,
                    s.QuestionId,
                    s.Transcript,
                    canStudentViewScores ? s.AiScore : null,
                    canStudentViewScores ? s.AiFeedback : null,
                    canStudentViewScores ? s.FinalScore : null,
                    canStudentViewLecturerNotes ? s.LecturerNotes : null,
                    s.Status,
                    s.SubmittedAt,
                    s.GradedAt,
                    qDto?.Title,
                    qDto?.Content,
                    s.IsFollowUp,
                    s.ParentSubmissionId,
                    s.FollowUpIndex,
                    s.FollowUpPrompt,
                    canStudentViewScores ? s.AiConfidenceScore : null,
                    s.Student?.StudentCode
                );
            })
            .ToList();

        var detail = new InClassShiftDetailResponse(
            shift.Id,
            shift.ClassId,
            shift.Class.Code,
            shift.Class.Name,
            shift.LecturerId,
            shift.Lecturer?.FullName,
            shift.ShiftName,
            shift.StartTime,
            shift.EndTime,
            shift.Status,
            shift.IsLocked,
            shift.FinalizedAt,
            shift.CreatedAt,
            participants,
            submissions,
            shift.Description,
            shift.QuestionPoolMode,
            selectedPoolIds,
            shift.TotalQuestionsPerStudent,
            shift.HasFollowUp,
            shift.MaxFollowUpQuestions,
            shift.IsAutoPublishGrades,
            shift.ExamDurationMinutes
        );
        return Result<InClassShiftDetailResponse>.Success(detail);
    }
}
