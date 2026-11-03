using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Queries.GetCourseClos;

public sealed record GetCourseClosQuery(
    Guid CourseId,
    bool? ActiveOnly = null) : IRequest<Result<List<CourseCloDto>>>;
