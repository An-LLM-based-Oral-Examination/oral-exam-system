using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseLecturers;

public record GetCourseLecturersQuery(Guid CourseId, Guid ActorUserId = default, Guid? SemesterId = null) : IRequest<Result<List<CourseLecturerDto>>>;
