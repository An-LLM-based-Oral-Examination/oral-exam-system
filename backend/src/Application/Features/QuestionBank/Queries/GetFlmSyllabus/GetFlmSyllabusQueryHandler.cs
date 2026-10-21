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

namespace OralExamination.Application.Features.QuestionBank.Queries.GetFlmSyllabus;

public sealed class GetFlmSyllabusQueryHandler
    : IRequestHandler<GetFlmSyllabusQuery, Result<FlmSyllabusResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetFlmSyllabusQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<FlmSyllabusResponseDto>> Handle(
        GetFlmSyllabusQuery request,
        CancellationToken cancellationToken)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (course == null)
        {
            return Result<FlmSyllabusResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy môn học hoặc môn học chưa được liên kết cấu trúc đề cương trên FLM.");
        }

        int pageNumber = Math.Max(1, request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 100);

        var clos = BuildCourseClos(course.Code, course.Name);
        var baseTopics = BuildStandardTopics(course.Code);

        var questionTitles = await _context.ExamQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == course.Id && q.IsActive)
            .OrderBy(q => q.CreatedAt)
            .Select(q => new { q.Title, q.BloomLevel })
            .Take(10)
            .ToListAsync(cancellationToken);

        var allTopics = new List<FlmTopicDto>(baseTopics);
        int nextSession = allTopics.Count + 1;

        foreach (var q in questionTitles)
        {
            if (!allTopics.Any(t => string.Equals(t.TopicName, q.Title, StringComparison.OrdinalIgnoreCase)))
            {
                var mappedClo = MapBloomToClo(q.BloomLevel);
                allTopics.Add(new FlmTopicDto(
                    $"topic-{nextSession:D2}",
                    nextSession,
                    q.Title,
                    new List<string> { mappedClo }
                ));
                nextSession++;
            }
        }

        IEnumerable<FlmTopicDto> filteredTopics = allTopics;

        if (!string.IsNullOrWhiteSpace(request.CloCode))
        {
            var cloFilter = request.CloCode.Trim();
            filteredTopics = filteredTopics.Where(t =>
                t.MappedClos.Any(c => string.Equals(c, cloFilter, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTopic))
        {
            var searchFilter = request.SearchTopic.Trim();
            filteredTopics = filteredTopics.Where(t =>
                t.TopicName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filteredTopics.ToList();
        int totalTopics = filteredList.Count;
        int totalPages = totalTopics == 0 ? 0 : (int)Math.Ceiling(totalTopics / (double)pageSize);

        var pagedTopics = filteredList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var pagination = new FlmPaginationMetadataDto(
            pageNumber,
            pageSize,
            totalTopics,
            totalPages
        );

        var response = new FlmSyllabusResponseDto(
            course.Id,
            course.Code,
            course.Name,
            $"FLM-{course.Code.ToUpperInvariant()}-FA26",
            course.Credits,
            clos,
            pagedTopics,
            pagination
        );

        return Result<FlmSyllabusResponseDto>.Success(response);
    }

    private static List<FlmCloDto> BuildCourseClos(string courseCode, string courseName)
    {
        return new List<FlmCloDto>
        {
            new("CLO1", $"Hiểu và giải thích được các nguyên lý kiến trúc cốt lõi của học phần {courseCode} ({courseName}).", "Understand"),
            new("CLO2", $"Vận dụng các mẫu thiết kế, công cụ và chuẩn kỹ thuật trong {courseCode} để giải quyết bài toán thực tế.", "Apply"),
            new("CLO3", $"Phân tích hiệu năng, bảo mật, luồng dữ liệu và xử lý ngoại lệ trong hệ thống {courseCode}.", "Analyze"),
            new("CLO4", $"Đánh giá, so sánh các giải pháp kiến trúc (trade-offs) và bảo vệ quyết định kỹ thuật cho {courseCode}.", "Evaluate")
        };
    }

    private static List<FlmTopicDto> BuildStandardTopics(string courseCode)
    {
        var normalized = (courseCode ?? string.Empty).Trim().ToUpperInvariant();

        if (normalized.Contains("PRN231") || normalized.Contains("PRN"))
        {
            return new List<FlmTopicDto>
            {
                new("topic-01", 1, "RESTful API Architecture & HTTP Semantics in ASP.NET Core", new List<string> { "CLO1", "CLO2" }),
                new("topic-02", 2, "Dependency Injection Lifetimes (Transient, Scoped, Singleton) & Service Container", new List<string> { "CLO1", "CLO2" }),
                new("topic-03", 3, "Entity Framework Core Change Tracking, LINQ Optimization & N+1 Query Mitigation", new List<string> { "CLO2", "CLO3" }),
                new("topic-04", 4, "Clean Architecture, CQRS Pattern with MediatR & Domain Validation", new List<string> { "CLO2", "CLO4" }),
                new("topic-05", 5, "JWT Authentication, Role-Based Authorization (RBAC) & OAuth 2.0 Security", new List<string> { "CLO3", "CLO4" }),
                new("topic-06", 6, "Real-Time Communication with SignalR Typed Hubs & Concurrency Control", new List<string> { "CLO3", "CLO4" })
            };
        }

        if (normalized.Contains("SWP391") || normalized.Contains("SWE") || normalized.Contains("SWR"))
        {
            return new List<FlmTopicDto>
            {
                new("topic-01", 1, "Software Requirements Engineering, Functional vs Non-Functional Specifications", new List<string> { "CLO1", "CLO2" }),
                new("topic-02", 2, "Domain-Driven Design (DDD), Bounded Contexts & Database Normalization (3NF)", new List<string> { "CLO2", "CLO3" }),
                new("topic-03", 3, "REST API Contract Design, RFC 7807 Problem Details & Idempotency", new List<string> { "CLO2", "CLO3" }),
                new("topic-04", 4, "Resilience Engineering: Retry Policies, Circuit Breaker & Dead-Letter Queues", new List<string> { "CLO3", "CLO4" }),
                new("topic-05", 5, "Automated Testing Strategy: Unit Tests, Integration Tests & Architecture Tests", new List<string> { "CLO3", "CLO4" })
            };
        }

        return new List<FlmTopicDto>
        {
            new("topic-01", 1, $"Tổng quan kiến trúc và các nguyên lý nền tảng trong {courseCode}", new List<string> { "CLO1", "CLO2" }),
            new("topic-02", 2, $"Thiết kế mô hình dữ liệu, giao thức tích hợp và luồng xử lý chính của {courseCode}", new List<string> { "CLO2", "CLO3" }),
            new("topic-03", 3, $"Tối ưu hóa hiệu năng, đồng thời (Concurrency) và xử lý lỗi trong {courseCode}", new List<string> { "CLO3", "CLO4" }),
            new("topic-04", 4, $"Bảo mật, kiểm thử tự động và thẩm định kiến trúc thực tế cho {courseCode}", new List<string> { "CLO2", "CLO4" })
        };
    }

    private static string MapBloomToClo(string? bloomLevel)
    {
        return (bloomLevel ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "remember" or "understand" => "CLO1",
            "apply" => "CLO2",
            "analyze" => "CLO3",
            "evaluate" or "create" => "CLO4",
            _ => "CLO2"
        };
    }
}
