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
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null)
        {
            return Result<StartPracticeSessionResponse>.Failure("Môn học không tồn tại.");
        }

        // Đọc cấu hình thời gian đệm hiệu đính transcript do Admin cấu hình trong SystemConfigs (fallback cấu hình môn học hoặc 60s)
        var bufferConfig = await _context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "TranscriptBufferSeconds", cancellationToken);
        int bufferSeconds = (bufferConfig != null && int.TryParse(bufferConfig.Value, out var parsedBuffer))
            ? parsedBuffer
            : (course.TranscriptBufferSeconds > 0 ? course.TranscriptBufferSeconds : 60);

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
                return await HandleSingleDifficultyPracticeAsync(course, bufferSeconds, normalizedDifficulty, request, cancellationToken);
            }

            return await HandleProgressivePracticeAsync(course, bufferSeconds, request, cancellationToken);
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
                return await HandleProgressivePracticeAsync(course, bufferSeconds, request, cancellationToken);
            }

            return await HandleOnDemandPerQuestionAsync(course, bufferSeconds, normalizedDifficulties, request, cancellationToken);
        }

        return await HandleSingleDifficultyPracticeAsync(course, bufferSeconds, normalizedDifficulty, request, cancellationToken);
    }

    private async Task<Result<StartPracticeSessionResponse>> HandleOnDemandPerQuestionAsync(
        Course course,
        int bufferSeconds,
        List<string> normalizedDifficulties,
        StartPracticeSessionCommand request,
        CancellationToken cancellationToken)
    {
        var availableQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
            .ThenInclude(r => r.Criteria)
            .Where(q => q.CourseId == request.CourseId &&
                        q.IsActive &&
                        normalizedDifficulties.Contains(q.Difficulty.ToLower()))
            .ToListAsync(cancellationToken);

        if (!availableQuestions.Any())
        {
            return Result<StartPracticeSessionResponse>.Failure(
                "Kho đề hiện tại không có câu hỏi khả dụng cho các mức độ đã chọn. Vui lòng liên hệ giảng viên bổ sung câu hỏi.");
        }

        // Bốc 1 câu hỏi ngẫu nhiên công bằng giữa các độ khó
        var candidateDiffs = availableQuestions.Select(q => q.Difficulty.Trim().ToLowerInvariant()).Distinct().ToList();
        var chosenDiff = candidateDiffs[Random.Shared.Next(candidateDiffs.Count)];
        var diffPool = availableQuestions.Where(q => q.Difficulty.Trim().ToLowerInvariant() == chosenDiff).ToList();
        var selectedQuestion = diffPool[Random.Shared.Next(diffPool.Count)];

        var selectedQuestions = new List<PracticeQuestionDto>
        {
            new(
                selectedQuestion.Id,
                selectedQuestion.Content,
                selectedQuestion.Rubric != null ? selectedQuestion.Rubric.Criteria.Select(c => c.Description ?? string.Empty).ToList() : new List<string>()
            )
        };

        var session = new PracticeSession
        {
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            PracticeMode = "per_question",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            SelectedDifficulties = JsonSerializer.Serialize(normalizedDifficulties),
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
        StartPracticeSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Đọc cấu hình MinMixedPracticeQuestions và MaxMixedPracticeQuestions từ DB qua SystemConfigs (fallback 3 và 10)
        var minConfig = await _context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "MinMixedPracticeQuestions", cancellationToken);
        int minAllowed = (minConfig != null && int.TryParse(minConfig.Value, out var parsedMin)) ? parsedMin : 3;

        var maxMixedConfig = await _context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "MaxMixedPracticeQuestions", cancellationToken);
        int maxAllowed = (maxMixedConfig != null && int.TryParse(maxMixedConfig.Value, out var parsedMaxMixed)) ? parsedMaxMixed : 10;

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

        // Truy vấn toàn bộ câu hỏi active của môn học
        var allQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
            .ThenInclude(r => r.Criteria)
            .Where(q => q.CourseId == request.CourseId && q.IsActive)
            .ToListAsync(cancellationToken);

        var easyPool = allQuestions
            .Where(q => string.Equals(q.Difficulty, "easy", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var mediumPool = allQuestions
            .Where(q => string.Equals(q.Difficulty, "medium", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hardPool = allQuestions
            .Where(q => string.Equals(q.Difficulty, "hard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Kiểm tra số lượng trong kho đề
        if (easyPool.Count < neededEasy || mediumPool.Count < neededMedium || hardPool.Count < neededHard)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó: cần {neededEasy} Dễ (có {easyPool.Count}), {neededMedium} Trung bình (có {mediumPool.Count}), {neededHard} Khó (có {hardPool.Count}). Vui lòng liên hệ giảng viên bổ sung câu hỏi.");
        }

        // Bốc ngẫu nhiên theo từng mức và sắp xếp tăng dần: Dễ -> Trung bình -> Khó
        var selectedEasy = easyPool.OrderBy(_ => Guid.NewGuid()).Take(neededEasy);
        var selectedMedium = mediumPool.OrderBy(_ => Guid.NewGuid()).Take(neededMedium);
        var selectedHard = hardPool.OrderBy(_ => Guid.NewGuid()).Take(neededHard);

        var selectedQuestions = selectedEasy
            .Concat(selectedMedium)
            .Concat(selectedHard)
            .Select(q => new PracticeQuestionDto(
                q.Id,
                q.Content,
                q.Rubric != null ? q.Rubric.Criteria.Select(c => c.Description ?? string.Empty).ToList() : new List<string>()
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
            SelectedDifficulties = "[\"easy\",\"medium\",\"hard\"]",
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
        StartPracticeSessionCommand request,
        CancellationToken cancellationToken)
    {
        // Đọc cấu hình MaxPracticeQuestionsPerSession từ DB qua SystemConfigs (fallback 10)
        var maxConfig = await _context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "MaxPracticeQuestionsPerSession", cancellationToken);
        int maxAllowed = (maxConfig != null && int.TryParse(maxConfig.Value, out var parsedMax)) ? parsedMax : 10;

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

        // Lọc danh sách câu hỏi theo CourseId, IsActive và Difficulty
        var availableQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
            .ThenInclude(r => r.Criteria)
            .Where(q => q.CourseId == request.CourseId &&
                        q.Difficulty.ToLower() == normalizedDifficulty &&
                        q.IsActive)
            .ToListAsync(cancellationToken);

        // Kiểm tra nếu số lượng câu hỏi có trong kho < count
        if (availableQuestions.Count < count)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Kho đề hiện tại chỉ có {availableQuestions.Count} câu hỏi {difficultyLabel}, vui lòng chọn số lượng ít hơn.");
        }

        // Lấy ngẫu nhiên đúng count câu hỏi
        var selectedQuestions = availableQuestions
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .Select(q => new PracticeQuestionDto(
                q.Id,
                q.Content,
                q.Rubric != null ? q.Rubric.Criteria.Select(c => c.Description ?? string.Empty).ToList() : new List<string>()
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
            SelectedDifficulties = JsonSerializer.Serialize(new[] { normalizedDifficulty }),
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

