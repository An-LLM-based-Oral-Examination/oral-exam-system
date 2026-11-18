using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Queries.GetCourses;

/// <summary>
/// Query lấy danh sách toàn bộ các môn học đang kích hoạt (hoặc bao gồm cả inactive đối với Admin).
/// </summary>
public sealed record GetCoursesQuery(bool IncludeInactive = false) : IRequest<Result<List<CourseDto>>>;
