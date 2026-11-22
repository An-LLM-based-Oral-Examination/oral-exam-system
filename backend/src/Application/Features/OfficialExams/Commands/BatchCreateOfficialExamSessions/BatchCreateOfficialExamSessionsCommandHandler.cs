using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.BatchCreateOfficialExamSessions;

public sealed class BatchCreateOfficialExamSessionsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BatchCreateOfficialExamSessionsCommand, Result<BatchCreateOfficialExamSessionsResponse>>
{
    public async Task<Result<BatchCreateOfficialExamSessionsResponse>> Handle(
        BatchCreateOfficialExamSessionsCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SemesterId == Guid.Empty)
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(OfficialExamErrors.SemesterNotFound);
        }

        if (request.ExamDate == default)
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure("BAD_REQUEST: Ngày thi (examDate) không hợp lệ.");
        }

        var normalizedInputMode = string.IsNullOrWhiteSpace(request.ExamInputMode)
            ? ExamInputMode.VoiceWithTranscriptEdit
            : request.ExamInputMode.Trim();

        if (!ExamInputMode.All.Contains(normalizedInputMode, StringComparer.Ordinal))
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                "BAD_REQUEST: Chế độ nhập bài thi (examInputMode) chỉ chấp nhận VoiceOnly hoặc VoiceWithTranscriptEdit.");
        }

        if (request.TranscriptBufferSeconds < OfficialExamRules.MinTranscriptBufferSeconds
            || request.TranscriptBufferSeconds > OfficialExamRules.MaxTranscriptBufferSeconds)
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                $"BAD_REQUEST: Thời gian đệm chỉnh sửa transcript phải từ {OfficialExamRules.MinTranscriptBufferSeconds} đến {OfficialExamRules.MaxTranscriptBufferSeconds} giây.");
        }

        if (request.Courses is null || request.Courses.Count == 0)
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                "BAD_REQUEST: Danh sách môn học cần khởi tạo kỳ thi không được để trống.");
        }

        if (request.Courses.Any(item => item is null || item.CourseId == Guid.Empty))
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                "BAD_REQUEST: Danh sách môn học chứa mã môn (courseId) không hợp lệ.");
        }

        var defaultAttemptType = string.IsNullOrWhiteSpace(request.ExamAttemptType)
            ? OfficialExamAttemptType.FinalExam
            : request.ExamAttemptType.Trim().ToUpperInvariant();

        if (!OfficialExamAttemptType.IsValid(defaultAttemptType))
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                "BAD_REQUEST: Loại đợt thi (examAttemptType) chỉ chấp nhận FE hoặc RE.");
        }

        if (request.Courses.Any(item => item.Title is not null && item.Title.Trim().Length > 255))
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                "BAD_REQUEST: Tiêu đề kỳ thi không được vượt quá 255 ký tự.");
        }

        var normalizedItems = new List<(Guid CourseId, string AttemptType, string? Title)>(request.Courses.Count);
        foreach (var item in request.Courses)
        {
            var resolvedAttemptType = string.IsNullOrWhiteSpace(item.ExamAttemptType)
                ? defaultAttemptType
                : item.ExamAttemptType.Trim().ToUpperInvariant();

            if (!OfficialExamAttemptType.IsValid(resolvedAttemptType))
            {
                return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                    "BAD_REQUEST: Loại đợt thi (examAttemptType) chỉ chấp nhận FE hoặc RE.");
            }

            normalizedItems.Add((item.CourseId, resolvedAttemptType, item.Title));
        }

        var duplicateKeys = normalizedItems
            .GroupBy(item => (item.CourseId, item.AttemptType))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateKeys.Count > 0 && !request.SkipExisting)
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                "CONFLICT: Danh sách môn học gửi lên có cặp (môn học, loại đợt thi FE/RE) bị lặp lại.");
        }

        var distinctCourseItems = normalizedItems
            .GroupBy(item => (item.CourseId, item.AttemptType))
            .Select(group => group.First())
            .ToList();

        return await context.ExecuteOfficialSemesterBatchAsync(
            request.SemesterId,
            ct => ExecuteLockedAsync(request, normalizedInputMode, distinctCourseItems, ct),
            cancellationToken);
    }

    private async Task<Result<BatchCreateOfficialExamSessionsResponse>> ExecuteLockedAsync(
        BatchCreateOfficialExamSessionsCommand request,
        string normalizedInputMode,
        IReadOnlyList<(Guid CourseId, string AttemptType, string? Title)> courseItems,
        CancellationToken cancellationToken)
    {
        var semester = await context.Semesters
            .AsNoTracking()
            .Where(item => item.Id == request.SemesterId)
            .Select(item => new { item.Id, item.Code })
            .FirstOrDefaultAsync(cancellationToken);

        if (semester is null)
        {
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(OfficialExamErrors.SemesterNotFound);
        }

        var requestedCourseIds = courseItems.Select(item => item.CourseId).Distinct().ToList();
        var existingCourses = await context.Courses
            .AsNoTracking()
            .Where(course => requestedCourseIds.Contains(course.Id))
            .ToDictionaryAsync(course => course.Id, cancellationToken);

        foreach (var courseId in requestedCourseIds)
        {
            if (!existingCourses.TryGetValue(courseId, out var foundCourse) || !foundCourse.IsActive)
            {
                return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                    $"NOT_FOUND: {OfficialExamErrors.CourseNotFound}");
            }
        }

        var eligibleCourses = await OfficialExamSchedulingHelper.GetEligibleCoursesForSemesterAsync(
            context, semester.Id, cancellationToken);
        var eligibleCourseMap = eligibleCourses.ToDictionary(course => course.Id);

        foreach (var courseId in requestedCourseIds)
        {
            if (!eligibleCourseMap.ContainsKey(courseId))
            {
                var code = existingCourses[courseId].Code;
                return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                    $"UNPROCESSABLE: Môn học {code} hiện không được mở giảng dạy trong học kỳ {semester.Code}.");
            }
        }

        var existingSessionsInSemester = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(session =>
                requestedCourseIds.Contains(session.CourseId)
                && (session.SemesterId == semester.Id
                    || (session.SemesterId == Guid.Empty && session.Course.SemesterId == semester.Id)))
            .Select(session => new { session.Id, session.CourseId, session.ExamAttemptType, session.Title })
            .ToListAsync(cancellationToken);

        var existingSessionKeys = existingSessionsInSemester
            .Select(session => (session.CourseId, (session.ExamAttemptType ?? OfficialExamAttemptType.FinalExam).ToUpperInvariant()))
            .ToHashSet();

        var conflictedItems = courseItems
            .Where(item => existingSessionKeys.Contains((item.CourseId, item.AttemptType)))
            .ToList();

        if (!request.SkipExisting && conflictedItems.Count > 0)
        {
            var conflictedLabels = conflictedItems
                .Select(item => $"{eligibleCourseMap[item.CourseId].Code} ({item.AttemptType})")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase);
            return Result<BatchCreateOfficialExamSessionsResponse>.Failure(
                $"CONFLICT: Đợt thi ({string.Join(", ", conflictedLabels)}) đã tồn tại trong học kỳ {semester.Code}.");
        }

        var activeStructures = await context.ExamStructures
            .AsNoTracking()
            .Where(structure => requestedCourseIds.Contains(structure.CourseId) && structure.IsActive)
            .OrderByDescending(structure => structure.CreatedAt)
            .ThenBy(structure => structure.Id)
            .Select(structure => new { structure.Id, structure.CourseId })
            .ToListAsync(cancellationToken);

        var activeStructureByCourse = activeStructures
            .GroupBy(structure => structure.CourseId)
            .ToDictionary(group => group.Key, group => group.First().Id);

        var createdEntities = new List<OfficialExamSession>();
        var nowUtc = DateTime.UtcNow;

        foreach (var item in courseItems)
        {
            if (existingSessionKeys.Contains((item.CourseId, item.AttemptType)))
            {
                continue;
            }

            var course = eligibleCourseMap[item.CourseId];
            var attemptLabel = item.AttemptType == OfficialExamAttemptType.RetakeExam
                ? "Thi lại (RE)"
                : "Cuối kỳ (FE)";
            var resolvedTitle = string.IsNullOrWhiteSpace(item.Title)
                ? $"Kỳ thi Vấn đáp {attemptLabel} {semester.Code} - {course.Code}"
                : item.Title.Trim();

            var clampedMaxFollowUp = Math.Clamp(
                course.MaxFollowUpQuestions,
                OfficialExamRules.MinFollowUpQuestions,
                OfficialExamRules.MaxFollowUpQuestions);

            var session = new OfficialExamSession
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                SemesterId = semester.Id,
                ExamAttemptType = item.AttemptType,
                ExamStructureId = activeStructureByCourse.TryGetValue(course.Id, out var structureId)
                    ? structureId
                    : null,
                Title = resolvedTitle,
                ExamDate = request.ExamDate,
                HasFollowUp = course.HasFollowUp,
                MaxFollowUpQuestions = clampedMaxFollowUp,
                ExamInputMode = normalizedInputMode,
                TranscriptBufferSeconds = request.TranscriptBufferSeconds,
                Status = "scheduled",
                CreatedBy = request.ActorUserId,
                CreatedAt = nowUtc
            };

            createdEntities.Add(session);
        }

        if (createdEntities.Count > 0)
        {
            context.OfficialExamSessions.AddRange(createdEntities);
            context.AuditLogs.Add(new AuditLog
            {
                UserId = request.ActorUserId,
                Action = "OFFICIAL_EXAM_SESSIONS_BATCH_CREATED",
                EntityName = nameof(Semester),
                EntityId = semester.Id,
                NewValues = JsonSerializer.Serialize(new
                {
                    semesterId = semester.Id,
                    createdCount = createdEntities.Count,
                    sessionIds = createdEntities.Select(item => item.Id).ToArray(),
                    courseIds = createdEntities.Select(item => item.CourseId).ToArray(),
                    attemptTypes = createdEntities.Select(item => item.ExamAttemptType).ToArray()
                }),
                CreatedAt = nowUtc
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        var responseItems = createdEntities
            .Select(session =>
            {
                var course = eligibleCourseMap[session.CourseId];
                return new OfficialExamSessionResponse(
                    session.Id,
                    session.CourseId,
                    session.ExamStructureId,
                    session.Title,
                    session.ExamDate,
                    session.HasFollowUp,
                    session.MaxFollowUpQuestions,
                    session.ExamInputMode,
                    session.TranscriptBufferSeconds,
                    session.Status,
                    session.SemesterId,
                    session.ExamAttemptType,
                    course.Code,
                    course.Name,
                    0,
                    0);
            })
            .ToList();

        return Result<BatchCreateOfficialExamSessionsResponse>.Success(new BatchCreateOfficialExamSessionsResponse(
            semester.Id,
            responseItems.Count,
            responseItems));
    }
}
