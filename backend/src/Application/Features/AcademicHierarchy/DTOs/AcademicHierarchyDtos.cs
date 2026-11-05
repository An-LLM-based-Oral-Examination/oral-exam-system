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
/// DTO yêu cầu sinh viên tham gia lớp học phần bằng mã và mật khẩu.
/// </summary>
public record JoinClassRequest(
    string JoinCode,
    string? Password = null);

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
