using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;

public sealed class SubmitExamSetForReviewCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SubmitExamSetForReviewCommand, Result>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead
    ];

    public async Task<Result> Handle(SubmitExamSetForReviewCommand request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        if (actor is null ||
            !actor.IsActive ||
            !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .Include(es => es.Course)
                .ThenInclude(c => c!.AcademicProgram)
            .Include(es => es.ExamSetQuestions)
                .ThenInclude(esq => esq.ExamQuestion)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (examSet.Course is null || !examSet.Course.IsActive)
        {
            return Result.Failure(OfficialExamErrors.CourseNotFound);
        }

        var canSubmitExamSet = actor.Role switch
        {
            UserRole.DepartmentHead => AcademicCourseOwnership.IsOwnedBy(examSet.Course, actor.Id),
            UserRole.Lecturer => examSet.CreatedBy == actor.Id
                && await OfficialExamAuthorization.CanLecturerAccessCourseAsync(
                    context, examSet.CourseId, actor.Id, cancellationToken),
            _ => false
        };
        if (!canSubmitExamSet)
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        if (examSet.Status != ExamSetStatus.Draft && examSet.Status != ExamSetStatus.Rejected)
        {
            return Result.Failure(OfficialExamErrors.ExamSetStatusInvalid);
        }

        examSet.Status = ExamSetStatus.SubmittedForReview;
        examSet.SubmittedAt = DateTime.UtcNow;

        // Cascade any draft questions in this set to submitted_for_review
        foreach (var esq in examSet.ExamSetQuestions)
        {
            if (esq.ExamQuestion != null && esq.ExamQuestion.ApprovalStatus == ApprovalStatus.Draft)
            {
                esq.ExamQuestion.ApprovalStatus = ApprovalStatus.SubmittedForReview;
                esq.ExamQuestion.SubmittedBy ??= request.ActorUserId;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
