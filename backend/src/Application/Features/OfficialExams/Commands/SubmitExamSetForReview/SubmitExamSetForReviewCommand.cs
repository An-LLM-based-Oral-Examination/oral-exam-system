using System;
using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;

public sealed record SubmitExamSetForReviewCommand(
    Guid ExamSetId,
    Guid ActorUserId,
    string? ActorRole = null) : IRequest<Result>;
