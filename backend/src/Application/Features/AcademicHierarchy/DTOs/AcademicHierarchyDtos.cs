using System;
using System.Collections.Generic;

namespace OralExamination.Application.Features.AcademicHierarchy.DTOs;

/// <summary>
/// DTO yêu cầu gán Trưởng bộ môn vào môn học (Admin).
/// </summary>
public record AssignDepartmentHeadRequest(Guid DepartmentHeadUserId);

/// <summary>
/// DTO phản hồi gán Trưởng bộ môn vào môn học.
/// </summary>
public record AssignDepartmentHeadResponseDto(
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid DepartmentHeadUserId,
    string DepartmentHeadName,
    string DepartmentHeadEmail);

/// <summary>
/// DTO yêu cầu phân công danh sách giảng viên vào môn học (Trưởng bộ môn).
/// </summary>
public record AssignLecturersToCourseRequest(
    List<Guid> LecturerUserIds,
    Guid? SemesterId = null);

/// <summary>
/// DTO phản hồi phân công giảng viên vào môn học.
/// </summary>
public record AssignLecturersToCourseResponseDto(
    Guid CourseId,
    string CourseCode,
    string CourseName,
    int AssignedCount,
    List<CourseLecturerDto> Lecturers);

/// <summary>
/// DTO chi tiết thông tin giảng viên phụ trách môn học.
/// </summary>
public record CourseLecturerDto(
    Guid LecturerId,
    string FullName,
    string Email,
    string? StudentCode,
    DateTime AssignedAt,
    bool IsActive,
    Guid? SemesterId,
    string? SemesterCode,
    Guid? CourseOfferingId = null);

public sealed record SyncCourseOfferingLecturersRequest(
    IReadOnlyCollection<Guid> LecturerUserIds);

public sealed record SyncCourseOfferingLecturersResponseDto(
    Guid CourseOfferingId,
    Guid CourseId,
    string CourseCode,
    Guid SemesterId,
    string SemesterCode,
    int AssignedCount,
    int DisabledCount,
    IReadOnlyList<CourseLecturerDto> Lecturers);

public sealed record LecturerCourseOfferingDto(
    Guid CourseOfferingId,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    DateTime AssignedAt,
    bool IsAssignmentActive,
    bool IsOfferingEnabled);

/// <summary>
/// DTO yêu cầu tạo lớp học phần mới (Giảng viên).
/// </summary>
public record CreateClassRequest(
    string Code,
    string Name,
    Guid CourseId,
    Guid SemesterId,
    Guid? LecturerId = null,
    string? JoinCode = null,
    string? Password = null,
    int MaxStudents = 35,
    bool AllowSelfEnrollment = true,
    bool IsJoinEnabled = true,
    Guid? CourseOfferingId = null);

public sealed record CreateCourseOfferingClassRequest(
    string Code,
    string Name,
    string Password,
    Guid? LecturerId = null,
    string? JoinCode = null,
    int MaxStudents = 35,
    bool AllowSelfEnrollment = true,
    bool IsJoinEnabled = true);

/// <summary>
/// DTO phản hồi tạo lớp học phần mới.
/// </summary>
public record CreateClassResponseDto(
    Guid Id,
    string Code,
    string Name,
    string JoinCode,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    Guid? LecturerId,
    string? LecturerName,
    int MaxStudents,
    bool HasPassword,
    bool IsJoinEnabled,
    bool AllowSelfEnrollment,
    DateTime CreatedAt,
    Guid? CourseOfferingId = null);

/// <summary>
/// DTO yêu cầu sinh viên tham gia lớp học phần bằng mã (JoinCode hoặc ClassId) và mật khẩu.
/// </summary>
public record JoinClassRequest(
    string? JoinCode = null,
    string? Password = null,
    Guid? ClassId = null);

/// <summary>
/// DTO phản hồi sinh viên tham gia lớp học phần.
/// </summary>
public record JoinClassResponseDto(
    Guid ClassId,
    string ClassCode,
    string ClassName,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    Guid? LecturerId,
    string? LecturerName,
    DateTime EnrolledAt,
    string Status,
    string JoinMethod,
    Guid? CourseOfferingId = null);

/// <summary>
/// DTO thông tin lớp học của sinh viên.
/// </summary>
public record StudentClassDto(
    Guid ClassId,
    string ClassCode,
    string ClassName,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    Guid? LecturerId,
    string? LecturerName,
    DateTime EnrolledAt,
    string Status,
    string JoinMethod,
    Guid? CourseOfferingId = null);

/// <summary>
/// DTO hiển thị kết quả tìm kiếm lớp học đang mở cho sinh viên tìm và bấm tham gia.
/// </summary>
public sealed record ClassDirectoryItemDto(
    Guid ClassId,
    string ClassCode,
    string ClassName,
    string JoinCode,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    Guid? CourseOfferingId,
    Guid? LecturerId,
    string? LecturerName,
    int EnrolledCount,
    int MaxStudents,
    bool RequiresPassword,
    bool IsJoinEnabled,
    bool AllowSelfEnrollment,
    bool IsActive,
    bool IsAlreadyEnrolled,
    bool CanJoin,
    DateTime CreatedAt);

/// <summary>
/// DTO hiển thị danh sách lớp học do Giảng viên phụ trách.
/// </summary>
public sealed record LecturerClassItemDto(
    Guid ClassId,
    string Code,
    string Name,
    string JoinCode,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    Guid? CourseOfferingId,
    Guid? LecturerId,
    string? LecturerName,
    int EnrolledCount,
    int MaxStudents,
    int ShiftsCount,
    bool HasPassword,
    bool IsJoinEnabled,
    bool AllowSelfEnrollment,
    bool IsActive,
    DateTime CreatedAt,
    string? JoinPassword = null,
    int ActiveShiftCount = 0);

public sealed record ClassStudentLookupDto(
    Guid StudentId,
    string FullName,
    string Email,
    string? StudentCode);

/// <summary>
/// DTO thông tin sinh viên ghi danh trong lớp (hiển thị cho Giảng viên / TBM / Admin).
/// </summary>
public sealed record ClassEnrolledStudentDto(
    Guid StudentId,
    string FullName,
    string Email,
    string? StudentCode,
    DateTime EnrolledAt,
    string Status,
    string JoinMethod);

/// <summary>
/// DTO tóm tắt ca kiểm tra vấn đáp trong lớp học.
/// </summary>
public sealed record ClassInClassShiftSummaryDto(
    Guid ShiftId,
    string ShiftName,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    bool IsLocked,
    int ParticipantsCount,
    int SubmissionsCount,
    DateTime CreatedAt);

/// <summary>
/// DTO chi tiết lớp học phần (kèm danh sách bài kiểm tra và danh sách sinh viên).
/// </summary>
public sealed record ClassDetailDto(
    Guid ClassId,
    string Code,
    string Name,
    string JoinCode,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid SemesterId,
    string SemesterCode,
    Guid? CourseOfferingId,
    Guid? LecturerId,
    string? LecturerName,
    int EnrolledCount,
    int MaxStudents,
    bool HasPassword,
    bool IsJoinEnabled,
    bool AllowSelfEnrollment,
    bool IsActive,
    bool IsAlreadyEnrolled,
    DateTime CreatedAt,
    IReadOnlyList<ClassInClassShiftSummaryDto> Shifts,
    IReadOnlyList<ClassEnrolledStudentDto> EnrolledStudents,
    string? JoinPassword = null);
