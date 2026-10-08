using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Queries.GetCourses;

/// <summary>
/// Query lấy danh sách toàn bộ các môn học đang kích hoạt (IsActive = true).
/// </summary>
public sealed record GetCoursesQuery : IRequest<Result<List<CourseDto>>>;
