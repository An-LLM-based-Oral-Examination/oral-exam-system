using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
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

        var normalizedDifficulty = (request.Difficulty ?? string.Empty).Trim().ToLowerInvariant();

        // 2. Phân nhánh xử lý theo độ khó
        if (normalizedDifficulty == "progressive")
        {
            return await HandleProgressivePracticeAsync(course, bufferSeconds, request, cancellationToken);
        }

        return await HandleSingleDifficultyPracticeAsync(course, bufferSeconds, normalizedDifficulty, request, cancellationToken);
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

        if (request.QuestionCount < minAllowed || request.QuestionCount > maxAllowed)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ {minAllowed} đến {maxAllowed} câu.");
        }

        // Thuật toán phân bổ:
        // baseCount = N / 3, remainder = N % 3
        // remainder == 0 -> Easy = baseCount, Medium = baseCount, Hard = baseCount
        // remainder == 1 -> Easy = baseCount, Medium = baseCount + 1, Hard = baseCount
        // remainder == 2 -> Easy = baseCount + 1, Medium = baseCount + 1, Hard = baseCount
        int baseCount = request.QuestionCount / 3;
        int remainder = request.QuestionCount % 3;
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

        if (request.QuestionCount > maxAllowed)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Số lượng câu hỏi yêu cầu ({request.QuestionCount}) vượt quá giới hạn tối đa cho phép của hệ thống ({maxAllowed}).");
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

        // Kiểm tra nếu số lượng câu hỏi có trong kho < QuestionCount
        if (availableQuestions.Count < request.QuestionCount)
        {
            return Result<StartPracticeSessionResponse>.Failure(
                $"Kho đề hiện tại chỉ có {availableQuestions.Count} câu hỏi {difficultyLabel}, vui lòng chọn số lượng ít hơn.");
        }

        // Lấy ngẫu nhiên đúng QuestionCount câu hỏi
        var selectedQuestions = availableQuestions
            .OrderBy(_ => Guid.NewGuid())
            .Take(request.QuestionCount)
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
