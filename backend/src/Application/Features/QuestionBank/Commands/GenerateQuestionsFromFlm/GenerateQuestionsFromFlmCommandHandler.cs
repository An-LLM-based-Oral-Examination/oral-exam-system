using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.GenerateQuestionsFromFlm;

public sealed class GenerateQuestionsFromFlmCommandHandler 
    : IRequestHandler<GenerateQuestionsFromFlmCommand, Result<GenerateQuestionsFromFlmResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAiQuestionGenerationService _aiService;

    public GenerateQuestionsFromFlmCommandHandler(
        IApplicationDbContext context,
        IAiQuestionGenerationService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    public async Task<Result<GenerateQuestionsFromFlmResponseDto>> Handle(
        GenerateQuestionsFromFlmCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra Môn học
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (course == null)
        {
            return Result<GenerateQuestionsFromFlmResponseDto>.Failure("Không tìm thấy môn học tương ứng.");
        }

        // 2. Kiểm tra quyền người dùng (Phải là lecturer, department_head hoặc admin)
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.RequesterUserId && u.IsActive, cancellationToken);

        if (user == null || (user.Role != "lecturer" && user.Role != "department_head" && user.Role != "admin"))
        {
            return Result<GenerateQuestionsFromFlmResponseDto>.Failure(
                "Chỉ Giảng viên ('lecturer'), Trưởng Bộ Môn ('department_head') hoặc Quản trị viên mới được kích hoạt AI sinh câu hỏi.");
        }

        // 3. Đóng gói tham số gọi AI Service
        var aiParams = new AiQuestionGenerationParams(
            course.Code,
            course.Name,
            request.SelectedCloCodes,
            request.Topics,
            request.NumberOfQuestions,
            request.UsageScope,
            request.TargetBloomLevels,
            request.DifficultyDistribution.Easy,
            request.DifficultyDistribution.Medium,
            request.DifficultyDistribution.Hard
        );

        // 4. Kích hoạt AI Generation Service
        var generatedQuestions = await _aiService.GenerateQuestionsAsync(aiParams, cancellationToken);

        if (generatedQuestions == null || generatedQuestions.Count == 0)
        {
            return Result<GenerateQuestionsFromFlmResponseDto>.Failure(
                "Dịch vụ AI không thể sinh câu hỏi theo yêu cầu. Vui lòng thử lại.");
        }

        // 5. Trả về kết quả dự thảo cho Giảng viên xem trước & tinh chỉnh barem
        var response = new GenerateQuestionsFromFlmResponseDto(
            course.Id,
            course.Code,
            generatedQuestions.Count,
            "flm_api",
            generatedQuestions
        );

        return Result<GenerateQuestionsFromFlmResponseDto>.Success(response);
    }
}
