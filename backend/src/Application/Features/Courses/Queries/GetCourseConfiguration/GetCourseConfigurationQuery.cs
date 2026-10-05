using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Queries.GetCourseConfiguration;

/// <summary>
/// CQRS Query tra cứu cấu hình động của môn học.
/// </summary>
public sealed record GetCourseConfigurationQuery(Guid CourseId) : IRequest<Result<CourseConfigurationDto>>;
