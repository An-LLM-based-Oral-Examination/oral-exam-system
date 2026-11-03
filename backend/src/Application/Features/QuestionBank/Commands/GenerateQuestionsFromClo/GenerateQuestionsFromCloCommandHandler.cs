using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromClo;

public sealed class GenerateQuestionsFromCloCommandHandler 
    : IRequestHandler<GenerateQuestionsFromCloCommand, Result<GenerateQuestionsFromCloResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAiQuestionGenerationService _aiService;

    public GenerateQuestionsFromCloCommandHandler(
        IApplicationDbContext context,
        IAiQuestionGenerationService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    public async Task<Result<GenerateQuestionsFromCloResponseDto>> Handle(
        GenerateQuestionsFromCloCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra Môn học
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (course == null)
        {
            return Result<GenerateQuestionsFromCloResponseDto>.Failure("Không tìm thấy môn học tương ứng hoặc môn học đã bị khóa.");
        }

        // 2. Kiểm tra quyền người dùng (Phải là lecturer, department_head hoặc admin)
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.RequesterUserId && u.IsActive, cancellationToken);

        if (user == null || (user.Role != UserRole.Lecturer && user.Role != UserRole.DepartmentHead && user.Role != UserRole.Admin))
        {
            return Result<GenerateQuestionsFromCloResponseDto>.Failure(
                "Chỉ Giảng viên ('lecturer'), Trưởng Bộ Môn ('department_head') hoặc Quản trị viên mới được kích hoạt AI sinh câu hỏi.");
        }

        // 3. Truy vấn Chuẩn đầu ra thực tế từ database (Bảng course_clos 3NF)
        var closInDb = await _context.CourseClos
            .AsNoTracking()
            .Where(c => c.CourseId == request.CourseId && c.IsActive)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        var selectedClos = closInDb
            .Where(c => request.SelectedCloCodes.Any(reqCode => string.Equals(reqCode.Trim(), c.CloCode.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (selectedClos.Count == 0)
        {
            return Result<GenerateQuestionsFromCloResponseDto>.Failure(
                $"Không tìm thấy Chuẩn đầu ra (CLO) hợp lệ nào trong cơ sở dữ liệu cho môn học {course.Code}. Vui lòng cấu hình CLO trước.");
        }

        // 4. Đóng gói tham số gọi AI Service kèm ngữ cảnh thực tế từ course_clos
        var aiParams = new AiQuestionGenerationParams(
            course.Code,
            course.Name,
            selectedClos.Select(c => c.CloCode).ToList(),
            request.Topics,
            request.NumberOfQuestions,
            request.UsageScope,
            request.TargetBloomLevels,
            request.DifficultyDistribution.Easy,
            request.DifficultyDistribution.Medium,
            request.DifficultyDistribution.Hard
        );

        // 5. Kích hoạt AI Generation Service
        var generatedQuestions = await _aiService.GenerateQuestionsAsync(aiParams, cancellationToken);

        if (generatedQuestions == null || generatedQuestions.Count == 0)
        {
            return Result<GenerateQuestionsFromCloResponseDto>.Failure(
                "Dịch vụ AI không thể sinh câu hỏi theo yêu cầu. Vui lòng thử lại.");
        }

        // 6. Kiểm tra chống trùng lặp ngữ nghĩa với ngân hàng câu hỏi (isFlagged)
        var existingTitles = await _context.ExamQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == course.Id && q.IsActive)
            .Select(q => q.Title)
            .Union(_context.PracticeQuestions
                .AsNoTracking()
                .Where(q => q.CourseId == course.Id && q.IsActive)
                .Select(q => q.Title))
            .ToListAsync(cancellationToken);

        var enrichedQuestions = new List<GeneratedQuestionDraftDto>();
        foreach (var q in generatedQuestions)
        {
            bool isFlagged = false;
            string? flagReason = null;

            var duplicate = existingTitles.FirstOrDefault(t =>
                string.Equals(t.Trim(), q.Title.Trim(), StringComparison.OrdinalIgnoreCase) ||
                (t.Length > 15 && q.Title.Length > 15 && (t.Contains(q.Title, StringComparison.OrdinalIgnoreCase) || q.Title.Contains(t, StringComparison.OrdinalIgnoreCase))));

            if (duplicate != null)
            {
                isFlagged = true;
                flagReason = $"Phát hiện câu hỏi có nội dung tương đồng cao với câu hỏi đã có trong ngân hàng: '{duplicate}'.";
            }

            enrichedQuestions.Add(q with
            {
                Source = QuestionSource.AiGenerated,
                UsageScope = request.UsageScope,
                IsFlagged = isFlagged,
                FlagReason = flagReason
            });
        }

        // 7. Trả về kết quả dự thảo kèm Barem Rubric 10.0đ và Model Answer >= 50 ký tự
        var response = new GenerateQuestionsFromCloResponseDto(
            course.Id,
            course.Code,
            enrichedQuestions.Count,
            QuestionSource.AiGenerated,
            enrichedQuestions
        );

        return Result<GenerateQuestionsFromCloResponseDto>.Success(response);
    }
}
