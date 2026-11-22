using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.ImportSessionCandidates;

public sealed class PreviewSessionCandidateImportQueryHandler(IApplicationDbContext context)
    : IRequestHandler<PreviewSessionCandidateImportQuery, Result<CandidateRosterImportPreviewResponse>>
{
    public async Task<Result<CandidateRosterImportPreviewResponse>> Handle(
        PreviewSessionCandidateImportQuery request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<CandidateRosterImportPreviewResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        var evaluation = await CandidateRosterImportEvaluator.EvaluateAsync(
            context,
            request.SessionId,
            request.Mode,
            request.Rows,
            cancellationToken);

        return evaluation.IsSuccess
            ? Result<CandidateRosterImportPreviewResponse>.Success(evaluation.Value.Preview)
            : Result<CandidateRosterImportPreviewResponse>.Failure(evaluation.Error);
    }
}
