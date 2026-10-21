using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetFlmSyllabus;

public sealed record GetFlmSyllabusQuery(
    Guid CourseId,
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTopic = null,
    string? CloCode = null
) : IRequest<Result<FlmSyllabusResponseDto>>;
