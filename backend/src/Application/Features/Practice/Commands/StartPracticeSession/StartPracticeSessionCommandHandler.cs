using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Features.Practice.Commands.StartPracticeSession;

public sealed class StartPracticeSessionCommandHandler : IRequestHandler<StartPracticeSessionCommand, Result<StartPracticeSessionResponse>>
{
    private readonly IApplicationDbContext _context;

    public StartPracticeSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<StartPracticeSessionResponse>> Handle(StartPracticeSessionCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy thông tin Course
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);
        if (course == null)
        {
            return Result<StartPracticeSessionResponse>.Failure("Môn học không tồn tại.");
        }

        // Đọc gộp toàn bộ cấu hình SystemConfigs cần thiết cho phiên luyện tập trong 1 lượt truy vấn duy nhất
        var configKeys = new[]
        {
            "TranscriptBufferSeconds",
            "MinMixedPracticeQuestions",
            "MaxMixedPracticeQuestions",
            "MaxPracticeQuestionsPerSession"
        };
        var systemConfigs = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => configKeys.Contains(c.Key))
            .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

        int bufferSeconds = (systemConfigs.TryGetValue("TranscriptBufferSeconds", out var bufferVal) && int.TryParse(bufferVal, out var parsedBuffer))
            ? parsedBuffer
            : (course.TranscriptBufferSeconds > 0 ? course.TranscriptBufferSeconds : 60);
        bufferSeconds = Math.Clamp(bufferSeconds, 10, 300);

        // Chuẩn hóa danh sách độ khó
        List<string> normalizedDifficulties;
        if (request.Difficulties != null && request.Difficulties.Any())
        {
            normalizedDifficulties = request.Difficulties
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();
        }
        else if (!string.IsNullOrWhiteSpace(request.Difficulty))
        {
            var d = request.Difficulty.Trim().ToLowerInvariant();
            normalizedDifficulties = (d == "progressive")
                ? new List<string> { "easy", "medium", "hard" }
                : new List<string> { d };
        }
        else
        {
            normalizedDifficulties = new List<string> { "easy", "medium", "hard" };
        }

        var normalizedDifficulty = (request.Difficulty ?? string.Empty).Trim().ToLowerInvariant();

        // 2. Chế độ Full-Session
        if (request.IsFullSession)
        {
            // Trường hợp đặc biệt: chọn đơn mức độ và count == 1 (legacy test)
            if (request.Difficulties == null && normalizedDifficulty != "progressive" && !string.IsNullOrWhiteSpace(normalizedDifficulty) && request.QuestionCount == 1)
            {
                return await HandleSingleDifficultyPracticeAsync(course, bufferSeconds, normalizedDifficulty, systemConfigs, request, cancellationToken);
            }

            return await HandleProgressivePracticeAsync(course, bufferSeconds, systemConfigs, request, cancellationToken);
        }

        // 3. Chế độ Per-Question
        if (request.Difficulties != null && request.Difficulties.Any())
        {
            return await HandleOnDemandPerQuestionAsync(course, bufferSeconds, normalizedDifficulties, request, cancellationToken);
        }

        if (normalizedDifficulty == "progressive")
        {
            if (request.QuestionCount.HasValue && request.QuestionCount.Value > 1)
            {
                return await HandleProgressivePracticeAsync(course, bufferSeconds, systemConfigs, request, cancellationToken);
            }

            return await HandleOnDemandPerQuestionAsync(course, bufferSeconds, normalizedDifficulties, request, cancellationToken);
        }

        return await HandleSingleDifficultyPracticeAsync(course, bufferSeconds, normalizedDifficulty, systemConfigs, request, cancellationToken);
    }

    private async Task<Result<StartPracticeSessionResponse>> HandleOnDemandPerQuestionAsync(
        Course course,
        int bufferSeconds,
        List<string> normalizedDifficulties,
        StartPracticeSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Pha 1: Chỉ chiếu (Project) Id và Difficulty nhẹ trên Index, không kéo toàn bộ Content/Rubric/Criteria của cả kho đề lên RAM
        var diffSet = normalizedDifficulties.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lightweightCandidates = await _context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == request.CourseId && q.IsActive)
            .Select(q => new { q.Id, q.Difficulty })
            .ToListAsync(cancellationToken);

        var matchingCandidates = lightweightCandidates
            .Where(q => !string.IsNullOrWhiteSpace(q.Difficulty) && diffSet.Contains(q.Difficulty.Trim()))
            .ToList();

        if (matchingCandidates.Count == 0)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                "Kho đề hiện tại không có câu hỏi khả dụng cho các mức độ đã chọn. Vui lòng liên hệ giảng viên bổ sung câu hỏi.");
        }

        // Bốc 1 câu hỏi ngẫu nhiên công bằng giữa các độ khó
        var candidateDiffs = matchingCandidates.Select(q => q.Difficulty.Trim().ToLowerInvariant()).Distinct().ToList();
        var chosenDiff = candidateDiffs[Random.Shared.Next(candidateDiffs.Count)];
        var diffPool = matchingCandidates.Where(q => q.Difficulty.Trim().ToLowerInvariant() == chosenDiff).ToList();
        var chosenId = diffPool[Random.Shared.Next(diffPool.Count)].Id;

        // Pha 2: Chỉ tải đầy đủ nội dung câu hỏi và Rubric Criteria cho đúng 1 câu hỏi đã được chọn
        var selectedQuestion = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .FirstAsync(q => q.Id == chosenId, cancellationToken);

        var selectedQuestions = new List<PracticeQuestionDto>
        {
            new(
                selectedQuestion.Id,
                selectedQuestion.Content,
                selectedQuestion.Rubric != null ? selectedQuestion.Rubric.Criteria.OrderBy(c => c.OrderIndex).Select(c => c.Description ?? string.Empty).ToList() : new List<string>(),
                selectedQuestion.ImageUrl,
                selectedQuestion.ImageDescription
            )
        };

        var session = new PracticeSession
        {
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            PracticeMode = "per_question",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            SelectedDifficulties = PracticeSession.SerializeDifficulties(normalizedDifficulties),
            SelectedQuestionIdsJson = PracticeSession.SerializeQuestionIds(selectedQuestions.Select(q => q.QuestionId)),
            Status = "in_progress"
        };

        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new StartPracticeSessionResponse(
            session.Id,
            bufferSeconds,
            selectedQuestions
        );

        return Result<StartPracticeSessionResponse>.Success(response);
    }

    private async Task<Result<StartPracticeSessionResponse>> HandleProgressivePracticeAsync(
        Course course,
        int bufferSeconds,
        IReadOnlyDictionary<string, string> systemConfigs,
        StartPracticeSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Đọc cấu hình MinMixedPracticeQuestions và MaxMixedPracticeQuestions đã lấy gộp từ SystemConfigs (fallback 3 và 10)
        int minAllowed = (systemConfigs.TryGetValue("MinMixedPracticeQuestions", out var minVal) && int.TryParse(minVal, out var parsedMin)) ? parsedMin : 3;
        int maxAllowed = (systemConfigs.TryGetValue("MaxMixedPracticeQuestions", out var maxVal) && int.TryParse(maxVal, out var parsedMaxMixed)) ? parsedMaxMixed : 10;

        int questionCount = request.QuestionCount ?? minAllowed;
        if (questionCount < minAllowed || questionCount > maxAllowed)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ {minAllowed} đến {maxAllowed} câu.");
        }

        // Thuật toán phân bổ:
        // baseCount = N / 3, remainder = N % 3
        // remainder == 0 -> Easy = baseCount, Medium = baseCount, Hard = baseCount
        // remainder == 1 -> Easy = baseCount, Medium = baseCount + 1, Hard = baseCount
        // remainder == 2 -> Easy = baseCount + 1, Medium = baseCount + 1, Hard = baseCount
        int baseCount = questionCount / 3;
        int remainder = questionCount % 3;
        int neededEasy = baseCount + (remainder == 2 ? 1 : 0);
        int neededMedium = baseCount + (remainder >= 1 ? 1 : 0);
        int neededHard = baseCount;

        // Pha 1: Chỉ truy vấn danh sách (Id, Difficulty) nhẹ trên Index của môn học
        var lightweightQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == request.CourseId && q.IsActive)
            .Select(q => new { q.Id, q.Difficulty })
            .ToListAsync(cancellationToken);

        var easyPool = lightweightQuestions
            .Where(q => string.Equals(q.Difficulty?.Trim(), "easy", StringComparison.OrdinalIgnoreCase))
            .Select(q => q.Id)
            .ToList();
        var mediumPool = lightweightQuestions
            .Where(q => string.Equals(q.Difficulty?.Trim(), "medium", StringComparison.OrdinalIgnoreCase))
            .Select(q => q.Id)
            .ToList();
        var hardPool = lightweightQuestions
            .Where(q => string.Equals(q.Difficulty?.Trim(), "hard", StringComparison.OrdinalIgnoreCase))
            .Select(q => q.Id)
            .ToList();

        // Kiểm tra số lượng trong kho đề
        if (easyPool.Count < neededEasy || mediumPool.Count < neededMedium || hardPool.Count < neededHard)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó: cần {neededEasy} Dễ (có {easyPool.Count}), {neededMedium} Trung bình (có {mediumPool.Count}), {neededHard} Khó (có {hardPool.Count}). Vui lòng liên hệ giảng viên bổ sung câu hỏi.");
        }

        // Bốc ngẫu nhiên theo từng mức và sắp xếp tăng dần: Dễ -> Trung bình -> Khó
        var orderedSelectedIds = easyPool.OrderBy(_ => Guid.NewGuid()).Take(neededEasy)
            .Concat(mediumPool.OrderBy(_ => Guid.NewGuid()).Take(neededMedium))
            .Concat(hardPool.OrderBy(_ => Guid.NewGuid()).Take(neededHard))
            .ToList();

        // Pha 2: Chỉ tải chi tiết câu hỏi kèm Rubric Criteria cho đúng các câu đã bốc trúng
        var selectedEntitiesMap = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => orderedSelectedIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, cancellationToken);

        var selectedQuestions = orderedSelectedIds
            .Where(selectedEntitiesMap.ContainsKey)
            .Select(id => selectedEntitiesMap[id])
            .Select(q => new PracticeQuestionDto(
                q.Id,
                q.Content,
                q.Rubric != null ? q.Rubric.Criteria.OrderBy(c => c.OrderIndex).Select(c => c.Description ?? string.Empty).ToList() : new List<string>(),
                q.ImageUrl,
                q.ImageDescription
            ))
            .ToList();

        // Tạo phiên luyện tập mới (hỗ trợ cả Per-Question và Full-Session)
        var session = new PracticeSession
        {
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            PracticeMode = request.IsFullSession ? "full_session" : "per_question",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            SelectedDifficulties = PracticeSession.SerializeDifficulties(new List<string> { "easy", "medium", "hard" }),
            SelectedQuestionIdsJson = PracticeSession.SerializeQuestionIds(selectedQuestions.Select(q => q.QuestionId)),
            Status = "in_progress"
        };

        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new StartPracticeSessionResponse(
            session.Id,
            bufferSeconds,
            selectedQuestions
        );

        return Result<StartPracticeSessionResponse>.Success(response);
    }

    private async Task<Result<StartPracticeSessionResponse>> HandleSingleDifficultyPracticeAsync(
        Course course,
        int bufferSeconds,
        string normalizedDifficulty,
        IReadOnlyDictionary<string, string> systemConfigs,
        StartPracticeSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Đọc cấu hình MaxPracticeQuestionsPerSession đã lấy gộp từ SystemConfigs (fallback 10)
        int maxAllowed = (systemConfigs.TryGetValue("MaxPracticeQuestionsPerSession", out var maxVal) && int.TryParse(maxVal, out var parsedMax)) ? parsedMax : 10;

        int count = (request.QuestionCount.HasValue && request.QuestionCount.Value > 0)
            ? request.QuestionCount.Value
            : 1;

        if (count > maxAllowed)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Số lượng câu hỏi yêu cầu ({count}) vượt quá giới hạn tối đa cho phép của hệ thống ({maxAllowed}).");
        }

        var difficultyLabel = normalizedDifficulty switch
        {
            "easy" => "Dễ",
            "hard" => "Khó",
            _ => "Trung bình"
        };

        // Pha 1: Chỉ truy vấn danh sách (Id, Difficulty) nhẹ trên Index của môn học
        var lightweightQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == request.CourseId && q.IsActive)
            .Select(q => new { q.Id, q.Difficulty })
            .ToListAsync(cancellationToken);

        var availableIds = lightweightQuestions
            .Where(q => string.Equals(q.Difficulty?.Trim(), normalizedDifficulty, StringComparison.OrdinalIgnoreCase))
            .Select(q => q.Id)
            .ToList();

        // Kiểm tra nếu số lượng câu hỏi có trong kho < count
        if (availableIds.Count < count)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Kho đề hiện tại chỉ có {availableIds.Count} câu hỏi {difficultyLabel}, vui lòng chọn số lượng ít hơn.");
        }

        // Lấy ngẫu nhiên đúng count câu hỏi và chỉ tải Rubric Criteria cho các câu được chọn
        var chosenIds = availableIds
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .ToList();

        var selectedEntitiesMap = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => chosenIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, cancellationToken);

        var selectedQuestions = chosenIds
            .Where(selectedEntitiesMap.ContainsKey)
            .Select(id => selectedEntitiesMap[id])
            .Select(q => new PracticeQuestionDto(
                q.Id,
                q.Content,
                q.Rubric != null ? q.Rubric.Criteria.OrderBy(c => c.OrderIndex).Select(c => c.Description ?? string.Empty).ToList() : new List<string>(),
                q.ImageUrl,
                q.ImageDescription
            ))
            .ToList();

        // Tạo phiên luyện tập mới
        var session = new PracticeSession
        {
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            PracticeMode = request.IsFullSession ? "full_session" : "per_question",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            SelectedDifficulties = PracticeSession.SerializeDifficulties(new[] { normalizedDifficulty }),
            SelectedQuestionIdsJson = PracticeSession.SerializeQuestionIds(selectedQuestions.Select(q => q.QuestionId)),
            Status = "in_progress"
        };

        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new StartPracticeSessionResponse(
            session.Id,
            bufferSeconds,
            selectedQuestions
        );

        return Result<StartPracticeSessionResponse>.Success(response);
    }
}
