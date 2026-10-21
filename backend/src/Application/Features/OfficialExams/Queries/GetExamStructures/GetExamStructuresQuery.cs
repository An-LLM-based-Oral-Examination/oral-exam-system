using System;
using System.Collections.Generic;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures;

/// <summary>
/// Truy vấn danh sách cấu trúc đề thi (ExamStructure) theo môn học, ID hoặc trạng thái kích hoạt.
/// </summary>
public sealed record GetExamStructuresQuery(
    Guid? CourseId = null,
    Guid? StructureId = null,
    bool? IsActive = true) : IRequest<Result<List<ExamStructureDto>>>;
