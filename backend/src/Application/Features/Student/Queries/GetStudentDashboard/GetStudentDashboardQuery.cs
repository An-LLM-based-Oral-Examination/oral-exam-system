using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Student.DTOs;

namespace OralExamination.Application.Features.Student.Queries.GetStudentDashboard;

/// <summary>
/// Query tổng hợp toàn bộ chỉ số học tập, hạn ngạch thi thử và lịch thi thật trên Student Dashboard (FE-10 / BE-V03).
/// </summary>
public sealed record GetStudentDashboardQuery(Guid StudentId) : IRequest<Result<StudentDashboardDto>>;
