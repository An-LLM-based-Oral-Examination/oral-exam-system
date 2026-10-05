using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;

public sealed record ReviewQuestionDecisionCommand(
    Guid QuestionId,
    Guid ReviewerId,
    string Decision,
    string ReviewNotes,
    string? TargetUsageScope
) : IRequest<Result<QuestionReviewDecisionResponseDto>>;
