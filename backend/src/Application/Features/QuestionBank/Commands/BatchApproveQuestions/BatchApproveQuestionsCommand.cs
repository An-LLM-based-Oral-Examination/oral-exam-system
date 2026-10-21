using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;

public sealed record BatchApproveQuestionsCommand(
    Guid CourseId,
    Guid ApproverUserId,
    List<ApprovedQuestionItemDto> Questions
) : IRequest<Result<BatchApproveQuestionsResponseDto>>;
