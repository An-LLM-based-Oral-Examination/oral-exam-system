using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;

public sealed class SubmitExamSetForReviewCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SubmitExamSetForReviewCommand, Result>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead,
        UserRole.ExaminationOfficer,
        UserRole.Admin
    ];

    public async Task<Result> Handle(SubmitExamSetForReviewCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorRole) &&
            !AllowedRoles.Contains(request.ActorRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .Include(es => es.ExamSetQuestions)
                .ThenInclude(esq => esq.ExamQuestion)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result.Failure(OfficialExamErrors.ExamSetNotFound);
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
