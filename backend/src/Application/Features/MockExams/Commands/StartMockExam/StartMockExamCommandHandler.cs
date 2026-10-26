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
using OralExamination.Application.Features.MockExams.Common;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.MockExams.Commands.StartMockExam;

public sealed class StartMockExamCommandHandler : IRequestHandler<StartMockExamCommand, Result<StartMockExamResponse>>
{
    private readonly IApplicationDbContext _context;

    public StartMockExamCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<StartMockExamResponse>> Handle(StartMockExamCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra môn học tồn tại và đang hoạt động
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (course == null)
        {
            return Result<StartMockExamResponse>.Failure("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        // 2. Kiểm tra Daily Quota Guard do Quản trị viên (Admin) cấu hình (Múi giờ GMT+7)
        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var sysQuotaStr = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => c.Key == "max_mock_exams_per_day" || c.Key == "MaxMockExamsPerDay")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var fallbackQuota = int.TryParse(sysQuotaStr, out var parsedSysQuota) && parsedSysQuota > 0 ? parsedSysQuota : 3;
        var maxDailyQuota = course.MaxMockExamsPerDay > 0 ? course.MaxMockExamsPerDay : fallbackQuota;

        var quota = await _context.MockExamQuotas
            .FirstOrDefaultAsync(q => q.StudentId == request.StudentId && q.CourseId == request.CourseId && q.QuotaDate == todayVn, cancellationToken);

        if (quota != null && quota.UsedCount >= maxDailyQuota)
        {
            return Result<StartMockExamResponse>.Failure($"QUOTA_EXCEEDED:Bạn đã sử dụng hết hạn ngạch thi thử trong ngày ({maxDailyQuota}/{maxDailyQuota} lượt) cho môn học này.");
        }

        // 3. Tra cứu ExamStructure mới nhất của môn học (nếu chưa có cấu trúc đề, khởi tạo và lưu cấu trúc mặc định để đảm bảo khóa ngoại FK exam_sets -> exam_structures)
        var structure = await _context.ExamStructures
            .AsNoTracking()
            .Where(s => s.CourseId == request.CourseId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (structure == null)
        {
            var creatorId = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && (u.Role == "department_head" || u.Role == "admin" || u.Id == request.StudentId))
                .OrderBy(u => u.Role == "department_head" ? 0 : u.Role == "admin" ? 1 : 2)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (creatorId == Guid.Empty)
            {
                creatorId = request.StudentId;
            }

            structure = new ExamStructure
            {
                Id = Guid.NewGuid(),
                CourseId = request.CourseId,
                CreatedBy = creatorId,
                Name = $"Cấu trúc mặc định - {course.Code}",
                TotalQuestions = 5,
                DurationMinutes = 30,
                BloomDistribution = "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.ExamStructures.Add(structure);
        }

        var targetTotalQuestions = structure.TotalQuestions > 0 ? structure.TotalQuestions : 5;

        // 4. Giải mã cấu trúc ma trận Bloom
        Dictionary<string, int>? bloomDict = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(structure.BloomDistribution))
            {
                bloomDict = JsonSerializer.Deserialize<Dictionary<string, int>>(structure.BloomDistribution, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch
        {
            // fallback nếu parse JSON thất bại
        }

        if (bloomDict == null || bloomDict.Count == 0)
        {
            bloomDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Remember"] = 1,
                ["Understand"] = 2,
                ["Apply"] = 2
            };
        }

        // 5. Lấy danh sách câu hỏi từ kho practice_questions (CÔ LẬP TUYỆT ĐỐI VỚI exam_questions)
        var allAvailableQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => q.CourseId == request.CourseId && q.IsActive)
            .ToListAsync(cancellationToken);

        if (allAvailableQuestions.Count < targetTotalQuestions)
        {
            return Result<StartMockExamResponse>.Failure($"Kho câu hỏi luyện tập của môn học hiện chỉ có {allAvailableQuestions.Count} câu, không đủ {targetTotalQuestions} câu theo cấu trúc đề thi.");
        }

        // 6. Thuật toán bốc đề theo Bloom Distribution kèm Fallback
        var questionsByBloom = allAvailableQuestions
            .GroupBy(q => q.BloomLevel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(_ => Guid.NewGuid()).ToList(), StringComparer.OrdinalIgnoreCase);

        var selectedQuestions = new List<PracticeQuestion>();
        var usedIds = new HashSet<Guid>();

        // Bốc theo phân bổ Bloom đã cấu hình
        foreach (var kvp in bloomDict)
        {
            var level = kvp.Key;
            var countNeeded = kvp.Value;

            if (questionsByBloom.TryGetValue(level, out var availableInLevel))
            {
                var candidates = availableInLevel.Where(q => !usedIds.Contains(q.Id)).Take(countNeeded).ToList();
                foreach (var c in candidates)
                {
                    selectedQuestions.Add(c);
                    usedIds.Add(c.Id);
                }
            }
        }

        // Fallback: Nếu thiếu câu theo mức Bloom, bù từ các câu còn lại trong kho practice_questions của môn học
        if (selectedQuestions.Count < targetTotalQuestions)
        {
            var remainingQuestions = allAvailableQuestions
                .Where(q => !usedIds.Contains(q.Id))
                .OrderBy(_ => Guid.NewGuid())
                .ToList();

            var needed = targetTotalQuestions - selectedQuestions.Count;
            var fillCandidates = remainingQuestions.Take(needed).ToList();
            foreach (var c in fillCandidates)
            {
                selectedQuestions.Add(c);
                usedIds.Add(c.Id);
            }
        }

        // Đảm bảo không vượt quá tổng số câu mục tiêu
        if (selectedQuestions.Count > targetTotalQuestions)
        {
            selectedQuestions = selectedQuestions.Take(targetTotalQuestions).ToList();
        }

        // Xáo ngẫu nhiên thứ tự các câu hỏi đã chọn trong đề thi
        selectedQuestions = selectedQuestions.OrderBy(_ => Guid.NewGuid()).ToList();

        // 7. Cập nhật lượt thi trong ngày (Daily Quota)
        if (quota == null)
        {
            quota = new MockExamQuota
            {
                Id = Guid.NewGuid(),
                StudentId = request.StudentId,
                CourseId = request.CourseId,
                QuotaDate = todayVn,
                UsedCount = 1,
                UpdatedAt = DateTime.UtcNow
            };
            _context.MockExamQuotas.Add(quota);
        }
        else
        {
            quota.UsedCount++;
            quota.UpdatedAt = DateTime.UtcNow;
        }

        // 8. Tính toán thời lượng làm bài
        // Không Follow-up: thời lượng từ structure (mặc định 30 phút).
        // Có Follow-up: cộng thêm 15 phút (tối thiểu 45 phút).
        var baseDuration = structure.DurationMinutes > 0 ? structure.DurationMinutes : 30;
        var durationMinutes = request.HasFollowUp ? Math.Max(45, baseDuration + 15) : baseDuration;
        var startedAt = DateTime.UtcNow;
        var expiresAt = startedAt.AddMinutes(durationMinutes);

        // 9. Khởi tạo ExamSet & MockExamSession & MockExamAnswer
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = structure.Id,
            CourseId = request.CourseId,
            SetCode = $"MOCK-{course.Code}-{DateTime.UtcNow:yyyyMMddHHmmss}-{uniqueSuffix}",
            GeneratedAt = startedAt,
            IsActive = true
        };
        _context.ExamSets.Add(examSet);

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            ExamSetId = examSet.Id,
            CourseId = request.CourseId,
            HasFollowUp = request.HasFollowUp,
            StartedAt = startedAt,
            ExpiresAt = expiresAt,
            Status = "in_progress",
            TotalScore = null
        };

