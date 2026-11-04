using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.GetStudentMyClasses;

public record GetStudentMyClassesQuery(Guid StudentId) : IRequest<Result<List<StudentClassDto>>>;
