using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;

public sealed class CreateDraftQuestionCommandHandler : IRequestHandler<CreateDraftQuestionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateDraftQuestionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateDraftQuestionCommand request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null)
            return Result<Guid>.Failure("Môn học không tồn tại.");
        if (!course.IsActive)
            return Result<Guid>.Failure("Môn học đã ngừng hoạt động hoặc chưa kích hoạt.");

        var actorUserId = request.ActorUserId.HasValue && request.ActorUserId.Value != Guid.Empty
            ? request.ActorUserId.Value
            : request.LecturerId;

        var actor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == actorUserId, cancellationToken);
        if (actor == null || !actor.IsActive || (actor.Role != UserRole.Lecturer && actor.Role != UserRole.DepartmentHead))
            return Result<Guid>.Failure("Người dùng không có quyền biên soạn câu hỏi.");

        if (request.CloId.HasValue && !await _context.CourseClos.AsNoTracking()
            .AnyAsync(c => c.Id == request.CloId.Value && c.CourseId == request.CourseId && c.IsActive, cancellationToken))
            return Result<Guid>.Failure("VALIDATION: CLO không tồn tại, không hoạt động hoặc không thuộc môn học của câu hỏi.");

        if (request.StructureId.HasValue && request.StructureId.Value != Guid.Empty && !await _context.ExamStructures.AsNoTracking()
            .AnyAsync(s => s.Id == request.StructureId.Value && s.CourseId == request.CourseId && s.IsActive, cancellationToken))
            return Result<Guid>.Failure("VALIDATION: Cấu trúc đề (ExamStructure) không tồn tại, không hoạt động hoặc không thuộc môn học của câu hỏi.");

        if (actor.Role == UserRole.Lecturer)
        {
            if (request.LecturerId != actor.Id)
            {
                return Result<Guid>.Failure("FORBIDDEN: Giảng viên không được phép biên soạn câu hỏi mạo danh người dùng khác.");
            }

            if (!await IsLecturerAssignedToCourseAsync(request.CourseId, actor.Id, cancellationToken))
            {
                return Result<Guid>.Failure("FORBIDDEN: Giảng viên chưa được phân công phụ trách môn học này nên không có quyền biên soạn câu hỏi.");
            }
        }
        else if (actor.Role == UserRole.DepartmentHead)
        {
            if (!AcademicCourseOwnership.IsOwnedBy(course, actor.Id))
            {
                return Result<Guid>.Failure("FORBIDDEN: Trưởng bộ môn không phụ trách môn học này nên không có quyền biên soạn câu hỏi.");
            }

            if (request.LecturerId != actor.Id)
            {
                var targetAuthor = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.LecturerId && u.IsActive && u.Role == UserRole.Lecturer, cancellationToken);
                if (targetAuthor == null || !await IsLecturerAssignedToCourseAsync(request.CourseId, targetAuthor.Id, cancellationToken))
                {
                    return Result<Guid>.Failure("FORBIDDEN: Giảng viên được chỉ định không hoạt động hoặc chưa được phân công phụ trách môn học này.");
                }
            }
        }

        var normalizedSource = (request.Source ?? "manual").Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(request.ImageUrl) && normalizedSource != "manual")
        {
            return Result<Guid>.Failure("Chỉ câu hỏi do giảng viên tạo thủ công (manual) mới được phép đính kèm hình ảnh.");
        }

        // Kiểm tra SampleAnswer >= 50 nếu có
        if (!string.IsNullOrEmpty(request.SampleAnswer) && request.SampleAnswer.Trim().Length < 50)
            return Result<Guid>.Failure("Câu trả lời mẫu (Model Answer) khi cung cấp bắt buộc phải từ 50 ký tự trở lên.");

        Rubric rubric;

        if (request.RubricId.HasValue && request.RubricId.Value != Guid.Empty && request.Rubric == null)
        {
            var existingRubric = await _context.Rubrics
                .Include(r => r.Criteria)
                .FirstOrDefaultAsync(r => r.Id == request.RubricId.Value && r.CourseId == request.CourseId && r.IsActive && !_context.ExamQuestions.Any(q => q.IsPaperCopy && q.RubricId == r.Id), cancellationToken);

            if (existingRubric == null)
                return Result<Guid>.Failure("Barem Rubric không tồn tại hoặc không thuộc môn học này.");

            if (existingRubric.Criteria == null || existingRubric.Criteria.Count < 2)
                return Result<Guid>.Failure("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");

            if (existingRubric.Criteria.Any(c => string.IsNullOrWhiteSpace(c.CriterionName) || c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength))
                return Result<Guid>.Failure("Tên từng tiêu chí con trong Barem Rubric không được để trống và tối đa 200 ký tự.");

            if (existingRubric.Criteria.Any(c => c.MaxScore <= 0))
                return Result<Guid>.Failure("Điểm tối đa của từng tiêu chí con trong Barem Rubric bắt buộc phải lớn hơn 0.");

            if (existingRubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                return Result<Guid>.Failure("Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");

            if (existingRubric.Criteria.Any(c => !RubricNormalization.HasValidScorePrecision(c.MaxScore)))
                return Result<Guid>.Failure("Điểm tối đa của từng tiêu chí con trong Barem Rubric chỉ chấp nhận tối đa 2 chữ số thập phân (numeric(4,2)).");

            rubric = existingRubric;
        }
        else
        {
            // Kiểm tra Barem 10.00
            if (request.Rubric?.Criteria == null || request.Rubric.Criteria.Count < 2)
                return Result<Guid>.Failure("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");

            if (string.IsNullOrWhiteSpace(request.Rubric.Name) || request.Rubric.Name.Trim().Length > RubricNormalization.MaxNameLength)
                return Result<Guid>.Failure("Tên Barem Rubric không được để trống và tối đa 200 ký tự.");

            if (request.Rubric.Criteria.Any(c => string.IsNullOrWhiteSpace(c.CriterionName) || c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength))
                return Result<Guid>.Failure("Tên từng tiêu chí con trong Barem Rubric không được để trống và tối đa 200 ký tự.");

            if (request.Rubric.Criteria.Any(c => c.MaxScore <= 0))
                return Result<Guid>.Failure("Điểm tối đa của từng tiêu chí con trong Barem Rubric bắt buộc phải lớn hơn 0.");

            if (request.Rubric.Criteria.Sum(c => c.MaxScore) != 10.00m)
                return Result<Guid>.Failure("Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");

            if (request.Rubric.Criteria.Any(c => !RubricNormalization.HasValidScorePrecision(c.MaxScore)))
                return Result<Guid>.Failure("Điểm tối đa của từng tiêu chí con trong Barem Rubric chỉ chấp nhận tối đa 2 chữ số thập phân (numeric(4,2)).");

            // 1. Tạo Rubric mới
            rubric = new Rubric
            {
                CourseId = request.CourseId,
                Name = request.Rubric.Name.Trim(),
                Description = request.Rubric.Description,
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            int order = 1;
            foreach (var c in request.Rubric.Criteria)
            {
                var normalizedWeight = RubricNormalization.NormalizeWeight(c.Weight, c.MaxScore);

                rubric.Criteria.Add(new RubricCriterion
                {
                    CriterionName = c.CriterionName.Trim(),
                    Description = c.Description,
                    MaxScore = c.MaxScore,
                    Weight = normalizedWeight,
                    BloomLevel = c.BloomLevel,
                    OrderIndex = order++,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.Rubrics.Add(rubric);
        }

        // 2. Tạo Question tách biệt độc lập hoàn toàn giữa 2 kho: practice_questions và exam_questions (R8)
        // Bất biến MF-03: Mọi câu hỏi do Giảng viên tạo đều phải ở trạng thái DRAFT chờ Trưởng Bộ môn duyệt;
        // PracticeQuestion chỉ được kích hoạt sau khi ReviewQuestionDecision phê duyệt.
        var keyPointsJson = JsonSerializer.Serialize(request.KeyPoints ?? new());
        var normalizedScope = (request.UsageScope ?? "exam").Trim().ToLowerInvariant();
        if (normalizedScope is not ("practice" or "exam" or "official_exam"))
        {
            return Result<Guid>.Failure("Phạm vi sử dụng câu hỏi không hợp lệ. Chỉ chấp nhận 'practice' (Luyện tập & Thi thử) hoặc 'exam' (Thi thật) tách biệt.");
        }

        var imageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        var imageDescription = string.IsNullOrWhiteSpace(request.ImageDescription) ? null : request.ImageDescription.Trim();

        // A draft is always reviewed in a separate action; request.ApprovedBy is untrusted input.
        var approvalStatus = ApprovalStatus.Draft;
        Guid? approvedBy = null;
        DateTime? approvedAt = null;

        bool createPractice = normalizedScope == "practice";
        bool createExam = !createPractice;

        var questionId = Guid.NewGuid();

        if (createExam)
        {
            var eq = new ExamQuestion
            {
                Id = questionId,
                CourseId = request.CourseId,
                CloId = request.CloId,
                StructureId = request.StructureId,
                RubricId = rubric.Id,
                Rubric = rubric,
                Title = request.Title,
                Content = request.Content,
                SampleAnswer = request.SampleAnswer,
                KeyPoints = keyPointsJson,
                Difficulty = request.Difficulty,
                BloomLevel = request.BloomLevel,
                Source = request.Source ?? "manual",
                ApprovalStatus = approvalStatus,
                ApprovedBy = approvedBy,
                ApprovedAt = approvedAt,
                SubmittedBy = request.LecturerId,
                ImageUrl = imageUrl,
                ImageDescription = imageDescription,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.ExamQuestions.Add(eq);
        }

        if (createPractice)
        {
            var pq = new PracticeQuestion
            {
                Id = questionId,
                CourseId = request.CourseId,
                CloId = request.CloId,
                RubricId = rubric.Id,
                Rubric = rubric,
                Title = request.Title,
                Content = request.Content,
                SampleAnswer = request.SampleAnswer,
                KeyPoints = keyPointsJson,
                Difficulty = request.Difficulty,
                BloomLevel = request.BloomLevel,
                HasFollowUp = request.HasFollowUp,
                FollowUpPrompt = request.FollowUpPrompt,
                ImageUrl = imageUrl,
                ImageDescription = imageDescription,
                Source = request.Source ?? "manual",
                ApprovalStatus = approvalStatus,
                ApprovedBy = approvedBy,
                ApprovedAt = approvedAt,
                SubmittedBy = request.LecturerId,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.PracticeQuestions.Add(pq);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(questionId);
    }

    private async Task<bool> IsLecturerAssignedToCourseAsync(Guid courseId, Guid lecturerUserId, CancellationToken cancellationToken)
    {
        return await _context.CourseLecturers
            .AsNoTracking()
            .AnyAsync(cl => cl.CourseId == courseId &&
                            cl.LecturerUserId == lecturerUserId &&
                            cl.IsActive &&
                            (cl.CourseOffering == null || cl.CourseOffering.IsEnabled), cancellationToken)
            || await _context.Classes
                .AsNoTracking()
                .AnyAsync(c => c.CourseId == courseId &&
                               c.LecturerId == lecturerUserId &&
                               c.IsActive &&
                               (c.CourseOffering == null || c.CourseOffering.IsEnabled), cancellationToken);
    }
}