        for (int i = 0; i < selectedQuestions.Count; i++)
        {
            var q = selectedQuestions[i];
            session.Answers.Add(new MockExamAnswer
            {
                Id = MockExamAnswerOrdering.CreateOrderedAnswerId(i + 1),
                SessionId = session.Id,
                QuestionId = q.Id,
                OrderIndex = i + 1,
                AnswerText = null,
                TimeTakenSeconds = 0,
                Status = "pending",
                AnsweredAt = startedAt.AddMilliseconds(i),
                IsFollowUp = false,
                ParentAnswerId = null,
                PromptText = null
            });
        }

        _context.MockExamSessions.Add(session);

        // 10. Lưu đồng bộ xuống CSDL
        await _context.SaveChangesAsync(cancellationToken);

        // 11. Chuẩn bị DTO phản hồi (Câu hỏi phụ chỉ được sinh động sau khi sinh viên nộp câu trả lời)
        var questionDtos = selectedQuestions.Select((q, idx) =>
        {
            var criteriaDtos = q.Rubric?.Criteria != null
                ? q.Rubric.Criteria
                    .OrderBy(c => c.OrderIndex)
                    .Select(c => new MockExamRubricCriterionDto(c.CriterionName, c.MaxScore))
                    .ToList()
                : new List<MockExamRubricCriterionDto>();

            return new MockExamQuestionDto(
                Id: q.Id,
                OrderIndex: idx + 1,
                Title: q.Title,
                Content: q.Content,
                BloomLevel: q.BloomLevel,
                Difficulty: q.Difficulty,
                RubricCriteria: criteriaDtos,
                ImageUrl: q.ImageUrl,
                ImageDescription: q.ImageDescription,
                FollowUpPrompt: null
            );
        }).ToList();

        var usedQuota = quota.UsedCount;
        var remainingQuota = Math.Max(0, maxDailyQuota - usedQuota);

        var response = new StartMockExamResponse(
            SessionId: session.Id,
            CourseId: course.Id,
            CourseCode: course.Code,
            CourseName: course.Name,
            HasFollowUp: request.HasFollowUp,
            DurationMinutes: durationMinutes,
            StartedAt: startedAt,
            ExpiresAt: expiresAt,
            UsedQuota: usedQuota,
            RemainingQuota: remainingQuota,
            MaxDailyQuota: maxDailyQuota,
            Questions: questionDtos
        );

        return Result<StartMockExamResponse>.Success(response);
    }
}
