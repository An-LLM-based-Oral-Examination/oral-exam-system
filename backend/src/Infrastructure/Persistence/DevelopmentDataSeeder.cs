using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;

namespace OralExamination.Infrastructure.Persistence;

/// <summary>
/// Bộ khởi tạo dữ liệu mẫu phát triển (Idempotent Data Seeder) cho MF-01 (Luyện tập) và MF-02 (Thi thử).
/// Đảm bảo đủ 3 học phần chuyên ngành Kỹ thuật Phần mềm (PRN231, SWD392, SWR302),
/// mỗi học phần có đủ 12 câu hỏi PracticeQuestions (4 Easy, 4 Medium, 4 Hard), Rubric chuẩn 10.0đ,
/// ExamStructure active, ghi danh lớp học và lịch sử phiên Luyện tập / Thi thử đã chấm điểm.
/// </summary>
public static class DevelopmentDataSeeder
{
    public static readonly Guid SemesterFa26Id = Guid.Parse("00000000-0000-0000-0000-000000000007");

    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DepartmentHeadUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid LecturerUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid ProctorUserId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public static readonly Guid StudentHoangId = Guid.Parse("00000000-0000-0000-0000-000000000005");
    public static readonly Guid StudentMaiId = Guid.Parse("00000000-0000-0000-0000-000000000006");
    public static readonly Guid StudentThanhId = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    public static readonly Guid CoursePrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000008");
    public static readonly Guid CourseSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000009");
    public static readonly Guid CourseSwr302Id = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    public static readonly Guid RubricPrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000013");
    public static readonly Guid RubricSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid RubricSwr302Id = Guid.Parse("00000000-0000-0000-0000-000000000301");

    public static readonly Guid StructurePrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000018");
    public static readonly Guid StructureSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000205");
    public static readonly Guid StructureSwr302Id = Guid.Parse("00000000-0000-0000-0000-000000000305");

    public static readonly Guid ClassPrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public static readonly Guid ClassSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000206");
    public static readonly Guid ClassSwr302Id = Guid.Parse("00000000-0000-0000-0000-000000000306");

    public static readonly Guid ExamSetPrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000019");
    public static readonly Guid ExamSetSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000207");
    public static readonly Guid ExamSetSwr302Id = Guid.Parse("00000000-0000-0000-0000-000000000307");

    public static readonly Guid OfficialSessionPrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000022");
    public static readonly Guid OfficialShiftPrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000023");
    public static readonly Guid TicketHoangId = Guid.Parse("00000000-0000-0000-0000-000000000024");
    public static readonly Guid TicketMaiId = Guid.Parse("00000000-0000-0000-0000-000000000025");
    public static readonly Guid AppealHoangId = Guid.Parse("00000000-0000-0000-0000-000000000026");

    private static readonly DateTime SeedTimestampUtc = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    public static async Task SeedAsync(OralExamDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await EnsureSemesterAndUsersAsync(context, cancellationToken);
        await EnsureSystemConfigsAsync(context, cancellationToken);
        await EnsureCoursesAsync(context, cancellationToken);
        await EnsureRubricsAndCriteriaAsync(context, cancellationToken);
        await EnsureExamStructuresAndSetsAsync(context, cancellationToken);
        await EnsurePracticeQuestionsAsync(context, cancellationToken);
        await EnsureExamQuestionsAsync(context, cancellationToken);
        await EnsureClassesAndEnrollmentsAsync(context, cancellationToken);
        await EnsureStudentPracticeAndMockExamHistoryAsync(context, cancellationToken);
        await EnsureOfficialExamAuditEvidenceAsync(context, cancellationToken);
    }

    private static async Task EnsureSemesterAndUsersAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var semesterExists = await context.Semesters
            .AnyAsync(s => s.Id == SemesterFa26Id || s.Code == "FA26", cancellationToken);

        if (!semesterExists)
        {
            context.Semesters.Add(new Semester
            {
                Id = SemesterFa26Id,
                Code = "FA26",
                Name = "Fall 2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31),
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            });
        }

        var defaultUsers = new List<User>
        {
            new()
            {
                Id = AdminUserId,
                Email = "admin@fpt.edu.vn",
                FullName = "Nguyen Quang Thanh",
                Username = "admin",
                PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-Admin!", "admin"),
                MustChangePassword = false,
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = DepartmentHeadUserId,
                Email = "headse@fe.edu.vn",
                FullName = "Tran Van Truong",
                Username = "headse",
                PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-Head!", "headse"),
                MustChangePassword = false,
                Role = UserRole.DepartmentHead,
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = LecturerUserId,
                Email = "totnt@fe.edu.vn",
                FullName = "Nguyen Trong Tot",
                Username = "totnt",
                PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-Lecturer!", "totnt"),
                MustChangePassword = false,
                Role = UserRole.Lecturer,
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ProctorUserId,
                Email = "haind@fe.edu.vn",
                FullName = "Nguyen Dang Hai",
                Username = "haind",
                PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-Proctor!", "haind"),
                MustChangePassword = false,
                Role = UserRole.Proctor,
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StudentHoangId,
                Email = "hoanglvse170001@fpt.edu.vn",
                FullName = "Le Vu Hoang",
                StudentCode = "SE170001",
                Username = "hoalvse170001",
                PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-SE170001!", "SE170001"),
                MustChangePassword = false,
                Role = UserRole.Student,
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StudentMaiId,
                Email = "maittse170002@fpt.edu.vn",
                FullName = "Tran Thi Mai",
                StudentCode = "SE170002",
                Username = "maittse170002",
                PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-SE170002!", "SE170002"),
                MustChangePassword = false,
                Role = UserRole.Student,
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StudentThanhId,
                Email = "thanhnqse184236@fpt.edu.vn",
                FullName = "Nguyễn Quang Thành",
                StudentCode = "SE184236",
                Username = "thanhnqse184236",
                PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-SE184236!", "SE184236"),
                MustChangePassword = false,
                Role = UserRole.Student,
                IsActive = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            }
        };

        var existingUsers = await context.Users.ToListAsync(cancellationToken);

