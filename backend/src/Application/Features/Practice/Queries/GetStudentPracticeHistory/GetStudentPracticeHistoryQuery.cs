using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;

namespace OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory;

/// <summary>
/// Query lấy danh sách lịch sử các phiên luyện tập của sinh viên.
/// </summary>
public sealed record GetStudentPracticeHistoryQuery(Guid StudentId) : IRequest<Result<List<PracticeHistoryItemDto>>>;
