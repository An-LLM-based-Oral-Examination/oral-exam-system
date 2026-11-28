using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamShiftStaff;

/// <summary>
/// Truy vấn danh sách nhân sự hợp lệ để phân công vào ca thi chính thức (MF-04):
/// - Proctors: chỉ tài khoản đang hoạt động có vai trò Giám thị (proctor).
/// - Examiners: ưu tiên Giảng viên phụ trách đúng môn học và Trưởng bộ môn của đúng môn/chương trình đó.
/// </summary>
public sealed record GetOfficialExamShiftStaffQuery(
    Guid ActorUserId,
    Guid? CourseId = null,
    Guid? SemesterId = null) : IRequest<Result<OfficialExamShiftStaffResponse>>;
