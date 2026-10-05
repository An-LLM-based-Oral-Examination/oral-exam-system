using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;

public sealed record GetQuestionsQuery(
    Guid? CourseId = null,
    string? ApprovalStatus = null,
    string? Difficulty = null,
    string? BloomLevel = null,
    string? Source = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<QuestionSummaryDto>>>;