        foreach (var user in defaultUsers)
        {
            var existing = existingUsers.FirstOrDefault(u =>
                u.Id == user.Id || string.Equals(u.Email, user.Email, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                context.Users.Add(user);
            }
            else
            {
                existing.MustChangePassword = false;
                existing.IsActive = true;
                if (string.IsNullOrWhiteSpace(existing.Username))
                {
                    existing.Username = user.Username;
                }
                if (string.IsNullOrWhiteSpace(existing.PasswordHash))
                {
                    existing.PasswordHash = user.PasswordHash;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSystemConfigsAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var existingKeys = (await context.SystemConfigs
            .Select(c => c.Key)
            .ToListAsync(cancellationToken)).ToHashSet();

        var defaultConfigs = new List<SystemConfig>
        {
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000027"),
                Key = "MaxPracticeQuestionsPerSession",
                Value = "10",
                Description = "Số lượng câu hỏi luyện tập tối đa trong một phiên do Admin cấu hình",
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000028"),
                Key = "MinMixedPracticeQuestions",
                Value = "3",
                Description = "Số lượng câu hỏi luyện tập tối thiểu cho chế độ Dễ đến Khó do Admin cấu hình",
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000029"),
                Key = "MaxMixedPracticeQuestions",
                Value = "10",
                Description = "Số lượng câu hỏi luyện tập tối đa cho chế độ Dễ đến Khó do Admin cấu hình",
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000030"),
                Key = "TranscriptBufferSeconds",
                Value = "60",
                Description = "Thời gian đệm hiệu đính transcript mặc định (giây) do Admin cấu hình",
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000031"),
                Key = "MaxPracticeFollowUpQuestions",
                Value = "2",
                Description = "Số lượng câu hỏi follow-up luyện tập tối đa do Admin cấu hình (1-5 câu)",
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000032"),
                Key = "SessionInactivityTimeoutMinutes",
                Value = "5",
                Description = "Thời gian timeout không tương tác của phiên luyện tập (phút)",
                UpdatedAt = SeedTimestampUtc
            }
        };

        foreach (var cfg in defaultConfigs)
        {
            if (!existingKeys.Contains(cfg.Key))
            {
                context.SystemConfigs.Add(cfg);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureCoursesAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var semester = await context.Semesters
            .FirstOrDefaultAsync(s => s.Id == SemesterFa26Id || s.Code == "FA26", cancellationToken);
        var semesterId = semester?.Id ?? SemesterFa26Id;

        var targetCourses = new List<Course>
        {
            new()
            {
                Id = CoursePrn231Id,
                Code = "PRN231",
                Name = "Building Cross-Platform Back-End Applications with .NET",
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
                Id = CourseSwd392Id,
                Code = "SWD392",
                Name = "Software Architecture and Design",
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
                Id = CourseSwr302Id,
                Code = "SWR302",
                Name = "Software Requirements",
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

        var existingCourses = await context.Courses.ToListAsync(cancellationToken);

        foreach (var target in targetCourses)
        {
            var existing = existingCourses.FirstOrDefault(c =>
                c.Id == target.Id || string.Equals(c.Code, target.Code, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                context.Courses.Add(target);
            }
            else
            {
                existing.Name = target.Name;
                existing.Credits = target.Credits;
                existing.HasFollowUp = true;
                existing.TranscriptBufferSeconds = existing.TranscriptBufferSeconds >= 10 ? existing.TranscriptBufferSeconds : 60;
                existing.MaxFollowUpQuestions = existing.MaxFollowUpQuestions >= 1 ? existing.MaxFollowUpQuestions : 2;
                existing.MaxMockExamsPerDay = 3;
                existing.IsActive = true;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRubricsAndCriteriaAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var prnCourse = await context.Courses.FirstAsync(c => c.Code == "PRN231", cancellationToken);
        var swdCourse = await context.Courses.FirstAsync(c => c.Code == "SWD392", cancellationToken);
        var swrCourse = await context.Courses.FirstAsync(c => c.Code == "SWR302", cancellationToken);

        var rubrics = new List<Rubric>
        {
            new()
            {
                Id = RubricPrn231Id,
                CourseId = prnCourse.Id,
                Name = "Barem Đánh Giá Kiến Trúc .NET 8",
                Description = "Đánh giá năng lực xây dựng ứng dụng Back-End đa nền tảng với .NET 8 Clean Architecture, CQRS và EF Core",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricSwd392Id,
                CourseId = swdCourse.Id,
                Name = "Barem Đánh Giá Kiến Trúc & Thiết Kế Phần Mềm SWD392",
                Description = "Đánh giá năng lực phân tích kiến trúc, mẫu thiết kế (Design Patterns) và thuộc tính chất lượng hệ thống (Quality Attributes) theo chuẩn CLO môn SWD392",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricSwr302Id,
                CourseId = swrCourse.Id,
                Name = "Barem Đánh Giá Kỹ Nghệ Yêu Cầu Phần Mềm SWR302",
                Description = "Đánh giá kỹ năng thu thập, phân tích, đặc tả SRS (IEEE 830 / ISO 29148) và thẩm định yêu cầu phần mềm theo chuẩn CLO môn SWR302",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }
        };

        var existingRubricIds = (await context.Rubrics.Select(r => r.Id).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var rubric in rubrics)
        {
            if (!existingRubricIds.Contains(rubric.Id))
            {
                context.Rubrics.Add(rubric);
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var criteria = new List<RubricCriterion>
        {
            // PRN231 Criteria (Sum = 10.00m)
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000014"),
                RubricId = RubricPrn231Id,
                CriterionName = "Khái niệm & Ranh giới 4 tầng Clean Architecture",
                Description = "Hiểu rõ vai trò Domain, Application, Infrastructure, API. Domain không phụ thuộc tầng nào.",
                MaxScore = 3.00m,
                Weight = 0.30m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000015"),
                RubricId = RubricPrn231Id,
                CriterionName = "Nguyên lý Dependency Inversion & CQRS MediatR",
                Description = "Áp dụng tách biệt Command và Query, sử dụng Interface và Dependency Injection Container.",
                MaxScore = 4.00m,
                Weight = 0.40m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000016"),
                RubricId = RubricPrn231Id,
                CriterionName = "Xử lý Ngoại lệ & Kiến trúc chịu lỗi (Resilience)",
                Description = "Trình bày cơ chế Global Exception RFC 7807, Bounded Channel và xử lý retry Polly.",
                MaxScore = 3.00m,
                Weight = 0.30m,
                BloomLevel = BloomLevel.Analyze,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            },

            // SWD392 Criteria (Sum = 10.00m)
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000202"),
                RubricId = RubricSwd392Id,
                CriterionName = "Nguyên lý Thiết kế SOLID & Mô hình Kiến trúc Cốt lõi",
                Description = "Giải thích chính xác bản chất SOLID, Clean Architecture, Hexagonal và phân tách trách nhiệm các tầng.",
                MaxScore = 3.00m,
                Weight = 0.30m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000203"),
                RubricId = RubricSwd392Id,
                CriterionName = "Vận dụng Design Patterns, DDD & Kiến trúc Phân tán",
                Description = "Áp dụng đúng mẫu thiết kế GoF, Bounded Context, Aggregate Root, CQRS, Event-Driven và Caching vào bài toán thực tế.",
                MaxScore = 4.00m,
                Weight = 0.40m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000204"),
                RubricId = RubricSwd392Id,
                CriterionName = "Phân tích Đánh đổi Kiến trúc (Trade-offs) & Thuộc tính Chất lượng",
                Description = "Phân tích sâu định lý CAP, Saga Pattern, Resilience (Circuit Breaker, Bulkhead) và bảo mật Zero-Trust.",
                MaxScore = 3.00m,
                Weight = 0.30m,
                BloomLevel = BloomLevel.Analyze,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            },

            // SWR302 Criteria (Sum = 10.00m)
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000302"),
                RubricId = RubricSwr302Id,
                CriterionName = "Khái niệm Cấp độ Yêu cầu & Chuẩn Tài liệu SRS",
                Description = "Phân biệt rõ Business/User/System Requirements, Functional vs Non-Functional và cấu trúc chuẩn IEEE 830 / ISO 29148.",
                MaxScore = 3.00m,
                Weight = 0.30m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000303"),
                RubricId = RubricSwr302Id,
                CriterionName = "Kỹ thuật Thu thập (Elicitation) & Đặc tả Use Case / User Story",
                Description = "Áp dụng kỹ thuật phỏng vấn, workshop, viết Use Case đầy đủ luồng chính/ngoại lệ, User Story chuẩn INVEST và Given-When-Then.",
                MaxScore = 4.00m,
                Weight = 0.40m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000304"),
                RubricId = RubricSwr302Id,
                CriterionName = "Truy vết Yêu cầu (RTM), Quản trị Thay đổi & Thẩm định Chất lượng",
                Description = "Phân tích ma trận RTM 2 chiều, kiểm soát Scope Creep qua CCB, phát hiện yêu cầu mơ hồ/mâu thuẫn và định lượng NFR.",
                MaxScore = 3.00m,
                Weight = 0.30m,
                BloomLevel = BloomLevel.Analyze,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            }
        };

        var existingCriterionIds = (await context.RubricCriteria.Select(c => c.Id).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var criterion in criteria)
        {
            if (!existingCriterionIds.Contains(criterion.Id))
            {
                context.RubricCriteria.Add(criterion);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureExamStructuresAndSetsAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var prnCourse = await context.Courses.FirstAsync(c => c.Code == "PRN231", cancellationToken);
        var swdCourse = await context.Courses.FirstAsync(c => c.Code == "SWD392", cancellationToken);
        var swrCourse = await context.Courses.FirstAsync(c => c.Code == "SWR302", cancellationToken);

        var creatorUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == LecturerUserId || u.Role == UserRole.Lecturer || u.Role == UserRole.DepartmentHead, cancellationToken);
        var creatorId = creatorUser?.Id ?? LecturerUserId;

        var structures = new List<ExamStructure>
        {
            new()
            {
                Id = StructurePrn231Id,
                CourseId = prnCourse.Id,
                CreatedBy = creatorId,
                Name = "Cấu trúc Đề thi Vấn đáp Cuối kỳ PRN231 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 30,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureSwd392Id,
                CourseId = swdCourse.Id,
                CreatedBy = creatorId,
                Name = "Cấu trúc Đề thi Vấn đáp SWD392 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 25,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureSwr302Id,
                CourseId = swrCourse.Id,
                CreatedBy = creatorId,
                Name = "Cấu trúc Đề thi Vấn đáp SWR302 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 25,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }
        };

        var existingStructures = await context.ExamStructures.ToListAsync(cancellationToken);
        foreach (var structure in structures)
        {
            var existing = existingStructures.FirstOrDefault(s => s.Id == structure.Id || s.CourseId == structure.CourseId);
            if (existing == null)
            {
                context.ExamStructures.Add(structure);
            }
            else
            {
                existing.IsActive = true;
                existing.TotalQuestions = structure.TotalQuestions;
                existing.DurationMinutes = structure.DurationMinutes;
                existing.BloomDistribution = structure.BloomDistribution;
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var actualPrnStruct = await context.ExamStructures.FirstAsync(s => s.CourseId == prnCourse.Id && s.IsActive, cancellationToken);
        var actualSwdStruct = await context.ExamStructures.FirstAsync(s => s.CourseId == swdCourse.Id && s.IsActive, cancellationToken);
        var actualSwrStruct = await context.ExamStructures.FirstAsync(s => s.CourseId == swrCourse.Id && s.IsActive, cancellationToken);

        var examSets = new List<ExamSet>
        {
            new()
            {
                Id = ExamSetPrn231Id,
                StructureId = actualPrnStruct.Id,
                CourseId = prnCourse.Id,
                SetCode = "PRN231-SET01",
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetSwd392Id,
                StructureId = actualSwdStruct.Id,
                CourseId = swdCourse.Id,
                SetCode = "SWD392-SET01",
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetSwr302Id,
                StructureId = actualSwrStruct.Id,
                CourseId = swrCourse.Id,
                SetCode = "SWR302-SET01",
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            }
        };

        var existingSets = await context.ExamSets.ToListAsync(cancellationToken);
        foreach (var set in examSets)
        {
            if (!existingSets.Any(s => s.Id == set.Id || s.SetCode == set.SetCode))
            {
                context.ExamSets.Add(set);
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsurePracticeQuestionsAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var prnCourse = await context.Courses.FirstAsync(c => c.Code == "PRN231", cancellationToken);
        var swdCourse = await context.Courses.FirstAsync(c => c.Code == "SWD392", cancellationToken);
        var swrCourse = await context.Courses.FirstAsync(c => c.Code == "SWR302", cancellationToken);

        var allQuestions = new List<PracticeQuestion>
        {
            // =========================================================================
            // 1. MÔN PRN231 — 12 CÂU HỎI (4 Easy / Understand, 4 Medium / Apply, 4 Hard / Analyze)
            // =========================================================================
            // 6 câu gốc từ Migration (đảm bảo luôn tồn tại cả trên InMemory lẫn Relational DB)
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000101"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Vai trò tầng Domain trong Clean Architecture",
                Content = "Tầng Domain trong Clean Architecture chứa những thành phần nào và tại sao không được tham chiếu tới các tầng khác?",
                SampleAnswer = "Tầng Domain chứa Entities, Value Objects, Domain Events và Enums cốt lõi. Không phụ thuộc vào framework hay database để bảo toàn quy tắc nghiệp vụ thuần khiết và dễ kiểm thử.",
                KeyPoints = "[\"Chứa Entities, Value Objects và quy tắc nghiệp vụ cốt lõi\",\"Không phụ thuộc Infrastructure hay UI\",\"Đảm bảo tính độc lập với công nghệ bên ngoài\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Làm thế nào để tầng Domain hoặc Application giao tiếp với dịch vụ bên ngoài mà không phá vỡ tính độc lập?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000102"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Phân biệt Command và Query trong mẫu CQRS",
                Content = "Hãy trình bày sự khác biệt giữa Command và Query khi triển khai mẫu CQRS với thư viện MediatR trong .NET 8.",
                SampleAnswer = "Command dùng để thay đổi trạng thái dữ liệu hệ thống (Create, Update, Delete), trong khi Query chỉ dùng để truy vấn đọc dữ liệu và tuyệt đối không làm biến đổi trạng thái.",
                KeyPoints = "[\"Command thay đổi trạng thái dữ liệu\",\"Query chỉ đọc dữ liệu (Side-Effect Free)\",\"Tách biệt mô hình đọc và ghi qua MediatR\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Khi nào nên sử dụng AsNoTracking() trong các Query Handler của EF Core?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000106"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Vòng đời dịch vụ Dependency Injection (Transient, Scoped, Singleton)",
                Content = "Giải thích sự khác biệt giữa ba vòng đời dịch vụ Transient, Scoped và Singleton trong ASP.NET Core .NET 8. Tại sao DbContext mặc định được đăng ký là Scoped?",
                SampleAnswer = "Transient tạo instance mới mỗi lần được yêu cầu; Scoped tạo một instance duy nhất cho mỗi HTTP request; Singleton tạo một instance duy nhất trong suốt vòng đời ứng dụng. DbContext dùng Scoped để chia sẻ Unit of Work trong cùng một HTTP request và tránh lỗi Captive Dependency hoặc xung đột luồng (Thread-safety).",
                KeyPoints = "[\"Transient tạo mới mỗi lần inject\",\"Scoped dùng chung trong 1 HTTP Request\",\"Singleton sống suốt vòng đời ứng dụng\",\"DbContext không thread-safe nên bắt buộc dùng Scoped\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Điều gì sẽ xảy ra nếu bạn inject một dịch vụ Scoped như DbContext vào bên trong một dịch vụ Singleton?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000107"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Chuẩn hóa phản hồi lỗi HTTP với ProblemDetails (RFC 7807)",
                Content = "Trình bày cấu trúc chuẩn RFC 7807 ProblemDetails trong ASP.NET Core Web API và lợi ích của việc dùng GlobalExceptionMiddleware thay vì try-catch rời rạc ở từng Controller.",
                SampleAnswer = "Chuẩn RFC 7807 ProblemDetails định nghĩa cấu trúc JSON chuẩn cho lỗi HTTP gồm các trường type, title, status, detail và instance. Sử dụng GlobalExceptionMiddleware giúp tập trung hóa xử lý ngoại lệ, ẩn stack trace nhạy cảm ở môi trường Production và đảm bảo mọi API trả về định dạng lỗi nhất quán cho Frontend.",
                KeyPoints = "[\"Cấu trúc gồm type, title, status, detail, instance\",\"Tập trung hóa xử lý lỗi qua GlobalExceptionMiddleware\",\"Không rò rỉ Stack Trace nội bộ ra môi trường Production\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Làm thế nào để phân biệt lỗi Validation (HTTP 400/422) và lỗi hệ thống (HTTP 500) trong GlobalExceptionMiddleware?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000017"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Nguyên lý Dependency Inversion",
                Content = "Hãy giải thích nguyên lý Dependency Inversion trong Clean Architecture .NET 8.",
                SampleAnswer = "Module cấp cao không phụ thuộc trực tiếp vào module cấp thấp mà cả hai cùng phụ thuộc vào abstraction. Tầng Application khai báo các interface như IApplicationDbContext, còn tầng Infrastructure hiện thực chi tiết và đăng ký qua DI Container.",
                KeyPoints = "[\"Module cấp cao không phụ thuộc module cấp thấp\",\"Cả hai phụ thuộc vào Abstraction/Interface\",\"Infrastructure hiện thực Interface của Application\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Nếu muốn thay đổi cơ sở dữ liệu từ PostgreSQL sang SQL Server, bạn cần thay đổi những tầng nào và tại sao?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000103"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Cơ chế Pipeline Behavior với FluentValidation",
                Content = "Cách thức hoạt động của IPipelineBehavior trong MediatR kết hợp với FluentValidation để kiểm tra dữ liệu đầu vào trước khi vào Handler?",
                SampleAnswer = "ValidationBehavior triển khai IPipelineBehavior chặn mọi request trước khi tới Handler, tự động kích hoạt toàn bộ IValidator<TRequest> đã đăng ký trong DI Container và trả lỗi hoặc ném ValidationException nếu vi phạm quy tắc nghiệp vụ.",
                KeyPoints = "[\"Chặn request trước khi vào Handler\",\"Chạy tất cả IValidator<TRequest> tự động\",\"Tách biệt hoàn toàn logic kiểm tra dữ liệu khỏi logic nghiệp vụ\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Ưu điểm của việc đặt validation ở tầng Application thay vì chỉ đặt DataAnnotations ở Controller là gì?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000108"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Xác thực JWT Bearer và Phân quyền Role-Based Authorization (RBAC)",
                Content = "Hãy trình bày quy trình xác thực JWT Bearer kết hợp Refresh Token Rotation và kiểm soát truy cập theo vai trò (RBAC) trong ASP.NET Core .NET 8.",
                SampleAnswer = "Khi đăng nhập thành công, hệ thống cấp Access Token ngắn hạn (chứa các Claims như sub, email, role) và Refresh Token lưu hash trong DB. Middleware UseAuthentication giải mã và xác thực chữ ký JWT, sau đó UseAuthorization kiểm tra attribute [Authorize(Roles = ...)] trước khi cho phép thực thi Controller.",
                KeyPoints = "[\"Access Token chứa Claims định danh và Role\",\"Refresh Token Rotation chống đánh cắp phiên\",\"Thứ tự chuẩn UseAuthentication trước UseAuthorization\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Tại sao chúng ta chỉ lưu mã băm (TokenHash) của Refresh Token trong cơ sở dữ liệu thay vì lưu chuỗi gốc?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000109"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Giao tiếp thời gian thực với SignalR Typed Hubs trong .NET 8",
                Content = "Trình bày cách thiết kế SignalR Hub trong ASP.NET Core để đẩy kết quả chấm điểm AI từ BackgroundService về đúng trình duyệt của sinh viên theo thời gian thực.",
                SampleAnswer = "Sử dụng SignalR Hub kết hợp Group theo SessionId (Clients.Group(sessionId)). Khi sinh viên vào phiên luyện tập, client gọi JoinSessionGroup để tham gia nhóm. Khi BackgroundService chấm điểm xong, nó dùng IHubContext<PracticeHub> phát sự kiện ReceiveGradingResult tới đúng Group của phiên đó.",
                KeyPoints = "[\"Quản lý kết nối theo Group SessionId\",\"Inject IHubContext<PracticeHub> vào BackgroundService\",\"Hỗ trợ tự động kết nối lại (Automatic Reconnect) phía Client\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Tại sao trong BackgroundService ta phải inject IHubContext<PracticeHub> thay vì inject trực tiếp lớp PracticeHub?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000104"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Thiết kế hàng đợi BoundedChannel và xử lý Dead-Letter Queue",
                Content = "Phân tích cách sử dụng BoundedChannel kết hợp BackgroundService và Polly Retry để xử lý chấm điểm bất đồng bộ chịu tải cao.",
                SampleAnswer = "BoundedChannel giới hạn số lượng slot trong RAM (ví dụ 1000 slots) để áp dụng backpressure (FullMode.Wait). BackgroundService đọc tuần tự và dùng Polly retry lũy thừa (2s, 4s, 8s), nếu vẫn lỗi thì lưu vào bảng dead_letter_queues để đảm bảo Zero Data Loss.",
                KeyPoints = "[\"BoundedChannel chống tràn RAM với Backpressure\",\"BackgroundService xử lý bất đồng bộ\",\"Polly Retry và DLQ bảo toàn 100% dữ liệu\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Tại sao phải tạo IServiceScope mới bên trong BackgroundService mỗi khi xử lý một item từ hàng đợi?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000105"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Tối ưu hóa truy vấn EF Core 8 và phòng chống lỗi N+1 Query",
                Content = "Phân tích nguyên nhân gây ra lỗi N+1 Query trong Entity Framework Core và các giải pháp khắc phục hiệu quả.",
                SampleAnswer = "Lỗi N+1 xảy ra khi ứng dụng thực hiện 1 câu truy vấn lấy danh sách thực thể cha rồi lặp qua N phần tử để chạy thêm N câu truy vấn lấy thực thể con. Khắc phục bằng Eager Loading (Include/ThenInclude), dùng AsSplitQuery tránh Cartesian Explosion hoặc dùng Select Projection chỉ lấy đúng các cột DTO cần thiết.",
                KeyPoints = "[\"Nguyên nhân do lazy query bên trong vòng lặp\",\"Dùng Include/ThenInclude kèm AsSplitQuery\",\"Dùng Select Projection và AsNoTracking cho truy vấn đọc\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Khi nào việc dùng một câu lệnh Include duy nhất (Single Query) lại gây ra hiện tượng bùng nổ tích Đề-các (Cartesian Explosion) và cách xử lý là gì?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000110"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Cơ chế SaveChangesInterceptor và Khóa điểm Một chiều (One-Way Lock)",
                Content = "Phân tích cách sử dụng SaveChangesInterceptor trong EF Core 8 để cưỡng chế quy tắc bất biến dữ liệu (One-Way Lock) chặn mọi thao tác sửa/xóa điểm sau khi đã công bố.",
                SampleAnswer = "SaveChangesInterceptor can thiệp trực tiếp vào đường ống SavingChanges/SavingChangesAsync của DbContext trước khi sinh lệnh SQL. Interceptor duyệt ChangeTracker.Entries<ILockableEntity>(), nếu phát hiện thực thể đang ở trạng thái Modified hoặc Deleted mà cờ IsLocked == true trong cơ sở dữ liệu (ngoài luồng phúc khảo hợp lệ), nó lập tức ném ngoại lệ bảo mật để hủy giao dịch và trả về HTTP 403 Forbidden.",
                KeyPoints = "[\"Can thiệp tại tầng DbContext qua SaveChangesInterceptor\",\"Kiểm tra trạng thái gốc trong ChangeTracker\",\"Chặn tuyệt đối UPDATE/DELETE trái phép ở cấp ORM\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Làm thế nào để mở ngoại lệ an toàn cho luồng Giảng viên chấm lại đơn phúc khảo (AppealRequest) mà không làm suy yếu OneWayLockInterceptor?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000111"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Chiến lược Persist First < 100ms và Quản trị Giao dịch Chịu tải Cao",
                Content = "Phân tích kiến trúc Persist First (< 100ms) khi tiếp nhận bài nộp của hàng trăm sinh viên cùng lúc trong phòng thi và lý do không gọi trực tiếp API LLM đồng bộ trong HTTP Request của kỳ thi chính thức.",
                SampleAnswer = "Gọi trực tiếp API LLM mất từ 2 đến 10 giây, dễ gây nghẽn Thread Pool, timeout kết nối và làm mất bài thi nếu rớt mạng hoặc vướng giới hạn Rate Limit HTTP 429. Chiến lược Persist First ghi ngay bản ghi bài nộp trạng thái SUBMITTED/pending vào PostgreSQL trong dưới 100ms, trả phản hồi an toàn cho client ngay lập tức, sau đó đẩy tác vụ vào BoundedChannel để Worker chấm ngầm có kiểm soát.",
                KeyPoints = "[\"Ghi nhận dữ liệu xuống đĩa cứng PostgreSQL dưới 100ms\",\"Giải phóng ngay HTTP Thread tránh nghẽn cổng API\",\"Cách ly hoàn toàn độ trễ và lỗi mạng của LLM bên thứ ba\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Nếu tiến trình Web API bị khởi động lại đột ngột khi BoundedChannel trong RAM vẫn còn bài chưa chấm, làm sao để hệ thống khôi phục và chấm tiếp các bài đó?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // =========================================================================
            // 2. MÔN SWD392 — 12 CÂU HỎI (4 Easy / Understand, 4 Medium / Apply, 4 Hard / Analyze)
            // =========================================================================
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000211"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Nguyên lý SOLID trong Thiết kế Hướng đối tượng (SRP & OCP)",
                Content = "Hãy giải thích ý nghĩa của nguyên lý Single Responsibility Principle (SRP) và Open-Closed Principle (OCP) trong bộ nguyên lý SOLID kèm ví dụ minh họa.",
                SampleAnswer = "Single Responsibility Principle khẳng định mỗi lớp chỉ nên có một lý do duy nhất để thay đổi (đảm nhận một trách nhiệm cốt lõi). Open-Closed Principle yêu cầu thực thể phần mềm mở cho việc mở rộng hành vi mới thông qua kế thừa hoặc interface nhưng đóng với việc chỉnh sửa trực tiếp mã nguồn đã ổn định.",
                KeyPoints = "[\"SRP: Mỗi class chỉ có một lý do duy nhất để thay đổi\",\"OCP: Mở để mở rộng (Extension), đóng để sửa đổi (Modification)\",\"Giảm sự phụ thuộc chéo và rủi ro lỗi hồi quy (Regression)\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Làm thế nào để áp dụng mẫu Strategy Pattern nhằm tuân thủ nguyên lý Open-Closed Principle khi tính phí vận chuyển?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000212"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "So sánh Kiến trúc Monolithic (Nguyên khối) và Microservices (Vi dịch vụ)",
                Content = "Trình bày ưu điểm, nhược điểm của kiến trúc Monolithic so với Microservices. Khi nào một dự án khởi nghiệp nên chọn Modular Monolith thay vì Microservices?",
                SampleAnswer = "Kiến trúc Monolithic triển khai toàn bộ chức năng trong một tiến trình và dùng chung CSDL, giúp phát triển nhanh, dễ debug và đảm bảo giao dịch ACID. Microservices tách hệ thống thành các dịch vụ độc lập theo nghiệp vụ nhưng làm tăng độ phức tạp vận hành, mạng và dữ liệu phân tán. Dự án mới nên chọn Modular Monolith khi ranh giới miền chưa ổn định và quy mô đội ngũ còn nhỏ.",
                KeyPoints = "[\"Monolith đơn giản hóa triển khai và giao dịch ACID\",\"Microservices mở rộng độc lập nhưng tốn chi phí vận hành phân tán\",\"Modular Monolith cân bằng giữa ranh giới module sạch và vận hành gọn nhẹ\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Dấu hiệu kỹ thuật nào cho thấy một module trong Modular Monolith đã đến lúc cần tách ra thành một Microservice riêng?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000213"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Quy tắc Phụ thuộc (Dependency Rule) trong Clean & Hexagonal Architecture",
                Content = "Giải thích quy tắc phụ thuộc đồng tâm (Dependency Rule) trong Clean Architecture và khái niệm Ports & Adapters trong Hexagonal Architecture.",
                SampleAnswer = "Trong Clean Architecture, các mũi tên phụ thuộc mã nguồn chỉ được phép hướng từ vòng ngoài (API, Infrastructure) vào vòng trong (Application, Domain). Trong Hexagonal Architecture, lõi nghiệp vụ giao tiếp với thế giới bên ngoài thông qua các Ports (Interfaces) và các Adapters (cơ sở dữ liệu, REST controller, bên thứ ba) cắm vào các Ports đó.",
                KeyPoints = "[\"Phụ thuộc luôn hướng về tâm (Domain Layer)\",\"Ports là các Interface định nghĩa hợp đồng giao tiếp\",\"Adapters là phần hiện thực cụ thể gắn với công nghệ ngoại vi\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Việc tuân thủ chặt chẽ Dependency Rule mang lại lợi ích gì khi viết Unit Test cho tầng Application?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000214"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Vai trò của API Gateway và mẫu thiết kế Backend for Frontend (BFF)",
                Content = "Trình bày vai trò của API Gateway trong hệ thống phân tán và giải thích tại sao mẫu Backend for Frontend (BFF) lại hữu ích khi hệ thống có cả ứng dụng Web và Mobile.",
                SampleAnswer = "API Gateway đóng vai trò cổng vào duy nhất (Single Entry Point) xử lý định tuyến, xác thực tập trung, giới hạn lưu lượng (Rate Limiting) và SSL Termination. Mẫu BFF tạo các tầng cổng API riêng biệt tối ưu hóa định dạng và dung lượng dữ liệu trả về cho từng loại thiết bị (Web Desktop so với Mobile băng thông thấp).",
                KeyPoints = "[\"API Gateway tập trung hóa Routing, Auth và Rate Limiting\",\"BFF tùy biến payload riêng cho Web, Mobile hoặc Kiosk\",\"Giảm số lượng vòng lặp gọi mạng (Chattiness) từ phía Client\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Làm thế nào để tránh việc API Gateway trở thành điểm nghẽn cổ chai (Single Point of Failure) của toàn hệ thống?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000215"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Vận dụng nhóm mẫu thiết kế GoF: Factory Method, Strategy và Observer",
                Content = "Hãy phân tích tình huống thực tế và cách kết hợp các mẫu thiết kế Factory Method, Strategy và Observer trong một hệ thống thương mại điện tử hoặc chấm thi tự động.",
                SampleAnswer = "Trong hệ thống chấm thi tự động, Factory Method dùng để khởi tạo bộ chấm điểm phù hợp theo loại câu hỏi; Strategy Pattern dùng để hoán đổi linh hoạt thuật toán chấm điểm hoặc mô hình LLM (Gemini Flash hay Pro); còn Observer Pattern (hoặc MediatR Notification) dùng để phát sự kiện cập nhật bảng điểm thời gian thực và ghi Audit Log sau khi chấm xong.",
                KeyPoints = "[\"Factory Method tách biệt việc khởi tạo đối tượng phức tạp\",\"Strategy đóng gói các thuật toán có thể hoán đổi lúc runtime\",\"Observer giải phóng sự phụ thuộc cứng khi thông báo sự kiện 1-nhiều\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Sự khác biệt cốt lõi giữa Factory Method Pattern và Abstract Factory Pattern là gì?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000216"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Thiết kế Domain-Driven Design (DDD): Bounded Context & Aggregate Root",
                Content = "Trình bày khái niệm Bounded Context, Ubiquitous Language và quy tắc thiết kế Aggregate Root trong Domain-Driven Design (DDD).",
                SampleAnswer = "Bounded Context xác định ranh giới ngữ cảnh rõ ràng nơi một mô hình nghiệp vụ và ngôn ngữ chung (Ubiquitous Language) có ý nghĩa thống nhất. Aggregate Root là thực thể gốc đại diện cho một cụm đối tượng nghiệp vụ, chịu trách nhiệm bảo vệ toàn bộ các bất biến (Invariants) và mọi thao tác thay đổi dữ liệu bên trong cụm phải đi qua Aggregate Root.",
                KeyPoints = "[\"Bounded Context phân tách ranh giới mô hình nghiệp vụ\",\"Ubiquitous Language đồng nhất thuật ngữ giữa Dev và Domain Expert\",\"Aggregate Root kiểm soát tính toàn vẹn giao dịch và bất biến nội tại\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Tại sao các Aggregate khác nhau chỉ nên tham chiếu lẫn nhau thông qua định danh (ID) thay vì giữ tham chiếu đối tượng trực tiếp?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000217"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Triển khai mẫu CQRS kết hợp Event-Driven Architecture",
                Content = "Giải thích cách kết hợp mẫu CQRS và kiến trúc hướng sự kiện (Event-Driven Architecture) để tối ưu hóa hệ thống có tỷ lệ đọc lớn gấp nhiều lần tỷ lệ ghi.",
                SampleAnswer = "Mẫu CQRS tách biệt Write Model (chuẩn hóa 3NF để đảm bảo toàn vẹn nghiệp vụ khi xử lý Command) và Read Model (tối ưu hóa cho truy vấn nhanh). Khi Write Model cập nhật dữ liệu thành công, nó phát sinh Domain/Integration Event qua Message Broker để cập nhật bất đồng bộ sang Read Database hoặc bộ nhớ đệm mà không làm chậm luồng ghi.",
                KeyPoints = "[\"Tách biệt Write Model (3NF) và Read Model (Denormalized/Projection)\",\"Đồng bộ hóa qua Domain Events / Integration Events\",\"Mở rộng quy mô (Scale-out) tầng đọc và tầng ghi độc lập\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Làm thế nào để xử lý trải nghiệm người dùng khi Read Model chưa kịp đồng bộ ngay lập tức sau khi người dùng vừa bấm lưu (Eventual Consistency)?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000218"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Chiến lược Caching phân tán và phòng chống sự cố Cache Stampede",
                Content = "So sánh chiến lược Cache-Aside và Write-Through khi sử dụng bộ nhớ đệm phân tán (Redis). Làm thế nào để phòng chống hiện tượng Cache Stampede khi một khóa hot vừa hết hạn?",
                SampleAnswer = "Với Cache-Aside, ứng dụng kiểm tra Cache trước, nếu Cache Miss mới đọc từ DB rồi nạp lại vào Cache; với Write-Through, mọi thao tác ghi đều cập nhật đồng bộ qua Cache và DB. Để chống Cache Stampede khi hàng nghìn request cùng bị Cache Miss một lúc, ta áp dụng cơ chế Distributed Lock (Mutex/Singleflight), làm mới sớm trước khi hết hạn (Probabilistic Early Expiration) hoặc thêm độ lệch ngẫu nhiên vào TTL (Jitter).",
                KeyPoints = "[\"Cache-Aside tối ưu cho dữ liệu đọc nhiều\",\"Write-Through đảm bảo dữ liệu cache luôn mới nhưng tăng độ trễ ghi\",\"Chống Cache Stampede bằng Semaphore/Lock, Early Refresh và TTL Jitter\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Hiện tượng Cache Penetration (truy vấn liên tục các ID không tồn tại cả trong Cache lẫn DB) được khắc phục bằng kỹ thuật nào?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000219"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Phân tích Saga Pattern (Choreography vs Orchestration) và Transactional Outbox",
                Content = "Phân tích cách quản lý giao dịch phân tán bằng Saga Pattern (so sánh Choreography và Orchestration) và vai trò của mẫu Transactional Outbox.",
                SampleAnswer = "Saga chia giao dịch phân tán thành chuỗi các giao dịch cục bộ kèm giao dịch bù trừ (Compensating Transactions) khi xảy ra lỗi. Choreography phối hợp phi tập trung qua sự kiện (phù hợp luồng ngắn 2-3 bước), còn Orchestration dùng một bộ điều phối trung tâm quản lý máy trạng thái (phù hợp luồng phức tạp). Transactional Outbox lưu sự kiện vào bảng Outbox trong cùng giao dịch CSDL cục bộ để đảm bảo không bao giờ mất sự kiện.",
                KeyPoints = "[\"Sử dụng Compensating Transactions để hoàn tác nghiệp vụ\",\"Choreography phi tập trung so với Orchestration điều phối tập trung\",\"Transactional Outbox đảm bảo At-Least-Once Event Delivery\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Vì sao khi áp dụng Transactional Outbox, các Consumer xử lý sự kiện bắt buộc phải được thiết kế theo nguyên tắc Idempotent (Lũy đẳng)?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000220"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Phân tích Định lý CAP (CAP Theorem) và định lý PACELC trong Hệ phân tán",
                Content = "Phân tích Định lý CAP (Consistency, Availability, Partition Tolerance) và giải thích sự đánh đổi giữa hệ thống CP và AP khi xảy ra phân mảnh mạng (Network Partition).",
                SampleAnswer = "Định lý CAP chứng minh rằng khi xảy ra phân mảnh mạng (Partition Tolerance - P là tất yếu trong hệ phân tán), hệ thống chỉ có thể chọn ưu tiên Nhất quán tức thì (Consistency - CP, từ chối yêu cầu nếu không đồng bộ được quorum) hoặc Tính sẵn sàng (Availability - AP, vẫn phản hồi từ nhánh mạng cục bộ và chấp nhận Eventual Consistency). Định lý PACELC mở rộng thêm: ngay cả khi mạng bình thường (Else), hệ thống vẫn phải đánh đổi giữa Độ trễ (Latency) và Tính nhất quán (Consistency).",
                KeyPoints = "[\"Partition Tolerance (P) là điều kiện bắt buộc trong mạng vật lý\",\"Hệ thống CP ưu tiên tính đúng đắn tuyệt đối (như giao dịch tài chính, khóa điểm)\",\"Hệ thống AP ưu tiên độ sẵn sàng phục vụ và đồng bộ hóa sau\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Trong hệ thống thi vấn đáp của chúng ta, luồng công bố điểm chính thức (MF-04) cần ưu tiên CP hay AP và tại sao?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000221"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Kiến trúc Chịu lỗi (Resilience): Circuit Breaker, Bulkhead & Exponential Backoff",
                Content = "Phân tích cơ chế hoạt động của 3 trạng thái trong Circuit Breaker Pattern và cách kết hợp với Bulkhead Isolation cùng Exponential Backoff để chống lỗi dây chuyền (Cascading Failure).",
                SampleAnswer = "Circuit Breaker gồm 3 trạng thái: Closed (hoạt động bình thường), Open (ngắt mạch ngay lập tức khi tỷ lệ lỗi vượt ngưỡng để bảo vệ dịch vụ đích) và Half-Open (cho phép một số lượng nhỏ request thăm dò nếu thành công thì đóng mạch lại). Kết hợp với Exponential Backoff (thử lại với khoảng chờ tăng dần kèm Jitter) và Bulkhead Isolation (cô lập vùng tài nguyên Thread/Queue riêng cho từng tác vụ) giúp ngăn chặn một dịch vụ bên ngoài bị chậm làm sập toàn bộ hệ thống.",
                KeyPoints = "[\"3 trạng thái Circuit Breaker: Closed, Open, Half-Open\",\"Exponential Backoff kèm Jitter tránh hiệu ứng Thundering Herd\",\"Bulkhead cô lập tài nguyên chống hiệu ứng Domino sập toàn hệ thống\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Điều gì sẽ xảy ra nếu ta chỉ áp dụng Retry liên tục mà không có Circuit Breaker khi dịch vụ Gemini AI đang bị quá tải nghiêm trọng?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000222"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Bảo mật Kiến trúc Zero-Trust, OAuth 2.0 PKCE và Phòng chống OWASP API Top 10",
                Content = "Phân tích luồng xác thực OAuth 2.0 Authorization Code kèm PKCE cho Single Page Application (React) và cách phòng chống lỗ hổng Broken Object Level Authorization (BOLA / IDOR).",
                SampleAnswer = "Luồng OAuth 2.0 PKCE sinh cặp khóa động code_verifier và code_challenge (băm SHA-256) tại mỗi lần đăng nhập để ngăn chặn tấn công đánh cắp Authorization Code trên trình duyệt. Để chống lỗ hổng BOLA (OWASP API1), mọi Command/Query Handler ở tầng Application bắt buộc phải đối chiếu định danh người dùng lấy từ JWT Claims (StudentId) với chủ sở hữu của bản ghi trong CSDL trước khi đọc hoặc cập nhật.",
                KeyPoints = "[\"PKCE bảo vệ luồng OAuth 2.0 bằng code_verifier và code_challenge SHA-256\",\"Chống BOLA/IDOR bằng cách kiểm tra quyền sở hữu bản ghi tại tầng Application\",\"Không bao giờ tin tưởng mù quáng vào tham số ID gửi từ Client\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Tại sao việc kiểm tra Role ở tầng Controller ([Authorize(Roles = \"student\")]) là chưa đủ để ngăn chặn một sinh viên xem trộm bảng điểm của sinh viên khác?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // =========================================================================
            // 3. MÔN SWR302 — 12 CÂU HỎI (4 Easy / Understand, 4 Medium / Apply, 4 Hard / Analyze)
            // =========================================================================
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000311"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Phân biệt Yêu cầu Chức năng (Functional) và Yêu cầu Phi chức năng (Non-Functional)",
                Content = "Hãy phân biệt rõ Yêu cầu Chức năng (Functional Requirements) và Yêu cầu Phi chức năng (Non-Functional Requirements / Quality Attributes) kèm ví dụ trong hệ thống thi vấn đáp.",
                SampleAnswer = "Yêu cầu Chức năng (FR) mô tả các hành vi, tính năng và dịch vụ cụ thể mà hệ thống phải thực hiện (ví dụ: sinh viên nộp câu trả lời giọng nói, Trưởng bộ môn phê duyệt câu hỏi). Yêu cầu Phi chức năng (NFR) xác định các tiêu chuẩn chất lượng, ràng buộc hiệu năng, bảo mật và độ tin cậy của hệ thống (ví dụ: lưu bài nộp Persist First trong dưới 100ms, khóa điểm một chiều chống sửa đổi).",
                KeyPoints = "[\"Functional Requirements: Hệ thống làm gì (What the system does)\",\"Non-Functional Requirements: Hệ thống hoạt động tốt như thế nào (How well it performs)\",\"NFR quyết định trực tiếp đến kiến trúc phần mềm\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Tại sao một yêu cầu phi chức năng viết chung chung như 'Hệ thống phải chạy nhanh và bảo mật cao' lại bị coi là một yêu cầu kém chất lượng?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000312"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Ba cấp độ Yêu cầu Phần mềm theo mô hình Karl Wiegers",
                Content = "Trình bày sự khác biệt và mối liên hệ giữa 3 cấp độ yêu cầu: Business Requirements, User Requirements và Software (System) Requirements.",
                SampleAnswer = "Business Requirements xác định mục tiêu chiến lược và giá trị kinh doanh cấp cao của tổ chức (tài liệu Vision & Scope). User Requirements mô tả các tác vụ mà từng nhóm người dùng cần thực hiện với hệ thống (biểu diễn qua Use Cases hoặc User Stories). Software Requirements đặc tả chi tiết các hàm chức năng, ràng buộc dữ liệu và thuộc tính chất lượng để đội ngũ kỹ sư lập trình và kiểm thử (tài liệu SRS).",
                KeyPoints = "[\"Business Requirements: Tại sao cần xây dựng hệ thống (Why)\",\"User Requirements: Người dùng làm được gì với hệ thống (Who & What)\",\"System Requirements: Đặc tả kỹ thuật chi tiết để hiện thực hóa (How in detail)\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Nếu một tính năng ở cấp System Requirement không thể truy ngược về bất kỳ Business Requirement nào (Gold Plating), người kỹ sư BA cần xử lý như thế nào?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000313"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Cấu trúc Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) chuẩn IEEE 830 / ISO 29148",
                Content = "Trình bày các phần cốt lõi của một tài liệu SRS theo chuẩn IEEE 830 / ISO/IEC/IEEE 29148 và vai trò của SRS đối với các bên liên quan (Stakeholders).",
                SampleAnswer = "Tài liệu SRS chuẩn gồm: (1) Giới thiệu tổng quan, phạm vi và thuật ngữ; (2) Mô tả tổng thể về bối cảnh sản phẩm, phân lớp người dùng, môi trường vận hành và giả định/ràng buộc; (3) Đặc tả chi tiết các yêu cầu chức năng (Use Cases), giao diện ngoại vi (UI/API/Hardware) và yêu cầu phi chức năng. SRS đóng vai trò là bản hợp đồng kỹ thuật thống nhất giữa khách hàng, đội phát triển và đội kiểm thử QA.",
                KeyPoints = "[\"Cấu trúc chuẩn: Introduction, Overall Description, Specific Requirements\",\"Bao gồm External Interface Requirements và Quality Attributes\",\"Là cơ sở pháp lý và kỹ thuật cho Thiết kế và Kiểm thử\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Trong các dự án Agile hiện đại, tài liệu SRS truyền thống thường được tổ chức và duy trì như thế nào để không trở nên lỗi thời?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000314"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Phân biệt Verification (Kiểm chứng) và Validation (Thẩm định) yêu cầu",
                Content = "Giải thích sự khác nhau giữa Verification và Validation trong Kỹ nghệ Yêu cầu Phần mềm theo châm ngôn của Barry Boehm.",
                SampleAnswer = "Theo Barry Boehm, Verification trả lời câu hỏi 'Are we building the product right?' (kiểm chứng xem tài liệu đặc tả và phần mềm có tuân thủ đúng chuẩn mực, nhất quán và không mâu thuẫn với thiết kế hay không). Validation trả lời câu hỏi 'Are we building the right product?' (thẩm định cùng khách hàng/người dùng thực tế xem yêu cầu và sản phẩm có thực sự giải quyết đúng bài toán nghiệp vụ của họ hay không).",
                KeyPoints = "[\"Verification: Xây dựng sản phẩm đúng quy chuẩn (Building the product right)\",\"Validation: Xây dựng đúng sản phẩm người dùng cần (Building the right product)\",\"Kết hợp Peer Review/Inspection và User Acceptance Testing (UAT)\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Chi phí sửa một lỗi yêu cầu phát hiện ở giai đoạn Validation sau khi đã viết code lớn hơn bao nhiêu lần so với phát hiện ngay lúc Review SRS?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000315"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Vận dụng các Kỹ thuật Thu thập Yêu cầu (Requirement Elicitation Techniques)",
                Content = "So sánh ưu nhược điểm của các kỹ thuật Elicitation: Phỏng vấn (Interview), Hội thảo JAD Workshop, Quan sát hiện trường (Observation) và Tạo mẫu (Prototyping).",
                SampleAnswer = "Phỏng vấn giúp khai thác sâu góc nhìn cá nhân của lãnh đạo/chuyên gia nhưng tốn thời gian; JAD Workshop tập hợp nhiều bên liên quan để giải quyết xung đột yêu cầu ngay tại chỗ; Quan sát hiện trường (Job Shadowing) giúp phát hiện các thao tác ngầm (Tacit Knowledge) mà người dùng quên kể; còn Prototyping giúp trực quan hóa giao diện sớm để nhận phản hồi chính xác khi người dùng chưa hình dung rõ yêu cầu.",
                KeyPoints = "[\"Interview khai thác chiều sâu, Workshop đồng thuận đa chiều\",\"Observation phát hiện quy trình thực tế và Tacit Knowledge\",\"Prototyping giảm thiểu hiểu lầm về giao diện và luồng tương tác\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Khi khảo sát quy trình coi thi phòng Lab Kiosk (MF-04), bạn sẽ kết hợp những kỹ thuật Elicitation nào với Giám thị và Giảng viên?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000316"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Đặc tả Use Case hoàn chỉnh và tiêu chuẩn INVEST cho User Story",
                Content = "Trình bày các thành phần bắt buộc của một Use Case Specification chuẩn và giải thích 6 chữ cái của tiêu chuẩn INVEST khi viết User Story.",
                SampleAnswer = "Một đặc tả Use Case hoàn chỉnh gồm: ID, Tên, Actor chính/phụ, Preconditions, Postconditions, Main Success Scenario (luồng chính), Alternative Flows (luồng rẽ nhánh) và Exception Flows (luồng xử lý lỗi). Với User Story trong Agile, tiêu chuẩn INVEST gồm: Independent (Độc lập), Negotiable (Có thể thương lượng), Valuable (Mang lại giá trị), Estimable (Ước lượng được), Small (Đủ nhỏ cho 1 Sprint) và Testable (Kiểm thử được).",
                KeyPoints = "[\"Use Case gồm Pre/Postconditions, Main Flow, Alternative và Exception Flows\",\"INVEST: Independent, Negotiable, Valuable, Estimable, Small, Testable\",\"Exception Flow là nguồn đầu vào quan trọng để thiết kế xử lý lỗi và viết Test Case\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Trong sơ đồ Use Case UML, mối quan hệ <<include>> và <<extend>> khác nhau ở điểm cốt lõi nào?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000317"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Xây dựng Tiêu chí Chấp nhận (Acceptance Criteria) theo chuẩn Given-When-Then",
                Content = "Hãy trình bày cách viết Acceptance Criteria theo cú pháp Gherkin (Given-When-Then) trong Behavior-Driven Development (BDD) cho tính năng kiểm tra hạn ngạch thi thử mỗi ngày.",
                SampleAnswer = "Cú pháp Gherkin chuẩn hóa kịch bản kiểm thử hành vi gồm: Given (tiền điều kiện/trạng thái ban đầu: sinh viên đã sử dụng hết N/N lượt thi thử trong ngày của môn học), When (hành động kích hoạt: sinh viên gửi yêu cầu bắt đầu ca thi thử mới), và Then (kết quả mong đợi có thể kiểm chứng: hệ thống từ chối khởi tạo ca thi và trả về mã trạng thái HTTP 429 Too Many Requests kèm thông báo rõ ràng).",
                KeyPoints = "[\"Given: Thiết lập bối cảnh và dữ liệu tiền điều kiện\",\"When: Hành động cụ thể của người dùng hoặc hệ thống\",\"Then: Kết quả đầu ra tường minh dùng để viết Automated Test\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Tại sao mỗi kịch bản Given-When-Then nên tập trung kiểm tra một quy tắc nghiệp vụ đơn lẻ thay vì gộp nhiều hành động nối tiếp nhau?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000318"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Phương pháp Phân loại Ưu tiên Yêu cầu MoSCoW và Mô hình Kano",
                Content = "Giải thích cách áp dụng kỹ thuật MoSCoW và mô hình Kano để sắp xếp thứ tự ưu tiên các yêu cầu trong Product Backlog khi nguồn lực và thời gian có hạn.",
                SampleAnswer = "Phương pháp MoSCoW chia yêu cầu thành 4 nhóm: Must-have (bắt buộc phải có để hệ thống vận hành hợp lệ), Should-have (quan trọng nhưng có giải pháp tạm thay thế), Could-have (tiện ích nâng cao nếu còn thời gian) và Won't-have this time (loại khỏi phạm vi đợt phát hành hiện tại). Mô hình Kano phân tích mức độ hài lòng của người dùng qua 3 nhóm: Basic Needs (Must-be), Performance Needs (One-dimensional) và Delighters (Attractive).",
                KeyPoints = "[\"MoSCoW: Must-have, Should-have, Could-have, Won't-have\",\"Kano Model: Basic, Performance và Excitement/Delighter features\",\"Đảm bảo bàn giao MVP đúng hạn mà không vỡ ngân sách\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Nếu khách hàng khẳng định 100% tính năng đều là Must-have, bạn sẽ dùng kỹ thuật thương lượng nào để giúp họ phân định độ ưu tiên thực tế?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000319"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Thiết kế Ma trận Truy vết Yêu cầu (Requirements Traceability Matrix - RTM) hai chiều",
                Content = "Phân tích cấu trúc của Ma trận Truy vết Yêu cầu (RTM) và giải thích sự khác biệt giữa Forward Traceability và Backward Traceability trong quản lý chất lượng dự án.",
                SampleAnswer = "Ma trận RTM liên kết toàn bộ vòng đời của từng yêu cầu từ Business Goal -> Use Case/SRS -> Kiến trúc/Module mã nguồn -> Test Cases. Forward Traceability (truy vết xuôi từ yêu cầu tới thiết kế, code và test) đảm bảo mọi yêu cầu khách hàng đề ra đều được hiện thực và kiểm thử đầy đủ 100%. Backward Traceability (truy vết ngược từ test/code về yêu cầu gốc) giúp phát hiện code thừa (Gold Plating / Orphan Code) và đánh giá chính xác phạm vi ảnh hưởng (Impact Analysis) khi một yêu cầu thay đổi.",
                KeyPoints = "[\"Forward Traceability đảm bảo không bỏ sót bất kỳ yêu cầu nào\",\"Backward Traceability ngăn chặn Gold Plating và Orphan Code\",\"Công cụ cốt lõi để phân tích ma trận tác động (Impact Analysis)\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Khi một yêu cầu nghiệp vụ bị thay đổi ở giai đoạn cuối dự án, bạn sử dụng RTM như thế nào để ước lượng chính xác nỗ lực sửa đổi (Effort Estimation)?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000320"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Phân tích nguyên nhân và quy trình kiểm soát Trượt phạm vi (Scope Creep)",
                Content = "Phân tích nguyên nhân dẫn đến hiện tượng Scope Creep trong dự án phần mềm và thiết lập quy trình Quản lý Thay đổi (Change Control Process) để bảo vệ tiến độ dự án.",
                SampleAnswer = "Scope Creep xảy ra khi phạm vi dự án phình to không kiểm soát do đường cơ sở yêu cầu (Requirements Baseline) định nghĩa mơ hồ, thiếu sự tham gia của người dùng thực tế từ đầu, hoặc lập trình viên tự ý nhận lời thêm tính năng ngoài luồng. Quy trình kiểm soát chuẩn yêu cầu: khóa Baseline -> tiếp nhận Change Request chính thức -> phân tích ma trận tác động (chi phí, thời gian, rủi ro kiến trúc) -> trình Hội đồng Kiểm soát Thay đổi (CCB) phê duyệt -> cập nhật lại Baseline và kế hoạch phát hành.",
                KeyPoints = "[\"Thiết lập và khóa đường cơ sở yêu cầu (Requirements Baseline)\",\"Mọi thay đổi phải qua bước Impact Analysis về lịch trình và kiến trúc\",\"Hội đồng CCB phê duyệt đánh đổi phạm vi, thời gian hoặc chi phí\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Làm sao để cân bằng giữa việc chào đón sự thay đổi trong Agile (Embrace Change) và việc bảo vệ Sprint Goal không bị phá vỡ giữa chừng?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000321"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Thẩm định chất lượng SRS: Phát hiện tính Mơ hồ, Mâu thuẫn và Không thể kiểm thử",
                Content = "Phân tích các lỗi chất lượng phổ biến nhất trong tài liệu đặc tả yêu cầu (Ambiguity, Inconsistency, Incompleteness, Untestability) và phương pháp khắc phục triệt để.",
                SampleAnswer = "Tính mơ hồ (Ambiguity) xuất hiện khi dùng từ ngữ đa nghĩa như 'nhanh chóng, thân thiện, linh hoạt' khiến Dev và QA hiểu khác nhau; Tính mâu thuẫn (Inconsistency) xảy ra khi hai điều khoản quy định trái ngược nhau (ví dụ một nơi ghi tối đa 3 câu, nơi khác ghi 5 câu); Tính thiếu sót (Incompleteness) là bỏ quên các ca biên và luồng lỗi (Exception Flows). Khắc phục bằng cách áp dụng từ điển thuật ngữ chuẩn (Glossary), lượng hóa mọi tiêu chí và tổ chức buổi Inspection chéo có sự tham gia của BA, Architect và QA Lead.",
                KeyPoints = "[\"Loại bỏ từ ngữ cảm tính, định tính không đo lường được\",\"Đồng bộ hóa toàn diện các con số và quy tắc qua Single Source of Truth\",\"Mọi yêu cầu bắt buộc phải đi kèm kịch bản kiểm thử xác nhận (Testable)\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Hãy lấy một ví dụ chuyển đổi một câu yêu cầu mơ hồ thành một câu đặc tả chuẩn kỹ thuật có thể kiểm thử tự động 100%.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000322"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Định lượng Yêu cầu Phi chức năng (SLA, P95/P99 Latency, Reliability & Security)",
                Content = "Phân tích phương pháp đặc tả định lượng (Planguage / Fit Criteria) cho các yêu cầu Hiệu năng, Độ tin cậy và Khả năng phục hồi trong một hệ thống AI chấm thi trực tuyến.",
                SampleAnswer = "Để đặc tả NFR có thể kiểm chứng, ta sử dụng thang đo định lượng (Scale), phương pháp đo (Meter) và ngưỡng chấp nhận (Must/Plan). Ví dụ: Về hiệu năng, 95% (P95) yêu cầu nộp bài phải hoàn tất ghi đĩa dưới 100ms ở mức tải 200 thí sinh đồng thời; Về độ tin cậy, tỷ lệ mất dữ liệu bài thi bằng 0% (Zero Data Loss) nhờ cơ chế Polly Retry 3 lần (2s, 4s, 8s) và cô lập vào Dead-Letter Queue quét lại mỗi 5 phút; Về bảo mật, 100% tệp ghi âm phòng thi được niêm phong mã băm SHA-256.",
                KeyPoints = "[\"Sử dụng chỉ số phân vị P95/P99 thay vì chỉ tính trung bình cộng\",\"Xác định rõ tải đồng thời (Concurrency Load) và ngưỡng SLA\",\"Gắn chặt yêu cầu phi chức năng với cơ chế kiểm chứng tự động\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Tại sao khi đo thời gian phản hồi của API, chỉ số phân vị P95 và P99 lại phản ánh trải nghiệm người dùng chính xác hơn chỉ số trung bình cộng (Average)?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }
        };

        var existingQuestionIds = (await context.PracticeQuestions
            .Select(q => q.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        foreach (var q in allQuestions)
        {
            if (!existingQuestionIds.Contains(q.Id))
            {
                context.PracticeQuestions.Add(q);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureClassesAndEnrollmentsAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var semester = await context.Semesters
            .FirstOrDefaultAsync(s => s.Id == SemesterFa26Id || s.Code == "FA26", cancellationToken);
        var semesterId = semester?.Id ?? SemesterFa26Id;

        var prnCourse = await context.Courses.FirstAsync(c => c.Code == "PRN231", cancellationToken);
        var swdCourse = await context.Courses.FirstAsync(c => c.Code == "SWD392", cancellationToken);
        var swrCourse = await context.Courses.FirstAsync(c => c.Code == "SWR302", cancellationToken);

        var lecturer = await context.Users
            .FirstOrDefaultAsync(u => u.Id == LecturerUserId || u.Role == UserRole.Lecturer, cancellationToken);
        var lecturerId = lecturer?.Id;

        var targetClasses = new List<Class>
        {
            new()
            {
                Id = ClassPrn231Id,
                Code = "SE1801-NET",
                CourseId = prnCourse.Id,
                SemesterId = semesterId,
                LecturerId = lecturerId,
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassSwd392Id,
                Code = "SE1801-SWD",
                CourseId = swdCourse.Id,
                SemesterId = semesterId,
                LecturerId = lecturerId,
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassSwr302Id,
                Code = "SE1801-SWR",
                CourseId = swrCourse.Id,
                SemesterId = semesterId,
                LecturerId = lecturerId,
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }
        };

        var existingClasses = await context.Classes.ToListAsync(cancellationToken);
        foreach (var cls in targetClasses)
        {
            var existing = existingClasses.FirstOrDefault(c => c.Id == cls.Id || c.Code == cls.Code);
            if (existing == null)
            {
                context.Classes.Add(cls);
            }
            else
            {
                existing.IsActive = true;
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var activeClasses = await context.Classes
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        var studentUsers = await context.Users
            .Where(u => u.Role == UserRole.Student && u.IsActive)
            .ToListAsync(cancellationToken);

        var existingEnrollments = await context.ClassEnrollments
            .Select(e => new { e.ClassId, e.StudentId })
            .ToListAsync(cancellationToken);

        var enrolledPairs = existingEnrollments
            .Select(e => (e.ClassId, e.StudentId))
            .ToHashSet();

        foreach (var student in studentUsers)
        {
            foreach (var cls in activeClasses)
            {
                if (!enrolledPairs.Contains((cls.Id, student.Id)))
                {
                    context.ClassEnrollments.Add(new ClassEnrollment
                    {
                        Id = Guid.NewGuid(),
                        ClassId = cls.Id,
                        StudentId = student.Id,
                        Status = "enrolled",
                        EnrolledAt = SeedTimestampUtc
                    });
                    enrolledPairs.Add((cls.Id, student.Id));
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureStudentPracticeAndMockExamHistoryAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var prnCourse = await context.Courses.FirstAsync(c => c.Code == "PRN231", cancellationToken);
        var swdCourse = await context.Courses.FirstAsync(c => c.Code == "SWD392", cancellationToken);
        var swrCourse = await context.Courses.FirstAsync(c => c.Code == "SWR302", cancellationToken);

        var prnQuestions = await context.PracticeQuestions
            .Where(q => q.CourseId == prnCourse.Id && q.IsActive)
            .OrderBy(q => q.Id)
            .ToListAsync(cancellationToken);
        var swdQuestions = await context.PracticeQuestions
            .Where(q => q.CourseId == swdCourse.Id && q.IsActive)
            .OrderBy(q => q.Id)
            .ToListAsync(cancellationToken);
        var swrQuestions = await context.PracticeQuestions
            .Where(q => q.CourseId == swrCourse.Id && q.IsActive)
            .OrderBy(q => q.Id)
            .ToListAsync(cancellationToken);

        var prnCriteria = await context.RubricCriteria
            .Where(c => c.RubricId == RubricPrn231Id)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);
        var swdCriteria = await context.RubricCriteria
            .Where(c => c.RubricId == RubricSwd392Id)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);
        var swrCriteria = await context.RubricCriteria
            .Where(c => c.RubricId == RubricSwr302Id)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        var prnSet = await context.ExamSets.FirstAsync(s => s.CourseId == prnCourse.Id, cancellationToken);
        var swdSet = await context.ExamSets.FirstAsync(s => s.CourseId == swdCourse.Id, cancellationToken);
        var swrSet = await context.ExamSets.FirstAsync(s => s.CourseId == swrCourse.Id, cancellationToken);

        var students = await context.Users
            .Where(u => u.Role == UserRole.Student && u.IsActive)
            .ToListAsync(cancellationToken);

        var baseTime = DateTime.UtcNow.AddDays(-2);

        foreach (var student in students)
        {
            // 1. Seed 3 phiên PracticeSession đã hoàn thành (PRN231 Per-Question, SWD392 Full-Session, SWR302 Per-Question)
            var existingPracticeCourses = (await context.PracticeSessions
                .Where(s => s.StudentId == student.Id && s.Status == PracticeSessionStatus.Completed)
                .Select(s => s.CourseId)
                .ToListAsync(cancellationToken)).ToHashSet();

            if (!existingPracticeCourses.Contains(prnCourse.Id) && prnQuestions.Count >= 3 && prnCriteria.Count >= 3)
            {
                CreateCompletedPracticeSession(
                    context,
                    student.Id,
                    prnCourse.Id,
                    PracticeMode.PerQuestion,
                    "[\"easy\",\"medium\"]",
                    prnQuestions.Take(3).ToList(),
                    prnCriteria,
                    baseTime.AddHours(-18),
                    new[] { 8.50m, 8.00m, 9.00m });
            }

            if (!existingPracticeCourses.Contains(swdCourse.Id) && swdQuestions.Count >= 3 && swdCriteria.Count >= 3)
            {
                CreateCompletedPracticeSession(
                    context,
                    student.Id,
                    swdCourse.Id,
                    PracticeMode.FullSession,
                    "[\"easy\",\"medium\",\"hard\"]",
                    new List<PracticeQuestion>
                    {
                        swdQuestions.First(q => q.Difficulty == QuestionDifficulty.Easy),
                        swdQuestions.First(q => q.Difficulty == QuestionDifficulty.Medium),
                        swdQuestions.First(q => q.Difficulty == QuestionDifficulty.Hard)
                    },
                    swdCriteria,
                    baseTime.AddHours(-12),
                    new[] { 8.80m, 8.20m, 8.50m });
            }

            if (!existingPracticeCourses.Contains(swrCourse.Id) && swrQuestions.Count >= 3 && swrCriteria.Count >= 3)
            {
                CreateCompletedPracticeSession(
                    context,
                    student.Id,
                    swrCourse.Id,
                    PracticeMode.PerQuestion,
                    "[\"medium\",\"hard\"]",
                    swrQuestions.Skip(2).Take(3).ToList(),
                    swrCriteria,
                    baseTime.AddHours(-6),
                    new[] { 7.80m, 8.50m, 8.90m });
            }

            // 2. Seed 3 phiên MockExamSession đã chấm điểm (Graded Scorecard) cho PRN231, SWD392, SWR302
            var existingMockCourses = (await context.MockExamSessions
                .Where(s => s.StudentId == student.Id && s.Status == "graded")
                .Select(s => s.CourseId)
                .ToListAsync(cancellationToken)).ToHashSet();

            if (!existingMockCourses.Contains(prnCourse.Id) && prnQuestions.Count >= 5 && prnCriteria.Count >= 3)
            {
                CreateGradedMockExamSession(
                    context,
                    student.Id,
                    prnCourse.Id,
                    prnSet.Id,
                    true,
                    prnQuestions.Take(5).ToList(),
                    prnCriteria,
                    baseTime.AddHours(-20),
                    30,
                    new[] { 8.50m, 9.00m, 8.00m, 8.50m, 9.00m });
            }

            if (!existingMockCourses.Contains(swdCourse.Id) && swdQuestions.Count >= 5 && swdCriteria.Count >= 3)
            {
                CreateGradedMockExamSession(
                    context,
                    student.Id,
                    swdCourse.Id,
                    swdSet.Id,
                    true,
                    swdQuestions.Take(5).ToList(),
                    swdCriteria,
                    baseTime.AddHours(-10),
                    25,
                    new[] { 8.80m, 8.40m, 9.00m, 8.20m, 8.60m });
            }

            if (!existingMockCourses.Contains(swrCourse.Id) && swrQuestions.Count >= 5 && swrCriteria.Count >= 3)
            {
                CreateGradedMockExamSession(
                    context,
                    student.Id,
                    swrCourse.Id,
                    swrSet.Id,
                    false,
                    swrQuestions.Take(5).ToList(),
                    swrCriteria,
                    baseTime.AddHours(-4),
                    25,
                    new[] { 8.00m, 8.50m, 7.50m, 8.50m, 8.50m });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void CreateCompletedPracticeSession(
        OralExamDbContext context,
        Guid studentId,
        Guid courseId,
        string practiceMode,
        string selectedDifficultiesJson,
        IReadOnlyList<PracticeQuestion> questions,
        IReadOnlyList<RubricCriterion> criteria,
        DateTime startedAt,
        IReadOnlyList<decimal> scores)
    {
        var sessionId = Guid.NewGuid();
        var completedAt = startedAt.AddMinutes(15);

        var session = new PracticeSession
        {
            Id = sessionId,
            StudentId = studentId,
            CourseId = courseId,
            PracticeMode = practiceMode,
            SelectedDifficulties = selectedDifficultiesJson,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            LastActivityAt = completedAt,
            Status = PracticeSessionStatus.Completed
        };
        context.PracticeSessions.Add(session);

        for (int i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var totalScore = scores[i % scores.Count];
            var answerId = Guid.NewGuid();
            var submittedAt = startedAt.AddMinutes((i + 1) * 4);

            var answer = new PracticeAnswer
            {
                Id = answerId,
                SessionId = sessionId,
                QuestionId = q.Id,
                StudentId = studentId,
                AnswerText = !string.IsNullOrWhiteSpace(q.SampleAnswer)
                    ? $"{q.SampleAnswer} Ngoài ra, trong thực tế triển khai dự án em luôn áp dụng kiểm thử tự động để xác minh tính đúng đắn."
                    : "Em trình bày đầy đủ các khái niệm cốt lõi, nguyên lý thiết kế và ví dụ thực tiễn theo đúng chuẩn kiến trúc.",
                IsFollowUp = false,
                ParentAnswerId = null,
                Status = PracticeAnswerStatus.Graded,
                SubmittedAt = submittedAt
            };
            context.PracticeAnswers.Add(answer);

            var evalId = Guid.NewGuid();
            var details = BuildEvaluationDetails(evalId, criteria, totalScore);

            var evaluation = new AiEvaluation
            {
                Id = evalId,
                AnswerType = "practice",
                PracticeAnswerId = answerId,
                ExamSubmissionId = null,
                MockExamAnswerId = null,
                TotalScore = details.Sum(d => d.Score),
                Feedback = $"Câu trả lời nắm vững trọng tâm câu hỏi \"{q.Title}\", giải thích rõ bản chất kỹ thuật và liên hệ tốt với thực tiễn.",
                ConfidenceScore = 0.94m,
                CotTrace = $"Phân tích câu trả lời cho \"{q.Title}\": Đạt đầy đủ các ý chính trong KeyPoints, lập luận mạch lạc theo chuẩn CLO.",
                EvaluatedAt = submittedAt.AddSeconds(3),
                Details = details
            };
            context.AiEvaluations.Add(evaluation);
        }
    }

    private static void CreateGradedMockExamSession(
        OralExamDbContext context,
        Guid studentId,
        Guid courseId,
        Guid examSetId,
        bool hasFollowUp,
        IReadOnlyList<PracticeQuestion> questions,
        IReadOnlyList<RubricCriterion> criteria,
        DateTime startedAt,
        int durationMinutes,
        IReadOnlyList<decimal> scores)
    {
        var sessionId = Guid.NewGuid();
        var expiresAt = startedAt.AddMinutes(durationMinutes);
        var submittedAt = startedAt.AddMinutes(Math.Max(10, durationMinutes - 4));

        var actualQuestionScores = new List<decimal>();

        for (int i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var targetScore = scores[i % scores.Count];
            var answerId = Guid.NewGuid();
            var answeredAt = startedAt.AddMinutes((i + 1) * 3);

            var evalId = Guid.NewGuid();
            var details = BuildEvaluationDetails(evalId, criteria, targetScore);
            var questionScore = details.Sum(d => d.Score);
            actualQuestionScores.Add(questionScore);

            var mockAnswer = new MockExamAnswer
            {
                Id = answerId,
                SessionId = sessionId,
                QuestionId = q.Id,
                AnswerText = !string.IsNullOrWhiteSpace(q.SampleAnswer)
                    ? $"{q.SampleAnswer} Bên cạnh đó, em xin phân tích thêm ưu nhược điểm và cách vận dụng trong hệ thống thực tế."
                    : "Em trình bày chi tiết các luận điểm kỹ thuật, phân tích trade-off và nêu ví dụ áp dụng cụ thể.",
                TimeTakenSeconds = 75 + (i * 12),
                Status = "graded",
                AnsweredAt = answeredAt
            };
            context.MockExamAnswers.Add(mockAnswer);

            var evaluation = new AiEvaluation
            {
                Id = evalId,
                AnswerType = "practice",
                PracticeAnswerId = null,
                ExamSubmissionId = null,
                MockExamAnswerId = answerId,
                TotalScore = questionScore,
                Feedback = $"Trình bày mạch lạc, nắm chắc kiến thức cốt lõi về \"{q.Title}\" và phân tích đúng theo các tiêu chí Barem Rubric 10.0.",
                ConfidenceScore = 0.95m,
                CotTrace = $"[CoT Grading] Đối chiếu câu trả lời với 3 tiêu chí Rubric của \"{q.Title}\": Đạt {questionScore:0.00}/10.00 điểm.",
                EvaluatedAt = submittedAt,
                Details = details
            };
            context.AiEvaluations.Add(evaluation);
        }

        var avgTotalScore = actualQuestionScores.Count > 0
            ? Math.Round(actualQuestionScores.Average(), 2, MidpointRounding.AwayFromZero)
            : 0.00m;

        var session = new MockExamSession
        {
            Id = sessionId,
            StudentId = studentId,
            ExamSetId = examSetId,
            CourseId = courseId,
            HasFollowUp = hasFollowUp,
            StartedAt = startedAt,
            ExpiresAt = expiresAt,
            SubmittedAt = submittedAt,
            Status = "graded",
            TotalScore = avgTotalScore
        };
        context.MockExamSessions.Add(session);
    }

    private static List<AiEvaluationDetail> BuildEvaluationDetails(
        Guid evaluationId,
        IReadOnlyList<RubricCriterion> criteria,
        decimal targetTotalScore)
    {
        var details = new List<AiEvaluationDetail>();
        decimal ratio = Math.Clamp(targetTotalScore / 10.0m, 0.0m, 1.0m);
        decimal runningSum = 0.0m;

        for (int i = 0; i < criteria.Count; i++)
        {
            var c = criteria[i];
            decimal score;
            if (i < criteria.Count - 1)
            {
                score = Math.Round(c.MaxScore * ratio, 1, MidpointRounding.AwayFromZero);
                score = Math.Clamp(score, 0.0m, c.MaxScore);
                runningSum += score;
            }
            else
            {
                score = Math.Clamp(Math.Round(targetTotalScore - runningSum, 2, MidpointRounding.AwayFromZero), 0.0m, c.MaxScore);
            }

            details.Add(new AiEvaluationDetail
            {
                Id = Guid.NewGuid(),
                EvaluationId = evaluationId,
                CriterionId = c.Id,
                Score = score,
                Comment = $"Đạt {score:0.0}/{c.MaxScore:0.0}đ — Trình bày rõ ràng và đáp ứng tốt tiêu chí \"{c.CriterionName}\"."
            });
        }

        return details;
    }

    private static async Task EnsureExamQuestionsAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var prnCourse = await context.Courses.FirstAsync(c => c.Code == "PRN231", cancellationToken);
        var swdCourse = await context.Courses.FirstAsync(c => c.Code == "SWD392", cancellationToken);
        var swrCourse = await context.Courses.FirstAsync(c => c.Code == "SWR302", cancellationToken);

        var targetExamQuestions = new List<ExamQuestion>
        {
            // =========================================================================
            // 1. MÔN PRN231 — 4 CÂU HỎI THI THẬT (APPROVED, DRAFT, SUBMITTED_FOR_REVIEW, NEEDS_REVISION)
            // =========================================================================
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Phân biệt BackgroundService và IHostedService trong .NET 8",
                Content = "Phân biệt sự khác nhau giữa BackgroundService và IHostedService trong .NET 8 khi xây dựng hàng đợi xử lý chấm điểm ngầm?",
                SampleAnswer = "IHostedService là interface nền tảng định nghĩa StartAsync và StopAsync. BackgroundService là một abstract class kế thừa IHostedService và triển khai sẵn ExecuteAsync chạy nền vòng lặp bất đồng bộ an toàn kèm CancellationToken.",
                KeyPoints = "[\"IHostedService là interface cốt lõi\",\"BackgroundService là abstract class kế thừa IHostedService\",\"ExecuteAsync chạy Task bất đồng bộ với StoppingToken\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                ApprovedBy = DepartmentHeadUserId,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.Manual,
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000401"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Cơ chế Middleware Pipeline và Thứ tự Thực thi trong ASP.NET Core .NET 8",
                Content = "Hãy trình bày nguyên lý hoạt động của Request Delegate Pipeline trong ASP.NET Core .NET 8 và giải thích tại sao thứ tự khai báo UseCors, UseAuthentication, UseAuthorization lại mang tính sống còn?",
                SampleAnswer = "Middleware Pipeline trong ASP.NET Core hoạt động theo mô hình chuỗi trách nhiệm hai chiều (Request đi vào và Response đi ngược ra). UseCors bắt buộc đặt trước để xử lý gói tin Preflight OPTIONS, tiếp theo là UseAuthentication để giải mã JWT Token tạo ClaimsPrincipal, và sau cùng là UseAuthorization để kiểm tra quyền truy cập vào Endpoint.",
                KeyPoints = "[\"Mô hình chuỗi Middleware hai chiều Request/Response\",\"UseCors phải đứng trước để xử lý Preflight OPTIONS\",\"UseAuthentication giải mã danh tính trước khi UseAuthorization kiểm tra quyền\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                ApprovedBy = null,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.Manual,
                ApprovalStatus = ApprovalStatus.Draft,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(2)
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000402"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Kiểm soát Truy cập Đồng thời (Concurrency Control) trong EF Core 8",
                Content = "Phân tích cách xử lý xung đột ghi dữ liệu đồng thời (Optimistic Concurrency Control) trong Entity Framework Core 8 với PostgreSQL xmin hoặc ConcurrencyToken.",
                SampleAnswer = "Optimistic Concurrency không khóa cứng bản ghi khi đọc mà đính kèm một cột phiên bản (như xmin trong PostgreSQL hoặc RowVersion/Timestamp) vào mệnh đề WHERE khi thực thi UPDATE/DELETE. Nếu số dòng bị ảnh hưởng bằng 0, EF Core ném DbUpdateConcurrencyException để ứng dụng xử lý xung đột an toàn.",
                KeyPoints = "[\"Sử dụng ConcurrencyToken hoặc cột hệ thống xmin của PostgreSQL\",\"Kiểm tra phiên bản trong mệnh đề WHERE khi UPDATE\",\"Bắt và xử lý ngoại lệ DbUpdateConcurrencyException chuẩn RFC 7807\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                ApprovedBy = null,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.FlmApi,
                ApprovalStatus = ApprovalStatus.SubmittedForReview,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(4)
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000403"),
                CourseId = prnCourse.Id,
                RubricId = RubricPrn231Id,
                Title = "Quản lý Vòng đời DbContext và IServiceScopeFactory trong Background Worker",
                Content = "Giải thích tại sao một BackgroundService (Singleton) không được phép inject trực tiếp OralExamDbContext (Scoped) qua Constructor và cách khắc phục chuẩn xác bằng IServiceScopeFactory.",
                SampleAnswer = "Inject một dịch vụ Scoped như DbContext vào Singleton sẽ gây ra lỗi Captive Dependency làm DbContext bị giữ sống suốt vòng đời ứng dụng, dẫn đến rò rỉ bộ nhớ ChangeTracker và lỗi không an toàn đa luồng (Thread-safety). Cách khắc phục chuẩn là inject IServiceScopeFactory và tạo một Scope mới cho mỗi tác vụ chấm điểm.",
                KeyPoints = "[\"Ngăn chặn lỗi Captive Dependency giữa Singleton và Scoped\",\"DbContext không hỗ trợ truy cập đa luồng đồng thời\",\"Sử dụng IServiceScopeFactory.CreateScope() trong mỗi vòng lặp xử lý\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                ApprovedBy = DepartmentHeadUserId,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.Manual,
                ApprovalStatus = ApprovalStatus.NeedsRevision,
                ReviewNotes = "Cần bổ sung rõ hơn tình huống xử lý Graceful Shutdown khi container dừng đột ngột và cách giải phóng kết nối Npgsql Connection Pool.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(6)
            },

            // =========================================================================
            // 2. MÔN SWD392 — 4 CÂU HỎI THI THẬT (APPROVED, DRAFT, SUBMITTED_FOR_REVIEW, NEEDS_REVISION)
            // =========================================================================
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000404"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Thiết kế Kiến trúc Chịu lỗi với Polly Resilience Pipeline và Dead-Letter Queue",
                Content = "Phân tích kiến trúc kết hợp Exponential Backoff Retry, Circuit Breaker và Dead-Letter Queue (DLQ) khi tích hợp với dịch vụ LLM bên thứ ba trong hệ thống chấm thi vấn đáp.",
                SampleAnswer = "Khi gọi API Gemini gặp lỗi tạm thời (HTTP 429 hoặc 503), Polly Retry thực hiện thử lại 3 lần theo chu kỳ lũy thừa (2s, 4s, 8s). Nếu lỗi vượt ngưỡng liên tiếp, Circuit Breaker ngắt mạch để tránh hiệu ứng thác đổ. Các tác vụ vẫn thất bại sau khi hết lượt retry sẽ được cô lập vào bảng dead_letter_queues và quét xử lý lại sau mỗi 5 phút để đảm bảo Zero Data Loss.",
                KeyPoints = "[\"Polly Retry 3 lần theo cấp số nhân (2s, 4s, 8s)\",\"Circuit Breaker ngăn chặn lỗi dây chuyền (Cascading Failure)\",\"Dead-Letter Queue bảo toàn 100% bài thi không bao giờ mất dữ liệu\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                ApprovedBy = DepartmentHeadUserId,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.FlmApi,
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(8)
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000405"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Áp dụng Nguyên lý Liskov Substitution (LSP) và Interface Segregation (ISP)",
                Content = "Trình bày nguyên lý Liskov Substitution Principle (LSP) và Interface Segregation Principle (ISP) trong thiết kế hướng đối tượng kèm tình huống vi phạm thường gặp.",
                SampleAnswer = "Liskov Substitution Principle yêu cầu các lớp dẫn xuất phải có thể thay thế hoàn toàn lớp cơ sở mà không làm sai lệch hành vi đúng đắn của chương trình. Interface Segregation Principle khẳng định không nên ép client phụ thuộc vào những phương thức mà nó không sử dụng bằng cách tách các Fat Interface thành các Role Interface nhỏ gọn, chuyên biệt.",
                KeyPoints = "[\"LSP bảo toàn hợp đồng hành vi khi kế thừa đa hình\",\"ISP tách biệt các Interface cồng kềnh thành các Interface chuyên biệt\",\"Giảm sự phụ thuộc thừa và tăng tính dễ bảo trì của hệ thống\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                ApprovedBy = null,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.Manual,
                ApprovalStatus = ApprovalStatus.Draft,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(10)
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000406"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Mẫu thiết kế Specification Pattern và Repository Pattern trong Domain-Driven Design",
                Content = "Giải thích cách kết hợp Specification Pattern với Repository Pattern để đóng gói các quy tắc lọc dữ liệu nghiệp vụ phức tạp mà không làm rò rỉ logic truy vấn ra tầng Application.",
                SampleAnswer = "Specification Pattern đóng gói từng điều kiện nghiệp vụ (như câu hỏi đã được phê duyệt, thuộc môn học đang hoạt động và đúng cấp độ Bloom) thành các đối tượng có thể kết hợp với nhau. Khi truyền Specification vào Repository, hệ thống giữ cho các Query Handler ngắn gọn, tránh trùng lặp biểu thức LINQ và dễ dàng viết Unit Test độc lập.",
                KeyPoints = "[\"Đóng gói quy tắc lọc nghiệp vụ thành đối tượng tái sử dụng\",\"Loại bỏ tình trạng trùng lặp câu lệnh LINQ rải rác ở các Handler\",\"Tăng khả năng kiểm thử đơn vị (Unit Testability) cho quy tắc miền\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                ApprovedBy = null,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.FlmApi,
                ApprovalStatus = ApprovalStatus.SubmittedForReview,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(12)
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000407"),
                CourseId = swdCourse.Id,
                RubricId = RubricSwd392Id,
                Title = "Thiết kế Kiến trúc Bảo mật Phòng thi Kiosk và Niêm phong Bằng chứng Số SHA-256",
                Content = "Phân tích kiến trúc bảo mật đa lớp từ máy trạm Kiosk phòng Lab đến lưu trữ đám mây Cloudflare R2 nhằm đảm bảo tính toàn vẹn và chống chối bỏ (Non-repudiation) của tệp ghi âm thi vấn đáp.",
                SampleAnswer = "Ngay khi thí sinh hoàn tất trả lời tại máy trạm Kiosk, hệ thống tính toán mã băm mật mã học SHA-256 trên luồng dữ liệu âm thanh gốc trước khi tải lên Cloudflare R2 theo định danh bất biến STT_MSSV.webm. Mã SHA-256 được ghi nhận tức thì vào PostgreSQL cùng vé thi để đối chiếu bất kỳ lúc nào trong quá trình hậu kiểm hoặc phúc khảo.",
                KeyPoints = "[\"Niêm phong mã băm SHA-256 ngay tại máy trạm Kiosk\",\"Quy tắc đặt tên tệp bất biến STT_MSSV.webm trên Cloudflare R2\",\"Đảm bảo tính toàn vẹn bằng chứng số phục vụ hậu kiểm và phúc khảo\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                ApprovedBy = DepartmentHeadUserId,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.Manual,
                ApprovalStatus = ApprovalStatus.NeedsRevision,
                ReviewNotes = "Đề nghị giảng viên bổ sung thêm ý về cơ chế Presigned URL có thời hạn ngắn khi tải tệp ghi âm lên Cloudflare R2 để hoàn thiện đáp án mẫu.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(14)
            },

            // =========================================================================
            // 3. MÔN SWR302 — 4 CÂU HỎI THI THẬT (2 APPROVED, 1 DRAFT, 1 SUBMITTED_FOR_REVIEW)
            // =========================================================================
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000408"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Đặc tả Yêu cầu Chức năng và Ràng buộc Nghiệp vụ cho Luồng Khóa điểm Một chiều",
                Content = "Hãy đặc tả các yêu cầu chức năng, tiền điều kiện (Preconditions), hậu điều kiện (Postconditions) và quy tắc bất biến cho nghiệp vụ Công bố điểm và Khóa điểm Một chiều (One-Way Lock).",
                SampleAnswer = "Tiền điều kiện: 100% vé thi của thí sinh trong ca thi phải ở trạng thái đã chấm điểm hoặc đã hậu kiểm. Khi giảng viên xác nhận Công bố điểm, hệ thống chuyển trạng thái toàn bộ vé thi sang PUBLISHED/LOCKED và bật cờ is_locked = true. Hậu điều kiện: Mọi thao tác UPDATE hoặc DELETE trực tiếp lên điểm số bị chặn tuyệt đối với lỗi HTTP 403 Forbidden, chỉ mở ngoại lệ duy nhất qua luồng phê duyệt đơn phúc khảo chính thức.",
                KeyPoints = "[\"Precondition: 100% bài thi trong ca đã có điểm hợp lệ\",\"Postcondition: Kích hoạt cờ bất biến is_locked = true trên toàn ca thi\",\"Exception Rule: Chỉ cho phép cập nhật điểm qua quy trình AppealRequest\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                ApprovedBy = DepartmentHeadUserId,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.Manual,
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(16)
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000409"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Phân tích Ma trận Phân quyền RBAC 5 Vai trò và Ranh giới Tác nhân (Actors)",
                Content = "Phân tích ranh giới trách nhiệm giữa 5 tác nhân (Student, Lecturer, Department Head, Proctor, Admin) trong tài liệu SRS để đảm bảo nguyên tắc Phân tách Nhiệm vụ (Separation of Duties).",
                SampleAnswer = "Nguyên tắc Separation of Duties được áp dụng chặt chẽ: Giảng viên biên soạn câu hỏi nhưng Trưởng bộ môn là người duy nhất có quyền phê duyệt đưa vào ngân hàng đề thi; Giám thị phụ trách điểm danh mở khóa ca thi tại phòng Lab nhưng không có quyền xem hay sửa điểm; Admin quản trị cấu hình kỹ thuật và tài khoản nhưng không được can thiệp vào hạn ngạch học thuật hay kết quả chấm thi.",
                KeyPoints = "[\"Tách biệt quyền biên soạn câu hỏi (GV) và quyền phê duyệt đề (Trưởng BM)\",\"Giám thị chỉ điều hành ca thi vật lý, không can thiệp điểm số học thuật\",\"Admin quản trị hạ tầng hệ thống, đảm bảo tính độc lập và minh bạch\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                ApprovedBy = DepartmentHeadUserId,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.FlmApi,
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(18)
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000410"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Kỹ thuật Phân tích Rủi ro Yêu cầu và Quản trị Yêu cầu Phi chức năng về Âm thanh",
                Content = "Trình bày cách xác định và đặc tả các yêu cầu phi chức năng nhằm giảm thiểu rủi ro nhiễu âm thanh phòng thi và sai lệch nhận dạng giọng nói thuật ngữ chuyên ngành (Code-Switching).",
                SampleAnswer = "Để giảm thiểu rủi ro âm thanh và nhận dạng STT đối với thuật ngữ tiếng Anh xen lẫn tiếng Việt, SRS quy định hai cơ chế bắt buộc: (1) Cổng kiểm tra phần cứng Microphone bắt buộc đạt ngưỡng âm lượng tối thiểu 60dB trong 30 giây trước khi vào ca thi; (2) Màn hình đệm hiệu đính văn bản (Transcript Buffer) cho phép sinh viên rà soát và sửa lỗi chính tả thuật ngữ chuyên ngành trong khoảng thời gian cấu hình từ 10 đến 300 giây.",
                KeyPoints = "[\"Kiểm tra ngưỡng phần cứng Microphone >= 60dB trước khi thi\",\"Cơ chế Transcript Buffer (10-300s) xử lý triệt để hiện tượng Code-Switching\",\"Đảm bảo tính công bằng cho thí sinh trước khi gửi văn bản cho AI chấm\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                ApprovedBy = null,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.Manual,
                ApprovalStatus = ApprovalStatus.Draft,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(20)
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000411"),
                CourseId = swrCourse.Id,
                RubricId = RubricSwr302Id,
                Title = "Thẩm định Kịch bản Ngoại lệ (Exception Flows) cho Luồng Mất kết nối Mạng Phòng thi",
                Content = "Hãy đặc tả luồng ngoại lệ (Exception Flow) trong Use Case Nộp bài thi vấn đáp khi máy trạm Kiosk gặp sự cố gián đoạn kết nối mạng cục bộ hoặc dịch vụ AI bị quá tải.",
                SampleAnswer = "Khi xảy ra sự cố gián đoạn kết nối tạm thời tại máy trạm, ứng dụng Kiosk kích hoạt cơ chế thử lại tự động có kiểm soát và giữ nguyên phiên làm việc đã khóa toàn màn hình. Ở phía máy chủ, ngay khi nhận được gói tin nộp bài, hệ thống áp dụng chiến lược Persist First lưu ngay bản ghi trạng thái SUBMITTED vào đĩa cứng trong dưới 100ms trước khi đẩy vào hàng đợi chấm ngầm, đảm bảo thí sinh không bao giờ phải thi lại từ đầu.",
                KeyPoints = "[\"Bảo toàn dữ liệu âm thanh và văn bản tại máy trạm khi mạng chập chờn\",\"Chiến lược Persist First < 100ms tách biệt luồng nộp bài khỏi luồng AI chấm điểm\",\"Cơ chế khôi phục phiên thi an toàn dưới sự giám sát của Giám thị\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                ApprovedBy = null,
                SubmittedBy = LecturerUserId,
                Source = QuestionSource.FlmApi,
                ApprovalStatus = ApprovalStatus.SubmittedForReview,
                IsActive = true,
                CreatedAt = SeedTimestampUtc.AddHours(22)
            }
        };

        var existingExamQuestions = await context.ExamQuestions.ToListAsync(cancellationToken);

        foreach (var target in targetExamQuestions)
        {
            var existing = existingExamQuestions.FirstOrDefault(q => q.Id == target.Id);
            if (existing == null)
            {
                context.ExamQuestions.Add(target);
            }
            else
            {
                existing.SubmittedBy ??= target.SubmittedBy;
                if (string.IsNullOrWhiteSpace(existing.SampleAnswer) || existing.SampleAnswer.Length < 60)
                {
                    existing.SampleAnswer = target.SampleAnswer;
                }
                if (target.Id == Guid.Parse("00000000-0000-0000-0000-000000000020"))
                {
                    existing.ApprovedBy ??= DepartmentHeadUserId;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Đảm bảo các câu hỏi APPROVED được gắn vào ExamSetQuestions của từng môn
        var existingSetQuestions = await context.ExamSetQuestions
            .Select(sq => new { sq.ExamSetId, sq.ExamQuestionId })
            .ToListAsync(cancellationToken);

        var existingPairs = existingSetQuestions
            .Select(sq => (sq.ExamSetId, sq.ExamQuestionId))
            .ToHashSet();

        var setMappings = new List<(Guid SetId, Guid QuestionId, int OrderIndex)>
        {
            (ExamSetPrn231Id, Guid.Parse("00000000-0000-0000-0000-000000000020"), 1),
            (ExamSetSwd392Id, Guid.Parse("00000000-0000-0000-0000-000000000404"), 1),
            (ExamSetSwr302Id, Guid.Parse("00000000-0000-0000-0000-000000000408"), 1),
            (ExamSetSwr302Id, Guid.Parse("00000000-0000-0000-0000-000000000409"), 2)
        };

        foreach (var (setId, questionId, orderIndex) in setMappings)
        {
            var setExists = await context.ExamSets.AnyAsync(s => s.Id == setId, cancellationToken);
            if (setExists && !existingPairs.Contains((setId, questionId)))
            {
                context.ExamSetQuestions.Add(new ExamSetQuestion
                {
                    Id = Guid.NewGuid(),
                    ExamSetId = setId,
                    ExamQuestionId = questionId,
                    OrderIndex = orderIndex
                });
                existingPairs.Add((setId, questionId));
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureOfficialExamAuditEvidenceAsync(OralExamDbContext context, CancellationToken cancellationToken)
    {
        var prnCourse = await context.Courses.FirstAsync(c => c.Code == "PRN231", cancellationToken);
        var prnStructure = await context.ExamStructures.FirstAsync(s => s.CourseId == prnCourse.Id && s.IsActive, cancellationToken);
        var prnSet = await context.ExamSets.FirstAsync(s => s.CourseId == prnCourse.Id, cancellationToken);
        var prnCriteria = await context.RubricCriteria
            .Where(c => c.RubricId == RubricPrn231Id)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        // 1. Đảm bảo OfficialExamSession tồn tại
        var officialSession = await context.OfficialExamSessions
            .FirstOrDefaultAsync(s => s.Id == OfficialSessionPrn231Id, cancellationToken);

        if (officialSession == null)
        {
            officialSession = new OfficialExamSession
            {
                Id = OfficialSessionPrn231Id,
                CourseId = prnCourse.Id,
                ExamStructureId = prnStructure.Id,
                Title = "Vấn đáp Cuối kỳ PRN231 — Ca 1 Sáng",
                ExamDate = new DateOnly(2026, 10, 15),
                HasFollowUp = true,
                MaxFollowUpQuestions = 2,
                ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
                TranscriptBufferSeconds = 60,
                Status = "auditing",
                CreatedBy = DepartmentHeadUserId,
                CreatedAt = SeedTimestampUtc
            };
            context.OfficialExamSessions.Add(officialSession);
            await context.SaveChangesAsync(cancellationToken);
        }

        // 2. Đảm bảo RealExamSessionShift (Ca thi Lab 301) tồn tại
        var shift = await context.RealExamSessionShifts
            .FirstOrDefaultAsync(s => s.Id == OfficialShiftPrn231Id, cancellationToken);

        if (shift == null)
        {
            shift = new RealExamSessionShift
            {
                Id = OfficialShiftPrn231Id,
                SessionId = officialSession.Id,
                ShiftName = "Kíp 1 (08h00 - 09h30)",
                RoomLab = "Lab 301",
                StartTime = new DateTime(2026, 10, 15, 1, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 10, 15, 2, 30, 0, DateTimeKind.Utc),
                ProctorUserId = ProctorUserId,
                Status = "completed",
                CreatedAt = SeedTimestampUtc
            };
            context.RealExamSessionShifts.Add(shift);
            await context.SaveChangesAsync(cancellationToken);
        }

        // 3. Đảm bảo 2 vé thi StudentExamTickets (SE170001 & SE170002) tồn tại và ở trạng thái AI_GRADED (nếu chưa khóa)
        var submittedTimestamp = new DateTime(2026, 10, 15, 1, 45, 0, DateTimeKind.Utc);

        var ticketHoang = await context.StudentExamTickets
            .FirstOrDefaultAsync(t => t.Id == TicketHoangId, cancellationToken);

        if (ticketHoang == null)
        {
            ticketHoang = new StudentExamTicket
            {
                Id = TicketHoangId,
                ShiftId = shift.Id,
                StudentId = StudentHoangId,
                ExamSetId = prnSet.Id,
                SeatNumber = 1,
                IpAddress = "192.168.1.101",
                Status = ExamTicketStatus.AiGraded,
                StudentAcknowledgementStatus = "PENDING",
                IsLocked = false,
                CheckedInAt = new DateTime(2026, 10, 15, 1, 5, 0, DateTimeKind.Utc),
                SubmittedAt = submittedTimestamp
            };
            context.StudentExamTickets.Add(ticketHoang);
        }
        else if (!ticketHoang.IsLocked && ticketHoang.Status == ExamTicketStatus.Scheduled)
        {
            ticketHoang.Status = ExamTicketStatus.AiGraded;
            ticketHoang.SubmittedAt ??= submittedTimestamp;
        }

        var ticketMai = await context.StudentExamTickets
            .FirstOrDefaultAsync(t => t.Id == TicketMaiId, cancellationToken);

        if (ticketMai == null)
        {
            ticketMai = new StudentExamTicket
            {
                Id = TicketMaiId,
                ShiftId = shift.Id,
                StudentId = StudentMaiId,
                ExamSetId = prnSet.Id,
                SeatNumber = 2,
                IpAddress = "192.168.1.102",
                Status = ExamTicketStatus.AiGraded,
                StudentAcknowledgementStatus = "PENDING",
                IsLocked = false,
                CheckedInAt = new DateTime(2026, 10, 15, 1, 6, 0, DateTimeKind.Utc),
                SubmittedAt = submittedTimestamp.AddMinutes(3)
            };
            context.StudentExamTickets.Add(ticketMai);
        }
        else if (!ticketMai.IsLocked && ticketMai.Status == ExamTicketStatus.Scheduled)
        {
            ticketMai.Status = ExamTicketStatus.AiGraded;
            ticketMai.SubmittedAt ??= submittedTimestamp.AddMinutes(3);
        }

        await context.SaveChangesAsync(cancellationToken);

        // 4. Đảm bảo ExamQuestionSubmissions & AiEvaluations cho 2 vé thi
        var examQuestionId = Guid.Parse("00000000-0000-0000-0000-000000000020");
        var submissionHoangId = Guid.Parse("00000000-0000-0000-0000-000000000901");
        var submissionMaiId = Guid.Parse("00000000-0000-0000-0000-000000000902");

        var hasHoangSubmission = await context.ExamQuestionSubmissions
            .AnyAsync(s => s.Id == submissionHoangId || s.TicketId == TicketHoangId, cancellationToken);

        if (!hasHoangSubmission && prnCriteria.Count >= 3)
        {
            var submissionHoang = new ExamQuestionSubmission
            {
                Id = submissionHoangId,
                TicketId = TicketHoangId,
                ExamQuestionId = examQuestionId,
                AudioR2Url = "https://r2.oralexam.fpt.edu.vn/fa26/prn231/01_SE170001.webm",
                AudioHashSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                TranscriptWhisper = "Thưa hội đồng, trong .NET 8 thì IHostedService là interface cơ bản có hai hàm StartAsync và StopAsync. Còn BackgroundService là lớp trừu tượng kế thừa từ IHostedService, cung cấp hàm ExecuteAsync để chạy vòng lặp xử lý hàng đợi chấm điểm ngầm... (có tiếng ồn quạt phòng Lab làm nhiễu đoạn giải thích về CancellationToken và Scoped DbContext).",
                TimestampsWhisper = "[{\"start\":0.0,\"end\":12.5,\"text\":\"Thưa hội đồng, trong .NET 8 thì IHostedService là interface cơ bản có hai hàm StartAsync và StopAsync.\"},{\"start\":12.6,\"end\":28.0,\"text\":\"Còn BackgroundService là lớp trừu tượng kế thừa từ IHostedService, cung cấp hàm ExecuteAsync để chạy vòng lặp xử lý hàng đợi chấm điểm ngầm.\"}]",
                TimeSpentSeconds = 165,
                AiScore = 6.80m,
                FinalScore = 6.80m,
                GradingStatus = "ai_graded",
                SubmittedAt = submittedTimestamp
            };
            context.ExamQuestionSubmissions.Add(submissionHoang);

            var evalHoangId = Guid.Parse("00000000-0000-0000-0000-000000000911");
            var evalHoangDetails = BuildEvaluationDetails(evalHoangId, prnCriteria, 6.80m);

            var evalHoang = new AiEvaluation
            {
                Id = evalHoangId,
                AnswerType = "exam",
                PracticeAnswerId = null,
                ExamSubmissionId = submissionHoangId,
                MockExamAnswerId = null,
                TotalScore = 6.80m,
                ConfidenceScore = 0.64m, // < 0.70m -> kích hoạt AI Doubt Guard (Nhóm 1: Cần xem lại/Nghi ngờ)
                Feedback = "Thí sinh phân biệt đúng IHostedService và BackgroundService nhưng phần giải thích về quản lý vòng đời Scoped DbContext và StoppingToken bị ngắt quãng do tạp âm môi trường.",
                CotTrace = "[Bước 1 - Phân tích Transcript]: Thí sinh nêu đúng IHostedService (StartAsync/StopAsync) và BackgroundService (ExecuteAsync). [Bước 2 - Đối chiếu Barem 10.0]: Tiêu chí 1 & 2 đạt mức khá, Tiêu chí 3 thiếu chi tiết xử lý ngoại lệ trong vòng lặp nền do âm thanh bị nhiễu ở giây thứ 28. [Bước 3 - Độ tin cậy]: Confidence = 0.64 (< 0.70) -> Đánh dấu nghi ngờ để Giảng viên nghe lại băng ghi âm gốc.",
                EvaluatedAt = submittedTimestamp.AddSeconds(12),
                Details = evalHoangDetails
            };
            context.AiEvaluations.Add(evalHoang);
        }

        var hasMaiSubmission = await context.ExamQuestionSubmissions
            .AnyAsync(s => s.Id == submissionMaiId || s.TicketId == TicketMaiId, cancellationToken);

        if (!hasMaiSubmission && prnCriteria.Count >= 3)
        {
            var submissionMai = new ExamQuestionSubmission
            {
                Id = submissionMaiId,
                TicketId = TicketMaiId,
                ExamQuestionId = examQuestionId,
                AudioR2Url = "https://r2.oralexam.fpt.edu.vn/fa26/prn231/02_SE170002.webm",
                AudioHashSha256 = "8f434346648f6b96df89dda901c5176b10a6d83961dd3c1ac88b59b2dc327aa4",
                TranscriptWhisper = "Em xin trình bày sự khác biệt giữa IHostedService và BackgroundService trong .NET 8: IHostedService là interface định nghĩa hợp đồng StartAsync và StopAsync. BackgroundService là abstract base class triển khai IHostedService, gói gọn luồng thực thi bất đồng bộ dài hạn trong ExecuteAsync(CancellationToken stoppingToken). Khi xây dựng GradingQueueWorker đọc từ BoundedChannel 1000 slots, em kế thừa BackgroundService, dùng IServiceScopeFactory tạo scope riêng cho mỗi lần gọi DbContext và bọc Polly Retry 3 lần (2s, 4s, 8s) trước khi đẩy lỗi vào Dead-Letter Queue.",
                TimestampsWhisper = "[{\"start\":0.0,\"end\":18.0,\"text\":\"Em xin trình bày sự khác biệt giữa IHostedService và BackgroundService trong .NET 8: IHostedService là interface định nghĩa hợp đồng StartAsync và StopAsync.\"},{\"start\":18.1,\"end\":45.0,\"text\":\"Khi xây dựng GradingQueueWorker đọc từ BoundedChannel 1000 slots, em kế thừa BackgroundService, dùng IServiceScopeFactory tạo scope riêng và bọc Polly Retry 3 lần.\"}]",
                TimeSpentSeconds = 190,
                AiScore = 8.75m,
                FinalScore = 8.75m,
                GradingStatus = "ai_graded",
                SubmittedAt = submittedTimestamp.AddMinutes(3)
            };
            context.ExamQuestionSubmissions.Add(submissionMai);

            var evalMaiId = Guid.Parse("00000000-0000-0000-0000-000000000912");
            var evalMaiDetails = BuildEvaluationDetails(evalMaiId, prnCriteria, 8.75m);

            var evalMai = new AiEvaluation
            {
                Id = evalMaiId,
                AnswerType = "exam",
                PracticeAnswerId = null,
                ExamSubmissionId = submissionMaiId,
                MockExamAnswerId = null,
                TotalScore = 8.75m,
                ConfidenceScore = 0.92m, // >= 0.70m -> Nhóm 2: Tin cậy cao
                Feedback = "Câu trả lời rất xuất sắc, nắm vững bản chất IHostedService/BackgroundService, cách dùng IServiceScopeFactory tránh Captive Dependency và liên hệ thực tế với BoundedChannel + Polly Retry.",
                CotTrace = "[Bước 1 - Phân tích Transcript]: Thí sinh trình bày mạch lạc, chính xác toàn bộ KeyPoints. [Bước 2 - Đối chiếu Barem 10.0]: Đạt điểm cao ở cả 3 tiêu chí Clean Architecture, Dependency Injection Scope và Resilience Pipeline. [Bước 3 - Độ tin cậy]: Confidence = 0.92 (Tin cậy cao).",
                EvaluatedAt = submittedTimestamp.AddMinutes(3).AddSeconds(10),
                Details = evalMaiDetails
            };
            context.AiEvaluations.Add(evalMai);
        }

        await context.SaveChangesAsync(cancellationToken);

        // 5. Đảm bảo đơn phúc khảo AppealRequest (00000000-0000-0000-0000-000000000026) tồn tại và gắn SubmissionId
        var appeal = await context.AppealRequests
            .FirstOrDefaultAsync(a => a.Id == AppealHoangId, cancellationToken);

        var actualHoangSub = await context.ExamQuestionSubmissions
            .FirstOrDefaultAsync(s => s.TicketId == TicketHoangId, cancellationToken);

        if (appeal == null)
        {
            context.AppealRequests.Add(new AppealRequest
            {
                Id = AppealHoangId,
                TicketId = TicketHoangId,
                StudentId = StudentHoangId,
                SessionId = officialSession.Id,
                SubmissionId = actualHoangSub?.Id,
                Reason = "Em xin phúc khảo phần phát biểu về BackgroundService và Dependency Inversion do thời điểm thu âm quạt thông gió phòng Lab 301 bị ồn làm Whisper nhận diện thiếu ý ở cuối câu.",
                Status = AppealStatus.Pending,
                AssignedTo = LecturerUserId,
                OriginalScore = 6.80m,
                CreatedAt = submittedTimestamp.AddHours(2),
                UpdatedAt = submittedTimestamp.AddHours(2)
            });
        }
        else if (appeal.SubmissionId == null && actualHoangSub != null)
        {
            appeal.SubmissionId = actualHoangSub.Id;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
