using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;

public sealed record GetQuestionByIdQuery(Guid Id) : IRequest<Result<QuestionDetailDto>>;
