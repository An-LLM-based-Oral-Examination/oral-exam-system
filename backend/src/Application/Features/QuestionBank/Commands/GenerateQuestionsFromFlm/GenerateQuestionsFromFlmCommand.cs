using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromFlm;

public sealed record GenerateQuestionsFromFlmCommand(
    Guid CourseId,
    Guid RequesterUserId,
    List<string> SelectedCloCodes,
    List<string> Topics,
    int NumberOfQuestions,
    string UsageScope,
    List<string> TargetBloomLevels,
    DifficultyDistributionDto DifficultyDistribution
) : IRequest<Result<GenerateQuestionsFromFlmResponseDto>>;
