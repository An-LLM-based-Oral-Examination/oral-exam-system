using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;

public sealed record BatchSubmitQuestionsForReviewCommand(
    Guid CourseId,
    Guid LecturerId,
    List<Guid> QuestionIds,
    string? SubmissionNotes
) : IRequest<Result<BatchSubmitQuestionsForReviewResponseDto>>;
