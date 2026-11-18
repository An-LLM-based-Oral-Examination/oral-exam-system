using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetSemesterEligibleExamCourses;

public sealed record GetSemesterEligibleExamCoursesQuery(
    Guid ActorUserId,
    Guid SemesterId) : IRequest<Result<IReadOnlyList<SemesterEligibleExamCourseDto>>>;
