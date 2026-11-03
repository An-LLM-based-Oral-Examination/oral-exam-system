using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Infrastructure.Persistence;

public static partial class DevelopmentDataSeeder
{
    public static readonly Guid CoursePrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000001");
    public static readonly Guid CourseCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000002");
    public static readonly Guid CourseDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000003");

    public static readonly Guid RubricPrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000011");
    public static readonly Guid RubricCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000012");
    public static readonly Guid RubricDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000013");

    public static readonly Guid StructurePrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000021");
    public static readonly Guid StructureCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000022");
    public static readonly Guid StructureDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000023");

    public static readonly Guid ClassPrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000041");
    public static readonly Guid ClassCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000042");
    public static readonly Guid ClassDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000043");

    public static readonly Guid ExamSetPrn231Set02Id = Guid.Parse("00000000-0000-0000-0000-00000000001a");
    public static readonly Guid ExamSetPrf192Id = Guid.Parse("00000000-0000-0000-0003-000000000031");
    public static readonly Guid ExamSetCsd201Id = Guid.Parse("00000000-0000-0000-0003-000000000032");
    public static readonly Guid ExamSetDbi202Id = Guid.Parse("00000000-0000-0000-0003-000000000033");

    public static readonly Guid ExaminationOfficerUserId = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    public static async Task SeedAdditionalThreeCoursesAndClosAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        await EnsureCourseClosAsync(context, cancellationToken);
        await EnsureAdditionalCoursesAsync(context, cancellationToken);
        await EnsureAdditionalRubricsAndCriteriaAsync(context, cancellationToken);
        await EnsureAdditionalExamStructuresAndSetsAsync(context, cancellationToken);
        await EnsureAdditionalPracticeQuestionsAsync(context, cancellationToken);
        await EnsureAdditionalClassesAndEnrollmentsAsync(context, cancellationToken);
    }

    private static async Task EnsureCourseClosAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var existingClos = (await context.CourseClos
            .Select(c => new { c.CourseId, c.CloCode })
            .ToListAsync(cancellationToken))
            .Select(x => $"{x.CourseId}:{x.CloCode}")
            .ToHashSet();

        var targetClos = new List<CourseClo>
        {
            // PRN231
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000061"), CourseId = CoursePrn231Id, CloCode = "CLO1", Description = "Hiểu và áp dụng kiến trúc ASP.NET Core Web API, Middleware, Dependency Injection", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000062"), CourseId = CoursePrn231Id, CloCode = "CLO2", Description = "Thiết kế và triển khai RESTful API, DTOs, Validation và xử lý lỗi chuẩn RFC 7807", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000063"), CourseId = CoursePrn231Id, CloCode = "CLO3", Description = "Tích hợp Entity Framework Core, Migration, tối ưu hóa truy vấn và giao dịch cơ sở dữ liệu", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000064"), CourseId = CoursePrn231Id, CloCode = "CLO4", Description = "Bảo mật hệ thống bằng JWT, OAuth 2.0, phân quyền RBAC và kiểm thử tự động", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // SWD392
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000261"), CourseId = CourseSwd392Id, CloCode = "CLO1", Description = "Phân tích yêu cầu và chuyển hóa thành các mô hình kiến trúc phần mềm tiêu chuẩn", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000262"), CourseId = CourseSwd392Id, CloCode = "CLO2", Description = "Áp dụng các mẫu kiến trúc Microservices, Clean Architecture và DDD", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000263"), CourseId = CourseSwd392Id, CloCode = "CLO3", Description = "Đánh giá các thuộc tính chất lượng phần mềm bao gồm bảo mật, mở rộng và hiệu năng", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000264"), CourseId = CourseSwd392Id, CloCode = "CLO4", Description = "Thiết kế chi tiết bằng biểu đồ UML và các Design Patterns GoF", BloomLevel = BloomLevel.Create, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // SWR302
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000361"), CourseId = CourseSwr302Id, CloCode = "CLO1", Description = "Phân loại yêu cầu phần mềm và quy trình kỹ thuật yêu cầu", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000362"), CourseId = CourseSwr302Id, CloCode = "CLO2", Description = "Áp dụng kỹ thuật phỏng vấn, workshop, viết Use Case và User Story", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000363"), CourseId = CourseSwr302Id, CloCode = "CLO3", Description = "Phân tích ma trận truy vết RTM, kiểm soát thay đổi Scope Creep", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000364"), CourseId = CourseSwr302Id, CloCode = "CLO4", Description = "Thẩm định chất lượng tài liệu đặc tả SRS theo tiêu chuẩn IEEE", BloomLevel = BloomLevel.Evaluate, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // PRF192
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000461"), CourseId = CoursePrf192Id, CloCode = "CLO1", Description = "Hiểu biến, kiểu dữ liệu, toán tử và cấu trúc rẽ nhánh if/else, switch/case", BloomLevel = BloomLevel.Remember, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000462"), CourseId = CoursePrf192Id, CloCode = "CLO2", Description = "Áp dụng vòng lặp for, while, mảng 1 chiều và chuỗi ký tự xử lý dữ liệu", BloomLevel = BloomLevel.Understand, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000463"), CourseId = CoursePrf192Id, CloCode = "CLO3", Description = "Sử dụng con trỏ, quản lý bộ nhớ động và hàm truyền tham chiếu", BloomLevel = BloomLevel.Apply, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000464"), CourseId = CoursePrf192Id, CloCode = "CLO4", Description = "Phân tích cấu trúc Struct và thao tác đọc ghi file văn bản/nhị phân", BloomLevel = BloomLevel.Analyze, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // CSD201
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000561"), CourseId = CourseCsd201Id, CloCode = "CLO1", Description = "Đánh giá độ phức tạp thời gian và không gian thuật toán bằng ký pháp Big-O", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000562"), CourseId = CourseCsd201Id, CloCode = "CLO2", Description = "Cài đặt và vận dụng danh sách liên kết đơn/đôi, ngăn xếp Stack và hàng đợi Queue", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000563"), CourseId = CourseCsd201Id, CloCode = "CLO3", Description = "Phân tích cấu trúc dữ liệu cây nhị phân tìm kiếm BST và cây cân bằng AVL", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000564"), CourseId = CourseCsd201Id, CloCode = "CLO4", Description = "Áp dụng thuật toán đồ thị duyệt BFS/DFS, tìm đường đi ngắn nhất Dijkstra và cây khung nhỏ nhất", BloomLevel = BloomLevel.Analyze, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc },

            // DBI202
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000661"), CourseId = CourseDbi202Id, CloCode = "CLO1", Description = "Hiểu mô hình thực thể kết hợp ERD và kỹ thuật ánh xạ sang lược đồ cơ sở dữ liệu quan hệ", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000662"), CourseId = CourseDbi202Id, CloCode = "CLO2", Description = "Viết truy vấn SQL phức tạp với JOIN, GROUP BY, HAVING và Subqueries", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000663"), CourseId = CourseDbi202Id, CloCode = "CLO3", Description = "Chuẩn hóa dữ liệu theo các dạng chuẩn 1NF, 2NF, 3NF và BCNF loại bỏ dư thừa", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000664"), CourseId = CourseDbi202Id, CloCode = "CLO4", Description = "Phân tích giao dịch ACID, cơ chế khóa chống Deadlock và tối ưu hóa truy vấn bằng Index", BloomLevel = BloomLevel.Analyze, OrderIndex = 4, IsActive = true, CreatedAt = SeedTimestampUtc }
        };

        foreach (var clo in targetClos)
        {
            var key = $"{clo.CourseId}:{clo.CloCode}";
            if (!existingClos.Contains(key))
            {
                context.CourseClos.Add(clo);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAdditionalCoursesAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var semester = await context.Semesters
            .FirstOrDefaultAsync(s => s.Id == SemesterFa26Id || s.Code == "FA26", cancellationToken);
        var semesterId = semester?.Id ?? SemesterFa26Id;

        var courses = new List<Course>
        {
            new()
            {
                Id = CoursePrf192Id,
                Code = "PRF192",
                Name = "Programming Fundamentals",
                Credits = 3,
                SemesterId = semesterId,
                HasFollowUp = true,
                TranscriptBufferSeconds = 60,
                MaxFollowUpQuestions = 2,
                MaxMockExamsPerDay = 3,
                ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = CourseCsd201Id,
                Code = "CSD201",
                Name = "Data Structures and Algorithms",
                Credits = 3,
                SemesterId = semesterId,
                HasFollowUp = true,
                TranscriptBufferSeconds = 60,
                MaxFollowUpQuestions = 2,
                MaxMockExamsPerDay = 3,
                ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = CourseDbi202Id,
                Code = "DBI202",
                Name = "Introduction to Databases",
                Credits = 3,
                SemesterId = semesterId,
                HasFollowUp = true,
                TranscriptBufferSeconds = 60,
                MaxFollowUpQuestions = 2,
                MaxMockExamsPerDay = 3,
                ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }
        };

        var existingCodes = (await context.Courses.Select(c => c.Code).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var course in courses)
        {
            if (!existingCodes.Contains(course.Code))
            {
                context.Courses.Add(course);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAdditionalRubricsAndCriteriaAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var existingRubrics = (await context.Rubrics.Select(r => r.Id).ToListAsync(cancellationToken)).ToHashSet();

        var rubrics = new List<Rubric>
        {
            new()
            {
                Id = RubricPrf192Id,
                CourseId = CoursePrf192Id,
                Name = "Rubric Đánh giá Vấn đáp PRF192 Chuẩn Barem 10.0đ (FA26)",
                Description = "Barem chấm thi vấn đáp chuẩn hóa cho PRF192: Cú pháp & Thuật toán (3đ) + Con trỏ & Bộ nhớ (4đ) + Mảng & File (3đ) = 10.0đ",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricCsd201Id,
                CourseId = CourseCsd201Id,
                Name = "Rubric Đánh giá Vấn đáp CSD201 Chuẩn Barem 10.0đ (FA26)",
                Description = "Barem chấm thi vấn đáp chuẩn hóa cho CSD201: Big-O & Tuyến tính (3đ) + Cấu trúc Cây (4đ) + Đồ thị & Tối ưu (3đ) = 10.0đ",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricDbi202Id,
                CourseId = CourseDbi202Id,
                Name = "Rubric Đánh giá Vấn đáp DBI202 Chuẩn Barem 10.0đ (FA26)",
                Description = "Barem chấm thi vấn đáp chuẩn hóa cho DBI202: ERD & Quan hệ (3đ) + SQL Nâng cao (4đ) + Chuẩn hóa 3NF & ACID (3đ) = 10.0đ",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }
        };

        foreach (var rubric in rubrics)
        {
            if (!existingRubrics.Contains(rubric.Id))
            {
                context.Rubrics.Add(rubric);
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var existingCriteria = (await context.RubricCriteria.Select(rc => rc.Id).ToListAsync(cancellationToken)).ToHashSet();

        var criteria = new List<RubricCriterion>
        {
            // PRF192 Criteria (3.0 + 4.0 + 3.0 = 10.00m, Weight 0.3 + 0.4 + 0.3 = 1.00)
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000111"), RubricId = RubricPrf192Id, CriterionName = "Cú pháp & Tư duy thuật toán cơ bản", Description = "Nắm vững kiểu dữ liệu, toán tử, cấu trúc rẽ nhánh if/else, switch/case và vòng lặp for, while.", MaxScore = 3.00m, Weight = 0.30m, BloomLevel = BloomLevel.Understand, OrderIndex = 1, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000112"), RubricId = RubricPrf192Id, CriterionName = "Bản chất Con trỏ & Quản lý bộ nhớ động", Description = "Hiểu địa chỉ ô nhớ, toán tử & và *, cấp phát động malloc/calloc/free, truyền tham chiếu và tránh rò rỉ bộ nhớ.", MaxScore = 4.00m, Weight = 0.40m, BloomLevel = BloomLevel.Apply, OrderIndex = 2, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000113"), RubricId = RubricPrf192Id, CriterionName = "Tổ chức hàm & Thao tác cấu trúc Struct / File", Description = "Tách module hàm tái sử dụng, định nghĩa struct phức hợp và thao tác đọc ghi file văn bản/nhị phân chuẩn C.", MaxScore = 3.00m, Weight = 0.30m, BloomLevel = BloomLevel.Analyze, OrderIndex = 3, CreatedAt = SeedTimestampUtc },

            // CSD201 Criteria (3.0 + 4.0 + 3.0 = 10.00m, Weight 0.3 + 0.4 + 0.3 = 1.00)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000511"), RubricId = RubricCsd201Id, CriterionName = "Phân tích Big-O & Cấu trúc tuyến tính", Description = "Đánh giá chính xác độ phức tạp thời gian/không gian và vận dụng danh sách liên kết, Stack, Queue.", MaxScore = 3.00m, Weight = 0.30m, BloomLevel = BloomLevel.Understand, OrderIndex = 1, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000512"), RubricId = RubricCsd201Id, CriterionName = "Cấu trúc phi tuyến (Tree, BST, AVL)", Description = "Nắm vững duyệt cây Pre/In/Post-order, thao tác chèn/xóa trên BST và các phép xoay cân bằng cây AVL.", MaxScore = 4.00m, Weight = 0.40m, BloomLevel = BloomLevel.Analyze, OrderIndex = 2, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000513"), RubricId = RubricCsd201Id, CriterionName = "Thuật toán Đồ thị & Tối ưu hóa", Description = "Áp dụng thuật toán tìm kiếm BFS, DFS, đường đi ngắn nhất Dijkstra và cây khung nhỏ nhất Prim/Kruskal.", MaxScore = 3.00m, Weight = 0.30m, BloomLevel = BloomLevel.Analyze, OrderIndex = 3, CreatedAt = SeedTimestampUtc },

            // DBI202 Criteria (3.0 + 4.0 + 3.0 = 10.00m, Weight 0.3 + 0.4 + 0.3 = 1.00)
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000611"), RubricId = RubricDbi202Id, CriterionName = "Thiết kế ERD & Đại số quan hệ", Description = "Mô hình hóa thực thể kết hợp (ERD), xác định khóa chính/khóa ngoại và chuyển đổi sang lược đồ quan hệ.", MaxScore = 3.00m, Weight = 0.30m, BloomLevel = BloomLevel.Understand, OrderIndex = 1, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000612"), RubricId = RubricDbi202Id, CriterionName = "Kỹ năng Truy vấn SQL nâng cao & Ràng buộc", Description = "Viết câu truy vấn phức tạp sử dụng JOIN, GROUP BY, HAVING, Subquery, Window Functions và CTE.", MaxScore = 4.00m, Weight = 0.40m, BloomLevel = BloomLevel.Apply, OrderIndex = 2, CreatedAt = SeedTimestampUtc },
            new() { Id = Guid.Parse("00000000-0000-0000-0000-000000000613"), RubricId = RubricDbi202Id, CriterionName = "Chuẩn hóa 3NF, Transaction ACID & Indexing", Description = "Chuẩn hóa từ 1NF đến 3NF loại bỏ dư thừa, hiểu tính chất ACID trong giao dịch và tối ưu hóa bằng B-Tree Index.", MaxScore = 3.00m, Weight = 0.30m, BloomLevel = BloomLevel.Analyze, OrderIndex = 3, CreatedAt = SeedTimestampUtc }
        };

        foreach (var c in criteria)
        {
            if (!existingCriteria.Contains(c.Id))
            {
                context.RubricCriteria.Add(c);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAdditionalExamStructuresAndSetsAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var existingStructures = (await context.ExamStructures.Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();

        var structures = new List<ExamStructure>
        {
            new()
            {
                Id = StructurePrf192Id,
                CourseId = CoursePrf192Id,
                CreatedBy = DepartmentHeadUserId,
                Name = "Cấu trúc Đề thi Vấn đáp PRF192 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 30,
                BloomDistribution = "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}",
                SlotDefinitions = "[]",
                RequiredSetCount = 2,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureCsd201Id,
                CourseId = CourseCsd201Id,
                CreatedBy = DepartmentHeadUserId,
                Name = "Cấu trúc Đề thi Vấn đáp CSD201 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 30,
                BloomDistribution = "{\"Understand\": 1, \"Apply\": 2, \"Analyze\": 2}",
                SlotDefinitions = "[]",
                RequiredSetCount = 2,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureDbi202Id,
                CourseId = CourseDbi202Id,
                CreatedBy = DepartmentHeadUserId,
                Name = "Cấu trúc Đề thi Vấn đáp DBI202 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 30,
                BloomDistribution = "{\"Understand\": 1, \"Apply\": 2, \"Analyze\": 2}",
                SlotDefinitions = "[]",
                RequiredSetCount = 2,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }
        };

        foreach (var structure in structures)
        {
            if (!existingStructures.Contains(structure.Id))
            {
                context.ExamStructures.Add(structure);
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var existingSets = (await context.ExamSets.Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();

        var sets = new List<ExamSet>
        {
            new()
            {
                Id = ExamSetPrn231Set02Id,
                StructureId = StructurePrn231Id,
                CourseId = CoursePrn231Id,
                SetCode = "PRN231-SET02",
                Status = ExamSetStatus.Official,
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetPrf192Id,
                StructureId = StructurePrf192Id,
                CourseId = CoursePrf192Id,
                SetCode = "PRF192-SET01",
                Status = ExamSetStatus.Official,
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetCsd201Id,
                StructureId = StructureCsd201Id,
                CourseId = CourseCsd201Id,
                SetCode = "CSD201-SET01",
                Status = ExamSetStatus.Official,
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetDbi202Id,
                StructureId = StructureDbi202Id,
                CourseId = CourseDbi202Id,
                SetCode = "DBI202-SET01",
                Status = ExamSetStatus.Official,
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            }
        };

        foreach (var set in sets)
        {
            if (!existingSets.Contains(set.Id))
            {
                context.ExamSets.Add(set);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAdditionalPracticeQuestionsAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var existingQuestions = (await context.PracticeQuestions.Select(q => q.Id).ToListAsync(cancellationToken)).ToHashSet();

        var questions = new List<PracticeQuestion>();

        // PRF192: 12 Questions (4 Easy, 4 Medium, 4 Hard)
        // Easy 1..4
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0003-000000000121"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Phân biệt kiểu dữ liệu int, float, double trong ngôn ngữ C",
            Content = "Trình bày sự khác nhau về kích thước bộ nhớ, dải giá trị và độ chính xác biểu diễn giữa int, float và double trong C.",
            SampleAnswer = "Trong chuẩn C thông thường, int chiếm 4 bytes lưu số nguyên có dấu; float chiếm 4 bytes biểu diễn số thực dấu chấm động độ chính xác đơn; double chiếm 8 bytes biểu diễn số thực độ chính xác kép với sai số tính toán nhỏ hơn nhiều.",
            KeyPoints = "[\"Kích thước bộ nhớ int 4 bytes, float 4 bytes, double 8 bytes\",\"Độ chính xác kép của double so với float\",\"Dải giá trị và cách biểu diễn số học\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Remember,
            HasFollowUp = true,
            FollowUpPrompt = "Khi nào ta nên ưu tiên sử dụng double thay vì float trong bài toán kỹ thuật?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0003-000000000122"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Cấu trúc rẽ nhánh if/else và toán tử 3 ngôi trong C",
            Content = "Giải thích cú pháp và trường hợp sử dụng của cấu trúc if/else lồng nhau và toán tử điều kiện 3 ngôi (condition ? expr1 : expr2).",
            SampleAnswer = "Cấu trúc if/else kiểm tra biểu thức điều kiện boolean để rẽ nhánh thực thi các khối mã lệnh phức tạp. Toán tử 3 ngôi là dạng viết tắt biểu thức trả về một giá trị dựa trên điều kiện, thường dùng khi gán giá trị đơn giản và ngắn gọn.",
            KeyPoints = "[\"Cú pháp if/else và luồng thực thi\",\"Toán tử 3 ngôi trả về giá trị biểu thức\",\"So sánh khả năng đọc hiểu và ứng dụng\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            HasFollowUp = true,
            FollowUpPrompt = "Có nên lồng nhiều toán tử 3 ngôi vào nhau không và tại sao?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0003-000000000123"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Vòng lặp while và do-while trong ngôn ngữ C",
            Content = "Phân biệt sự khác nhau căn bản giữa vòng lặp while và do-while về số lần thực thi tối thiểu và điều kiện dừng.",
            SampleAnswer = "Vòng lặp while kiểm tra điều kiện trước khi thực hiện thân vòng lặp, nên số lần lặp tối thiểu là 0 nếu điều kiện sai ngay từ đầu. Ngược lại, do-while thực thi thân vòng lặp trước rồi mới kiểm tra điều kiện, đảm bảo luôn chạy ít nhất 1 lần.",
            KeyPoints = "[\"Kiểm tra điều kiện trước (while) và sau (do-while)\",\"Số lần lặp tối thiểu là 0 hoặc 1\",\"Trường hợp thực tế phù hợp cho do-while như nhập dữ liệu\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            HasFollowUp = true,
            FollowUpPrompt = "Hãy cho một ví dụ thực tế bắt buộc phải dùng do-while thay vì while.",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000424"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Mảng một chiều và ký tự kết thúc chuỗi '\\0' trong C",
            Content = "Trình bày cách lưu trữ mảng một chiều trong bộ nhớ liên tục và vai trò của ký tự null '\\0' trong chuỗi ký tự C.",
            SampleAnswer = "Mảng một chiều là tập hợp các phần tử cùng kiểu dữ liệu được cấp phát trên các ô nhớ liên tiếp. Trong ngôn ngữ C, một chuỗi thực chất là mảng các ký tự char kết thúc bằng ký tự null '\\0' để báo hiệu điểm kết thúc chuỗi cho các hàm thư viện.",
            KeyPoints = "[\"Cấp phát bộ nhớ liên tục cho mảng một chiều\",\"Chuỗi C là mảng char kết thúc bằng '\\0'\",\"Hậu quả khi thiếu ký tự null trong thao tác chuỗi\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Remember,
            HasFollowUp = true,
            FollowUpPrompt = "Điều gì xảy ra nếu gọi strlen hoặc printf với chuỗi không có ký tự '\\0'?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        // Medium 5..8
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000425"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Bản chất của biến con trỏ và toán tử & và * trong C",
            Content = "Giải thích ý nghĩa của biến con trỏ, toán tử lấy địa chỉ & và toán tử giải tham chiếu * trong việc truy xuất vùng nhớ.",
            SampleAnswer = "Biến con trỏ là biến dùng để lưu trữ địa chỉ vùng nhớ của một biến khác. Toán tử & dùng để trích xuất địa chỉ ô nhớ của biến, còn toán tử * (dereference) dùng để đọc hoặc ghi giá trị trực tiếp tại địa chỉ mà con trỏ đang trỏ tới.",
            KeyPoints = "[\"Con trỏ lưu địa chỉ ô nhớ thay vì giá trị thông thường\",\"Toán tử & trích xuất địa chỉ, toán tử * giải tham chiếu\",\"Truy cập gián tiếp giá trị qua con trỏ\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Thế nào là con trỏ hoang (wild pointer) hoặc con trỏ lơ lửng (dangling pointer)?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000426"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Truyền tham trị (Pass-by-value) và truyền tham chiếu (Pass-by-reference)",
            Content = "So sánh cơ chế truyền tham số theo giá trị và truyền con trỏ mô phỏng tham chiếu trong việc hoán đổi giá trị 2 biến.",
            SampleAnswer = "Trong C, mọi tham số hàm đều truyền theo giá trị (sao chép giá trị vào stack frame mới). Để thay đổi giá trị của biến gốc bên ngoài hàm như bài toán swap(a, b), ta phải truyền con trỏ chứa địa chỉ của biến vào hàm để thao tác trực tiếp trên ô nhớ gốc.",
            KeyPoints = "[\"Cơ chế copy giá trị khi truyền tham trị\",\"Sử dụng con trỏ để mô phỏng truyền tham chiếu\",\"Viết hàm swap hoán vị chính xác bằng con trỏ\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Tại sao hàm swap(int a, int b) lại không làm thay đổi giá trị ở hàm main?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000427"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Cấp phát động malloc, calloc và giải phóng bộ nhớ free",
            Content = "Giải thích sự khác nhau giữa malloc và calloc khi cấp phát bộ nhớ trên Heap và tầm quan trọng của việc gọi free.",
            SampleAnswer = "Cả malloc và calloc đều cấp phát vùng nhớ động trên Heap. malloc nhận số bytes và không khởi tạo giá trị (chứa dữ liệu rác), trong khi calloc nhận số phần tử kèm kích thước và khởi tạo toàn bộ các bytes về giá trị 0. Bắt buộc gọi free để tránh rò rỉ bộ nhớ Memory Leak.",
            KeyPoints = "[\"Phân biệt khởi tạo bộ nhớ giữa malloc và calloc\",\"Vùng nhớ Heap so với Stack\",\"Hậu quả nghiêm trọng của Memory Leak khi quên gọi free\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Sau khi gọi free(ptr), ta có nên gán ptr = NULL không và tại sao?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000428"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Định nghĩa và quản lý kiểu dữ liệu Struct trong C",
            Content = "Trình bày cách khai báo một Struct quản lý thông tin sinh viên và cách truy xuất trường qua toán tử dấu chấm '.' và mũi tên '->'.",
            SampleAnswer = "Struct gom nhóm các biến có kiểu dữ liệu khác nhau lại thành một thực thể có ý nghĩa. Ta dùng toán tử dấu chấm '.' để truy xuất thành phần từ biến struct thông thường, và dùng toán tử mũi tên '->' để truy xuất trường khi đang thao tác qua biến con trỏ trỏ tới struct.",
            KeyPoints = "[\"Khai báo struct gom nhóm các kiểu dữ liệu khác nhau\",\"Toán tử chấm '.' truy xuất trực tiếp\",\"Toán tử mũi tên '->' tương đương (*ptr).member\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Tại sao nên truyền con trỏ struct vào hàm thay vì truyền cả biến struct nguyên bản?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        // Hard 9..12
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000429"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Con trỏ hàm (Function Pointer) và cơ chế Callback trong C",
            Content = "Giải thích cú pháp con trỏ hàm, cách truyền hàm như tham số cho thuật toán sắp xếp qsort tổng quát trong thư viện C.",
            SampleAnswer = "Con trỏ hàm lưu trữ địa chỉ vùng nhớ chứa mã máy của một hàm. Bằng cách truyền con trỏ hàm làm đối số, ta có thể cài đặt cơ chế Callback cho phép hàm qsort gọi hàm so sánh compare(const void*, const void*) do người dùng định nghĩa để sắp xếp mảng kiểu dữ liệu tùy ý.",
            KeyPoints = "[\"Cú pháp khai báo con trỏ hàm\",\"Cơ chế Callback trong kiến trúc phần mềm C\",\"Ứng dụng trong hàm sắp xếp tổng quát qsort\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Làm sao ép kiểu con trỏ void* trong hàm so sánh compare của qsort?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000042a"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Kỹ thuật cấp phát động mảng hai chiều bằng con trỏ cấp 2 (int**)",
            Content = "Trình bày các bước cấp phát động một ma trận m dòng n cột bằng con trỏ cấp 2 và quy trình giải phóng bộ nhớ đúng cách.",
            SampleAnswer = "Đầu tiên dùng malloc cấp phát một mảng con trỏ cấp 1 gồm m phần tử kiểu int*. Sau đó duyệt qua từng dòng để cấp phát tiếp mảng n phần tử int. Khi giải phóng bộ nhớ, bắt buộc phải duyệt giải phóng từng dòng con trước bằng free(matrix[i]), cuối cùng mới free(matrix) con trỏ ngoài.",
            KeyPoints = "[\"Cấp phát mảng con trỏ dòng trước, cấp phát từng cột sau\",\"Con trỏ cấp 2 quản lý mảng con trỏ\",\"Thứ tự giải phóng ngược lại từ trong ra ngoài chống rò rỉ\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Nếu ta giải phóng free(matrix) trước các dòng con thì điều gì sẽ xảy ra?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000042b"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Đọc ghi file nhị phân (Binary File) bằng fread và fwrite",
            Content = "So sánh đọc ghi file nhị phân với file văn bản và giải thích cách lưu trữ một mảng Struct sinh viên xuống file nhị phân.",
            SampleAnswer = "File văn bản mã hóa dữ liệu thành các ký tự ASCII/UTF-8 nên tốn dung lượng và cần phân giải chuỗi. File nhị phân ghi trực tiếp khối bytes từ bộ nhớ RAM xuống đĩa bằng fwrite(&sv, sizeof(Student), 1, fp), giúp tốc độ I/O nhanh hơn vượt trội và bảo toàn định dạng dữ liệu nguyên vẹn.",
            KeyPoints = "[\"Ghi trực tiếp block bytes từ RAM xuống đĩa bằng fwrite\",\"Đọc dữ liệu nguyên khối struct bằng fread\",\"Hiệu năng I/O và sự khác biệt với file văn bản fprintf/fscanf\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Tại sao ghi struct chứa biến con trỏ xuống file nhị phân lại gây lỗi crash khi đọc lại ở phiên chạy khác?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000042c"),
            CourseId = CoursePrf192Id,
            RubricId = RubricPrf192Id,
            Title = "Phân tích lỗi tràn bộ đệm (Buffer Overflow) và rủi ro an ninh trong C",
            Content = "Giải thích nguyên nhân xảy ra lỗi Buffer Overflow khi dùng hàm gets/strcpy và các giải pháp phòng chống chuẩn bảo mật.",
            SampleAnswer = "Buffer Overflow xảy ra khi chương trình ghi dữ liệu vượt quá kích thước vùng đệm đã cấp phát trên Stack, đè lên các biến liền kề hoặc địa chỉ trả về return address của hàm. Các hàm không kiểm tra giới hạn như gets, strcpy rất nguy hiểm, cần thay thế bằng fgets, strncpy hoặc snprintf.",
            KeyPoints = "[\"Nguyên nhân ghi đè vùng nhớ ngoài phạm vi trên Stack\",\"Rủi ro bảo mật kiểm soát con trỏ lệnh EIP/RIP\",\"Sử dụng hàm an toàn fgets và strncpy kiểm soát độ dài\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Hàm fgets nhận tham số nào để bảo đảm không bị tràn bộ nhớ đệm?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        // CSD201: 12 Questions (4 Easy, 4 Medium, 4 Hard)
        // Easy 1..4
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000521"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Phân tích độ phức tạp thời gian Big-O của tìm kiếm tuần tự và nhị phân",
            Content = "So sánh độ phức tạp thời gian trường hợp tốt nhất và xấu nhất của thuật toán Linear Search và Binary Search.",
            SampleAnswer = "Linear Search duyệt tuần tự từng phần tử nên có độ phức tạp trường hợp xấu nhất là O(n), không yêu cầu dữ liệu sắp xếp. Binary Search chia đôi không gian tìm kiếm mỗi bước nên đạt độ phức tạp O(log n), nhưng đòi hỏi mảng bắt buộc phải được sắp xếp từ trước.",
            KeyPoints = "[\"Linear Search đạt O(1) tốt nhất, O(n) xấu nhất\",\"Binary Search đạt O(log n) xấu nhất\",\"Điều kiện tiên quyết của Binary Search là mảng đã sắp xếp\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            HasFollowUp = true,
            FollowUpPrompt = "Nếu mảng chưa sắp xếp và chỉ cần tìm kiếm 1 lần, ta nên dùng Linear Search hay sắp xếp rồi Binary Search?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000522"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Nguyên lý hoạt động của Cấu trúc Dữ liệu Ngăn xếp (Stack)",
            Content = "Trình bày nguyên lý LIFO của Stack, các thao tác cơ bản push, pop, peek và độ phức tạp thời gian của chúng.",
            SampleAnswer = "Stack hoạt động theo nguyên lý Last In, First Out (phần tử thêm vào sau cùng sẽ được lấy ra đầu tiên). Các thao tác push (thêm vào đỉnh), pop (lấy ra khỏi đỉnh) và peek (xem phần tử đỉnh) đều có độ phức tạp thời gian O(1) tức thời.",
            KeyPoints = "[\"Nguyên lý vào sau ra trước (LIFO)\",\"Các hàm push, pop, peek đều đạt O(1)\",\"Ứng dụng trong quản lý gọi hàm Call Stack và Undo/Redo\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Remember,
            HasFollowUp = true,
            FollowUpPrompt = "Stack thường được ứng dụng để giải quyết bài toán kiểm tra dấu ngoặc hợp lệ như thế nào?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000523"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Nguyên lý hoạt động của Cấu trúc Hàng đợi (Queue)",
            Content = "Trình bày nguyên lý FIFO của Queue, các thao tác cơ bản enqueue, dequeue và so sánh với Stack.",
            SampleAnswer = "Queue hoạt động theo nguyên lý First In, First Out (phần tử vào trước sẽ được xử lý ra trước). Thao tác enqueue thêm vào cuối hàng đợi và dequeue lấy ra từ đầu hàng đợi với độ phức tạp O(1). Khác với Stack xử lý ở cùng một đầu đỉnh, Queue xử lý ở hai đầu đối diện.",
            KeyPoints = "[\"Nguyên lý vào trước ra trước (FIFO)\",\"Thao tác enqueue ở đuôi (tail) và dequeue ở đầu (head) đạt O(1)\",\"Ứng dụng trong hàng đợi in ấn và thuật toán duyệt đồ thị BFS\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Remember,
            HasFollowUp = true,
            FollowUpPrompt = "Cấu trúc Hàng đợi vòng (Circular Queue) giải quyết nhược điểm gì của Queue cài đặt bằng mảng tĩnh?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000524"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "So sánh Mảng tĩnh (Array) và Danh sách liên kết đơn (Singly Linked List)",
            Content = "Phân tích ưu nhược điểm về cấp phát bộ nhớ, truy xuất ngẫu nhiên và chi phí chèn/xóa phần tử giữa Array và Linked List.",
            SampleAnswer = "Mảng cấp phát liên tục trên bộ nhớ, cho phép truy xuất ngẫu nhiên O(1) theo chỉ số nhưng kích thước cố định và chèn/xóa giữa tốn O(n). Danh sách liên kết cấp phát động từng node phân tán, chèn/xóa tại vị trí đã biết chỉ tốn O(1), nhưng truy xuất tuần tự tốn O(n) và tốn thêm bộ nhớ cho con trỏ next.",
            KeyPoints = "[\"Mảng truy xuất ngẫu nhiên O(1), kích thước cố định\",\"Linked List cấp phát động linh hoạt, chèn xóa O(1) khi biết vị trí\",\"Chi phí bộ nhớ phụ trội của con trỏ liên kết\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            HasFollowUp = true,
            FollowUpPrompt = "Khi nào ta nên lựa chọn Danh sách liên kết thay vì ArrayList?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        // Medium 5..8
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000525"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Cây nhị phân tìm kiếm (Binary Search Tree - BST)",
            Content = "Định nghĩa tính chất của cây BST và phân tích các trường hợp duyệt cây In-order, Pre-order, Post-order.",
            SampleAnswer = "Cây BST thỏa mãn tính chất: mọi node con bên trái có giá trị nhỏ hơn node cha, và mọi node con bên phải có giá trị lớn hơn node cha. Phép duyệt In-order (Left-Root-Right) trên cây BST luôn trả về danh sách các khóa theo thứ tự tăng dần. Phép duyệt Pre-order thường dùng để sao chép cây.",
            KeyPoints = "[\"Tính chất khóa nhánh trái < cha < nhánh phải\",\"Duyệt In-order sinh dãy khóa tăng dần chuẩn xác\",\"Độ phức tạp tìm kiếm O(h) phụ thuộc chiều cao h\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Khi nào cây BST bị suy biến thành danh sách liên kết và độ phức tạp khi đó là bao nhiêu?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000526"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Thuật toán sắp xếp QuickSort và kỹ thuật phân hoạch Partition",
            Content = "Giải thích nguyên lý chia để trị của QuickSort, cách chọn chốt Pivot và độ phức tạp trung bình so với trường hợp xấu nhất.",
            SampleAnswer = "QuickSort chọn một phần tử làm chốt Pivot, phân hoạch mảng sao cho các phần tử nhỏ hơn chốt nằm bên trái và lớn hơn nằm bên phải, sau đó đệ quy sắp xếp 2 nửa. Độ phức tạp trung bình đạt O(n log n). Trường hợp xấu nhất xảy ra khi chốt luôn là min hoặc max khiến độ phức tạp giảm xuống O(n^2).",
            KeyPoints = "[\"Kỹ thuật phân hoạch quanh điểm chốt Pivot\",\"Độ phức tạp trung bình O(n log n)\",\"Trường hợp suy biến O(n^2) khi mảng đã sắp xếp và chọn chốt ở đầu\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Làm sao để hạn chế trường hợp xấu nhất O(n^2) trong QuickSort trong thực tế?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000527"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Bảng băm (Hash Table) và các kỹ thuật xử lý xung đột",
            Content = "Trình bày nguyên lý hàm băm Hash Function và so sánh phương pháp Chaining với Open Addressing (Linear Probing).",
            SampleAnswer = "Bảng băm ánh xạ khóa thành chỉ số mảng qua hàm băm để đạt tìm kiếm O(1). Khi 2 khóa băm ra cùng một chỉ số sẽ gây xung đột. Phương pháp Separate Chaining dùng danh sách liên kết tại mỗi ô bucket. Phương pháp Open Addressing (Linear Probing) dò tìm ô trống tiếp theo trong mảng.",
            KeyPoints = "[\"Ánh xạ khóa sang chỉ số bằng hàm băm O(1)\",\"Xử lý xung đột bằng Separate Chaining (danh sách liên kết)\",\"Xử lý xung đột bằng Open Addressing / Linear Probing\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Hệ số tải Load Factor trong Hash Table ảnh hưởng như thế nào đến hiệu năng?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000528"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Thuật toán tìm kiếm theo chiều rộng (BFS) trên đồ thị",
            Content = "Mô tả thuật toán BFS sử dụng Queue, cách đánh dấu đỉnh đã thăm và ứng dụng tìm đường đi ngắn nhất không trọng số.",
            SampleAnswer = "Thuật toán BFS duyệt các đỉnh theo từng tầng khoảng cách từ đỉnh nguồn. Thuật toán dùng một Queue lưu các đỉnh chờ duyệt và mảng visited đánh dấu tránh chu trình lặp vô hạn. Do duyệt theo từng lớp lân cận, BFS luôn tìm ra đường đi có số cạnh ít nhất trên đồ thị không trọng số.",
            KeyPoints = "[\"Sử dụng hàng đợi Queue và mảng boolean visited\",\"Duyệt đồ thị theo từng tầng mức khoảng cách\",\"Tìm đường đi ngắn nhất trên đồ thị không trọng số đạt O(V + E)\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Độ phức tạp không gian (Space Complexity) của BFS phụ thuộc vào yếu tố nào?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        // Hard 9..12
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000529"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Cây cân bằng AVL và các phép xoay tái cân bằng",
            Content = "Định nghĩa chỉ số cân bằng Balance Factor và giải thích 4 trường hợp xoay: LL, RR, LR, RL khi chèn node mới.",
            SampleAnswer = "Cây AVL là cây BST tự cân bằng, trong đó độ lệch chiều cao Balance Factor = Height(Left) - Height(Right) tại mọi node chỉ được thuộc {-1, 0, 1}. Khi bị mất cân bằng, ta thực hiện: xoay đơn Right Rotation cho trường hợp LL; xoay đơn Left Rotation cho RR; xoay kép Left-Right cho LR; xoay kép Right-Left cho RL để duy trì độ cao O(log n).",
            KeyPoints = "[\"Hệ số cân bằng Balance Factor chỉ được nhận giá trị -1, 0, 1\",\"Xoay đơn Right (LL) và Left (RR)\",\"Xoay kép Left-Right (LR) và Right-Left (RL) bảo toàn O(log n)\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "So sánh cây AVL với cây Đỏ-Đen (Red-Black Tree) về tần suất xoay và trường hợp ứng dụng.",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000052a"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Thuật toán tìm đường đi ngắn nhất Dijkstra",
            Content = "Trình bày thuật toán Dijkstra sử dụng Hàng đợi ưu tiên (Priority Queue/Min-Heap) và điều kiện áp dụng về trọng số cạnh.",
            SampleAnswer = "Thuật toán Dijkstra tìm đường đi ngắn nhất từ một đỉnh nguồn đến mọi đỉnh khác trên đồ thị có trọng số không âm. Thuật toán duy trì khoảng cách nhỏ nhất ước lượng qua Min-Heap, mỗi bước lấy đỉnh có khoảng cách nhỏ nhất và thực hiện cập nhật giãn cạnh (relaxation). Nếu đồ thị có trọng số âm, thuật toán có thể cho kết quả sai.",
            KeyPoints = "[\"Thuật toán tham lam kết hợp kỹ thuật giãn cạnh Relaxation\",\"Sử dụng Min-Heap đạt độ phức tạp O((V + E) log V)\",\"Bắt buộc trọng số cạnh không âm (non-negative weights)\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Tại sao thuật toán Dijkstra lại thất bại khi đồ thị có cạnh mang trọng số âm?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000052b"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Thuật toán xây dựng Cây khung nhỏ nhất Kruskal và Disjoint Set (DSU)",
            Content = "Giải thích nguyên lý của thuật toán Kruskal và cách cấu trúc dữ liệu Disjoint Set Union (Find/Union) kiểm tra chu trình.",
            SampleAnswer = "Kruskal sắp xếp toàn bộ các cạnh theo trọng số tăng dần, sau đó lần lượt thêm từng cạnh vào cây khung nếu cạnh đó không tạo thành chu trình với các cạnh đã chọn. Cấu trúc Disjoint Set Union (DSU) với kỹ thuật nén đường đi Path Compression và hợp nhất theo hạng Union by Rank kiểm tra tính liên thông trong thời gian gần như O(1).",
            KeyPoints = "[\"Sắp xếp cạnh tăng dần và tham lam chọn cạnh không tạo chu trình\",\"Cấu trúc DSU kiểm tra hai đỉnh cùng thành phần liên thông\",\"Kỹ thuật Path Compression và Union by Rank đạt độ phức tạp gần tuyến tính\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "So sánh thuật toán Kruskal với thuật toán Prim: trường hợp nào ta nên chọn Kruskal?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000052c"),
            CourseId = CourseCsd201Id,
            RubricId = RubricCsd201Id,
            Title = "Cấu trúc dữ liệu Max-Heap và thuật toán HeapSort",
            Content = "Định nghĩa cây Max-Heap dạng mảng và các bước của hàm Heapify để xây dựng Heap và sắp xếp mảng tại chỗ (in-place).",
            SampleAnswer = "Max-Heap là cây nhị phân gần hoàn chỉnh thỏa mãn giá trị node cha luôn lớn hơn hoặc bằng các node con, được biểu diễn trên mảng với con trái tại 2i+1 và con phải tại 2i+2. Hàm heapify chìm dần phần tử vi phạm xuống dưới. HeapSort xây dựng Max-Heap O(n), sau đó liên tục hoán đổi gốc max về cuối mảng và heapify lại, đạt O(n log n) tại chỗ.",
            KeyPoints = "[\"Biểu diễn cây nhị phân trên mảng 1 chiều không dùng con trỏ\",\"Thao tác heapify duy trì tính chất Max-Heap trong O(log n)\",\"HeapSort sắp xếp tại chỗ O(1) phụ trợ và O(n log n) mọi trường hợp\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "HeapSort có phải là thuật toán sắp xếp ổn định (Stable Sort) không và tại sao?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        // DBI202: 12 Questions (4 Easy, 4 Medium, 4 Hard)
        // Easy 1..4
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000621"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Khái niệm Khóa chính (Primary Key) và Khóa ngoại (Foreign Key)",
            Content = "Phân biệt vai trò của Khóa chính và Khóa ngoại trong mô hình cơ sở dữ liệu quan hệ.",
            SampleAnswer = "Khóa chính (Primary Key) là một hoặc một nhóm thuộc tính xác định duy nhất mỗi dòng bản ghi trong bảng, bắt buộc không được null (NOT NULL) và không trùng lặp (UNIQUE). Khóa ngoại (Foreign Key) là cột tham chiếu tới khóa chính của bảng khác để thiết lập mối quan hệ và bảo toàn toàn vẹn tham chiếu.",
            KeyPoints = "[\"Khóa chính định danh duy nhất bản ghi, không chứa NULL\",\"Khóa ngoại tham chiếu tới khóa chính bảng cha\",\"Ràng buộc toàn vẹn tham chiếu Referential Integrity\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Remember,
            HasFollowUp = true,
            FollowUpPrompt = "Một bảng có thể có nhiều Khóa ngoại không và có thể có nhiều Khóa chính không?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000622"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Phân biệt mệnh đề WHERE và HAVING trong SQL",
            Content = "Giải thích sự khác nhau về thời điểm lọc dữ liệu và điều kiện áp dụng giữa WHERE và HAVING.",
            SampleAnswer = "Mệnh đề WHERE lọc từng dòng dữ liệu riêng lẻ trước khi thực hiện nhóm dữ liệu bằng GROUP BY, và không được chứa các hàm tổng hợp như SUM, AVG, COUNT. Mệnh đề HAVING lọc các nhóm kết quả sau khi đã gom nhóm và thường đi kèm điều kiện trên các hàm tổng hợp.",
            KeyPoints = "[\"WHERE lọc từng dòng trước khi gom nhóm\",\"HAVING lọc các nhóm sau khi gom nhóm GROUP BY\",\"HAVING hỗ trợ các hàm tổng hợp COUNT, SUM, AVG\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            HasFollowUp = true,
            FollowUpPrompt = "Ta có thể dùng HAVING mà không có GROUP BY trong câu lệnh SQL được không?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000623"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "So sánh INNER JOIN và LEFT OUTER JOIN trong truy vấn SQL",
            Content = "Trình bày tập kết quả trả về khi kết nối 2 bảng bằng phép INNER JOIN và LEFT OUTER JOIN.",
            SampleAnswer = "Phép INNER JOIN chỉ trả về các dòng có giá trị khóa khớp nhau ở cả 2 bảng. Phép LEFT OUTER JOIN trả về toàn bộ các dòng từ bảng bên trái; đối với các dòng không tìm thấy bản ghi tương ứng ở bảng bên phải, các cột của bảng bên phải sẽ được điền giá trị NULL.",
            KeyPoints = "[\"INNER JOIN chỉ lấy phần giao giữa 2 bảng\",\"LEFT JOIN giữ lại 100% dòng của bảng trái và điền NULL cho bảng phải\",\"Trường hợp sử dụng thực tế khi tìm khách hàng chưa có đơn hàng\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            HasFollowUp = true,
            FollowUpPrompt = "Làm sao tìm các bản ghi ở bảng trái không có bản ghi nào ở bảng phải bằng LEFT JOIN?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000624"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Các thuộc tính ACID trong giao dịch Cơ sở dữ liệu (Transaction)",
            Content = "Giải thích ý nghĩa của 4 tính chất: Atomicity, Consistency, Isolation, Durability trong quản lý giao dịch.",
            SampleAnswer = "Tính nguyên tố (Atomicity): mọi thao tác trong giao dịch thành công toàn bộ hoặc bị hủy bỏ hoàn toàn (all-or-nothing). Tính nhất quán (Consistency): đưa DB từ trạng thái hợp lệ này sang trạng thái hợp lệ khác. Tính cô lập (Isolation): các giao dịch song song không nhìn thấy dữ liệu chưa commit. Tính bền vững (Durability): dữ liệu đã commit được lưu vĩnh viễn.",
            KeyPoints = "[\"Atomicity: tất cả hoặc không có gì (rollback khi lỗi)\",\"Consistency: tuân thủ mọi ràng buộc toàn vẹn\",\"Isolation: kiểm soát tương tranh độc lập\",\"Durability: bảo toàn dữ liệu trên đĩa sau sự cố crash\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Remember,
            HasFollowUp = true,
            FollowUpPrompt = "Trong bài toán chuyển tiền từ tài khoản A sang tài khoản B, tính Atomicity thể hiện như thế nào?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        // Medium 5..8
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000625"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Quy trình chuẩn hóa dữ liệu từ 1NF đến 3NF",
            Content = "Trình bày điều kiện để một lược đồ quan hệ đạt dạng chuẩn 1NF, 2NF và 3NF, giải thích khái niệm Phụ thuộc hàm.",
            SampleAnswer = "Dạng chuẩn 1NF đòi hỏi mọi giá trị tại các ô thuộc tính phải mang tính nguyên tử (atomic), không chứa mảng hay tập hợp lặp. Dạng chuẩn 2NF yêu cầu đạt 1NF và mọi thuộc tính không khóa phải phụ thuộc hàm đầy đủ vào toàn bộ khóa chính (loại bỏ phụ thuộc bộ phận). Dạng chuẩn 3NF yêu cầu đạt 2NF và không có phụ thuộc bắc cầu giữa các thuộc tính không khóa.",
            KeyPoints = "[\"1NF: Giá trị nguyên tử, không chứa danh sách lặp\",\"2NF: Loại bỏ phụ thuộc hàm từng phần vào khóa phức hợp\",\"3NF: Loại bỏ phụ thuộc hàm bắc cầu qua thuộc tính không khóa\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Lợi ích và đánh đổi (trade-off) khi đưa cơ sở dữ liệu về chuẩn 3NF so với Denormalization là gì?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000626"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Kỹ thuật Subquery tương quan (Correlated Subquery) trong SQL",
            Content = "Giải thích cơ chế hoạt động của Correlated Subquery so với Subquery độc lập và toán tử EXISTS.",
            SampleAnswer = "Subquery độc lập chỉ thực thi 1 lần duy nhất rồi trả kết quả cho câu truy vấn ngoài. Correlated Subquery tham chiếu trực tiếp đến cột của dòng hiện tại ở truy vấn ngoài, do đó phải thực thi lặp lại cho từng dòng của bảng ngoài. Toán tử EXISTS kết hợp với Correlated Subquery dừng ngay khi tìm thấy dòng đầu tiên thỏa mãn giúp tối ưu hiệu năng.",
            KeyPoints = "[\"Correlated Subquery phụ thuộc vào từng dòng của truy vấn ngoài\",\"Thực thi lặp lại cho từng dòng bản ghi\",\"Toán tử EXISTS tối ưu hơn IN khi kiểm tra sự tồn tại\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Tại sao nên ưu tiên dùng EXISTS thay vì IN khi bảng con có chứa giá trị NULL?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000627"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Chỉ mục B-Tree (Index) và nguyên tắc tối ưu hóa truy vấn",
            Content = "Giải thích cấu trúc chỉ mục B-Tree trong cơ sở dữ liệu và các trường hợp chỉ mục không được sử dụng (Index Invalidation).",
            SampleAnswer = "Chỉ mục B-Tree tổ chức dữ liệu dạng cây tự cân bằng cho phép tìm kiếm, quét khoảng trong thời gian O(log n). Chỉ mục sẽ bị vô hiệu hóa (phải dùng Full Table Scan) nếu truy vấn sử dụng hàm bọc quanh cột như WHERE UPPER(name) = 'ABC', dùng toán tử LIKE '%abc', hoặc so sánh khác kiểu dữ liệu ép kiểu ngầm định.",
            KeyPoints = "[\"Cấu trúc cây B-Tree cho phép tìm kiếm và duyệt khoảng O(log n)\",\"Index Invalidation khi dùng hàm trên cột hoặc LIKE '%prefix'\",\"Nguyên tắc ESR sắp xếp Equality, Sort, Range cho composite index\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Thế nào là Covering Index và tại sao nó lại cải thiện hiệu năng truy vấn vượt trội?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000628"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Các mức cô lập giao dịch (Transaction Isolation Levels) và hiện tượng tranh chấp",
            Content = "Phân tích 4 mức cô lập: Read Uncommitted, Read Committed, Repeatable Read, Serializable và các hiện tượng Dirty Read, Non-repeatable Read, Phantom Read.",
            SampleAnswer = "Read Uncommitted cho phép đọc dữ liệu chưa commit dẫn đến Dirty Read. Read Committed chống Dirty Read nhưng vẫn bị Non-repeatable Read (giá trị cùng một dòng thay đổi giữa 2 lần đọc). Repeatable Read khóa dòng chống thay đổi nhưng vẫn bị Phantom Read (xuất hiện thêm dòng mới). Serializable cô lập tuyệt đối nhưng chi phí hiệu năng cao nhất.",
            KeyPoints = "[\"4 mức cô lập chuẩn ANSI SQL\",\"Hiện tượng Dirty Read, Non-repeatable Read, Phantom Read\",\"Sự đánh đổi giữa tính toàn vẹn dữ liệu và thông lượng xử lý đồng thời\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            HasFollowUp = true,
            FollowUpPrompt = "Cơ chế MVCC (Multi-Version Concurrency Control) trong PostgreSQL giải quyết tranh chấp đọc-ghi như thế nào?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        // Hard 9..12
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000629"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Hàm cửa sổ (Window Functions) ROW_NUMBER, RANK, DENSE_RANK",
            Content = "So sánh sự khác nhau giữa ROW_NUMBER(), RANK() và DENSE_RANK() khi có các giá trị trùng nhau trong mệnh đề PARTITION BY và ORDER BY.",
            SampleAnswer = "Các hàm Window Functions tính toán trên một tập dòng liên quan mà không làm gộp các dòng lại như GROUP BY. Khi gặp các giá trị bằng nhau: ROW_NUMBER() đánh số thứ tự liên tiếp không bao giờ trùng; RANK() gán cùng thứ hạng nhưng nhảy cách bước ở số tiếp theo (ví dụ 1, 2, 2, 4); DENSE_RANK() gán cùng thứ hạng và không nhảy số (ví dụ 1, 2, 2, 3).",
            KeyPoints = "[\"Mệnh đề OVER (PARTITION BY ... ORDER BY ...)\",\"Sự khác biệt khi có giá trị đồng hạng giữa ROW_NUMBER, RANK, DENSE_RANK\",\"Ứng dụng trong phân trang nâng cao và tìm top N phần tử mỗi nhóm\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Làm sao kết hợp DENSE_RANK() với CTE để lấy sinh viên có điểm cao thứ 2 của mỗi lớp học?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000062a"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Biểu thức bảng chung đệ quy (Recursive Common Table Expression - CTE)",
            Content = "Giải thích cú pháp của Recursive CTE với phần Anchor member và Recursive member trong bài toán truy vấn cấu trúc phân cấp cây.",
            SampleAnswer = "Recursive CTE bao gồm phần định vị gốc Anchor Member và phần đệ quy Recursive Member kết nối với nhau qua toán tử UNION ALL. Phần đệ quy tự tham chiếu đến chính tên CTE để duyệt qua các cấp độ con cháu lồng nhau cho đến khi không còn bản ghi nào thỏa mãn, rất hiệu quả cho bài toán cây danh mục hoặc sơ đồ tổ chức.",
            KeyPoints = "[\"Phần neo ban đầu Anchor Member\",\"Phần tự tham chiếu Recursive Member qua UNION ALL\",\"Ứng dụng duyệt cấu trúc cây phân cấp và điều kiện dừng đệ quy\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Làm sao ngăn chặn vòng lặp vô hạn (Infinite Loop) khi dữ liệu trong bảng phân cấp bị tạo chu trình kín?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000062b"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Cơ chế khóa Deadlock và kỹ thuật giải quyết trong Hệ Quản trị CSDL",
            Content = "Giải thích nguyên nhân hình thành Deadlock giữa 2 giao dịch đồng thời và các phương pháp phòng chống Deadlock trong thiết kế ứng dụng.",
            SampleAnswer = "Deadlock xảy ra khi giao dịch T1 nắm giữ khóa trên tài nguyên R1 và chờ khóa tài nguyên R2, trong khi giao dịch T2 nắm giữ khóa trên R2 và chờ khóa trên R1, tạo thành chu trình chờ đợi lẫn nhau. Hệ thống DB tự phát hiện qua đồ thị chờ Wait-For Graph và rollback một giao dịch. Phòng ngừa bằng cách luôn truy cập tài nguyên theo thứ tự nhất quán trong mọi giao dịch.",
            KeyPoints = "[\"Điều kiện hình thành chu trình chờ khóa Deadlock\",\"Cơ chế phát hiện bằng Wait-For Graph và chọn nạn nhân Victim để rollback\",\"Biện pháp phòng ngừa: truy cập tài nguyên theo thứ tự khóa nhất quán\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Tại sao việc giữ transaction mở quá lâu lại làm tăng nguy cơ xảy ra Deadlock?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });
        questions.Add(new PracticeQuestion
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000062c"),
            CourseId = CourseDbi202Id,
            RubricId = RubricDbi202Id,
            Title = "Đọc và phân tích Kế hoạch thực thi truy vấn (Execution Plan / EXPLAIN ANALYZE)",
            Content = "Giải thích ý nghĩa của các nút Seq Scan, Index Scan, Index Only Scan, Nested Loop, Hash Join khi phân tích câu truy vấn.",
            SampleAnswer = "Lệnh EXPLAIN ANALYZE hiển thị kế hoạch thực thi thực tế của bộ tối ưu hóa chi phí Cost-Based Optimizer. Seq Scan biểu thị việc quét toàn bộ bảng; Index Scan dùng B-Tree để tìm con trỏ bản ghi rồi đọc heap; Index Only Scan chỉ đọc dữ liệu từ index mà không cần truy xuất bảng; Hash Join băm bảng nhỏ để kết nối bảng lớn; Nested Loop duyệt lồng phù hợp khi một bảng rất nhỏ.",
            KeyPoints = "[\"Phân biệt Seq Scan, Index Scan và Index Only Scan\",\"Các thuật toán kết nối bảng: Nested Loop, Hash Join, Merge Join\",\"Cách phát hiện điểm nghẽn chi phí I/O lớn nhất trong cây kế hoạch\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Analyze,
            HasFollowUp = true,
            FollowUpPrompt = "Khi nào bộ tối ưu quyết định dùng Seq Scan thay vì Index Scan ngay cả khi cột đã có Index?",
            IsActive = true,
            CreatedAt = SeedTimestampUtc
        });

        foreach (var q in questions)
        {
            if (!existingQuestions.Contains(q.Id))
            {
                context.PracticeQuestions.Add(q);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureAdditionalClassesAndEnrollmentsAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var existingClasses = (await context.Classes.Select(c => c.Id).ToListAsync(cancellationToken)).ToHashSet();
        var semester = await context.Semesters.FirstOrDefaultAsync(cancellationToken);
        var semesterId = semester?.Id ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

        var classes = new List<Class>
        {
            new()
            {
                Id = ClassPrf192Id,
                CourseId = CoursePrf192Id,
                Code = "SE1804-PRF",
                SemesterId = semesterId,
                LecturerId = LecturerUserId,
                MaxStudents = 40,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassCsd201Id,
                CourseId = CourseCsd201Id,
                Code = "SE1805-CSD",
                SemesterId = semesterId,
                LecturerId = LecturerUserId,
                MaxStudents = 40,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassDbi202Id,
                CourseId = CourseDbi202Id,
                Code = "SE1806-DBI",
                SemesterId = semesterId,
                LecturerId = LecturerUserId,
                MaxStudents = 40,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }
        };

        foreach (var cls in classes)
        {
            if (!existingClasses.Contains(cls.Id))
            {
                context.Classes.Add(cls);
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var existingEnrollments = (await context.ClassEnrollments.Select(e => e.Id).ToListAsync(cancellationToken)).ToHashSet();

        var enrollments = new List<ClassEnrollment>
        {
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000401"), ClassId = ClassPrf192Id, StudentId = StudentHoangId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" },
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000402"), ClassId = ClassPrf192Id, StudentId = StudentMaiId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" },
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000403"), ClassId = ClassPrf192Id, StudentId = StudentThanhId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" },

            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000411"), ClassId = ClassCsd201Id, StudentId = StudentHoangId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" },
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000412"), ClassId = ClassCsd201Id, StudentId = StudentMaiId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" },
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000413"), ClassId = ClassCsd201Id, StudentId = StudentThanhId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" },

            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000421"), ClassId = ClassDbi202Id, StudentId = StudentHoangId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" },
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000422"), ClassId = ClassDbi202Id, StudentId = StudentMaiId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" },
            new() { Id = Guid.Parse("00000000-0000-0000-0003-000000000423"), ClassId = ClassDbi202Id, StudentId = StudentThanhId, EnrolledAt = SeedTimestampUtc, Status = "enrolled" }
        };

        foreach (var enr in enrollments)
        {
            if (!existingEnrollments.Contains(enr.Id))
            {
                context.ClassEnrollments.Add(enr);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
