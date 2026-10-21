using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetRubrics;

public sealed record GetRubricsQuery(
    Guid? CourseId = null,
    Guid? RubricId = null
) : IRequest<Result<List<RubricDetailDto>>>;
