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

    // ENT (English Speaking) Courses
    public static readonly Guid CourseEnt001Id = Guid.Parse("00000000-0000-0000-0000-000000000e01");
    public static readonly Guid CourseEnt104Id = Guid.Parse("00000000-0000-0000-0000-000000000e02");
    public static readonly Guid CourseEnt203Id = Guid.Parse("00000000-0000-0000-0000-000000000e03");
    public static readonly Guid CourseEnt303Id = Guid.Parse("00000000-0000-0000-0000-000000000e04");
    public static readonly Guid CourseEnt403Id = Guid.Parse("00000000-0000-0000-0000-000000000e05");
    public static readonly Guid CourseEnt503Id = Guid.Parse("00000000-0000-0000-0000-000000000e06");

    public static readonly Guid RubricPrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000013");
    public static readonly Guid RubricSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid RubricSwr302Id = Guid.Parse("00000000-0000-0000-0000-000000000301");

    // ENT (English Speaking) Rubrics (Standardized 10.0-point FPTU Speaking Rubric)
    public static readonly Guid RubricEnt001Id = Guid.Parse("00000000-0000-0000-0000-000000000e11");
    public static readonly Guid RubricEnt104Id = Guid.Parse("00000000-0000-0000-0000-000000000e12");
    public static readonly Guid RubricEnt203Id = Guid.Parse("00000000-0000-0000-0000-000000000e13");
    public static readonly Guid RubricEnt303Id = Guid.Parse("00000000-0000-0000-0000-000000000e14");
    public static readonly Guid RubricEnt403Id = Guid.Parse("00000000-0000-0000-0000-000000000e15");
    public static readonly Guid RubricEnt503Id = Guid.Parse("00000000-0000-0000-0000-000000000e16");

    public static readonly Guid StructurePrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000018");
    public static readonly Guid StructureSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000205");
    public static readonly Guid StructureSwr302Id = Guid.Parse("00000000-0000-0000-0000-000000000305");
    public static readonly Guid StructureEnt001Id = Guid.Parse("00000000-0000-0000-0000-000000000e91");
    public static readonly Guid StructureEnt104Id = Guid.Parse("00000000-0000-0000-0000-000000000e92");
    public static readonly Guid StructureEnt203Id = Guid.Parse("00000000-0000-0000-0000-000000000e93");
    public static readonly Guid StructureEnt303Id = Guid.Parse("00000000-0000-0000-0000-000000000e94");
    public static readonly Guid StructureEnt403Id = Guid.Parse("00000000-0000-0000-0000-000000000e95");
    public static readonly Guid StructureEnt503Id = Guid.Parse("00000000-0000-0000-0000-000000000e96");

    public static readonly Guid ClassPrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public static readonly Guid ClassSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000206");
    public static readonly Guid ClassSwr302Id = Guid.Parse("00000000-0000-0000-0000-000000000306");
    public static readonly Guid ClassEnt001Id = Guid.Parse("00000000-0000-0000-0000-000000000eb1");
    public static readonly Guid ClassEnt104Id = Guid.Parse("00000000-0000-0000-0000-000000000eb2");
    public static readonly Guid ClassEnt203Id = Guid.Parse("00000000-0000-0000-0000-000000000eb3");
    public static readonly Guid ClassEnt303Id = Guid.Parse("00000000-0000-0000-0000-000000000eb4");
    public static readonly Guid ClassEnt403Id = Guid.Parse("00000000-0000-0000-0000-000000000eb5");
    public static readonly Guid ClassEnt503Id = Guid.Parse("00000000-0000-0000-0000-000000000eb6");

    public static readonly Guid ExamSetPrn231Id = Guid.Parse("00000000-0000-0000-0000-000000000019");
    public static readonly Guid ExamSetSwd392Id = Guid.Parse("00000000-0000-0000-0000-000000000207");
    public static readonly Guid ExamSetSwr302Id = Guid.Parse("00000000-0000-0000-0000-000000000307");
    public static readonly Guid ExamSetEnt001Id = Guid.Parse("00000000-0000-0000-0000-000000000ea1");
    public static readonly Guid ExamSetEnt104Id = Guid.Parse("00000000-0000-0000-0000-000000000ea2");
    public static readonly Guid ExamSetEnt203Id = Guid.Parse("00000000-0000-0000-0000-000000000ea3");
    public static readonly Guid ExamSetEnt303Id = Guid.Parse("00000000-0000-0000-0000-000000000ea4");
    public static readonly Guid ExamSetEnt403Id = Guid.Parse("00000000-0000-0000-0000-000000000ea5");
    public static readonly Guid ExamSetEnt503Id = Guid.Parse("00000000-0000-0000-0000-000000000ea6");

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
                if (string.IsNullOrWhiteSpace(existing.PasswordHash) || existing.PasswordHash == user.PasswordHash)
                {
                    existing.MustChangePassword = user.MustChangePassword;
                }
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
            },
            new()
            {
                Id = CourseEnt001Id,
                Code = "ENT001",
                Name = "English Preparation 1",
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
                Id = CourseEnt104Id,
                Code = "ENT104",
                Name = "English Preparation 2",
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
                Id = CourseEnt203Id,
                Code = "ENT203",
                Name = "English Intermediate 1",
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
                Id = CourseEnt303Id,
                Code = "ENT303",
                Name = "English Intermediate 2",
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
                Id = CourseEnt403Id,
                Code = "ENT403",
                Name = "English Advanced 1",
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
                Id = CourseEnt503Id,
                Code = "ENT503",
                Name = "English Advanced 2",
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
        var ent001Course = await context.Courses.FirstAsync(c => c.Code == "ENT001", cancellationToken);
        var ent104Course = await context.Courses.FirstAsync(c => c.Code == "ENT104", cancellationToken);
        var ent203Course = await context.Courses.FirstAsync(c => c.Code == "ENT203", cancellationToken);
        var ent303Course = await context.Courses.FirstAsync(c => c.Code == "ENT303", cancellationToken);
        var ent403Course = await context.Courses.FirstAsync(c => c.Code == "ENT403", cancellationToken);
        var ent503Course = await context.Courses.FirstAsync(c => c.Code == "ENT503", cancellationToken);

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
            },
            new()
            {
                Id = RubricEnt001Id,
                CourseId = ent001Course.Id,
                Name = "Barem Đánh Giá Vấn Đáp Tiếng Anh ENT001 (FPTU Standard Speaking Rubric)",
                Description = "Đánh giá 5 tiêu chuẩn kỹ năng nói tiếng Anh theo chuẩn FPTU (Ideas, Pronunciation, Grammar, Vocabulary, Fluency) thang điểm 10.0",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricEnt104Id,
                CourseId = ent104Course.Id,
                Name = "Barem Đánh Giá Vấn Đáp Tiếng Anh ENT104 (FPTU Standard Speaking Rubric)",
                Description = "Đánh giá 5 tiêu chuẩn kỹ năng nói tiếng Anh theo chuẩn FPTU (Ideas, Pronunciation, Grammar, Vocabulary, Fluency) thang điểm 10.0",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricEnt203Id,
                CourseId = ent203Course.Id,
                Name = "Barem Đánh Giá Vấn Đáp Tiếng Anh ENT203 (FPTU Standard Speaking Rubric)",
                Description = "Đánh giá 5 tiêu chuẩn kỹ năng nói tiếng Anh theo chuẩn FPTU (Ideas, Pronunciation, Grammar, Vocabulary, Fluency) thang điểm 10.0",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricEnt303Id,
                CourseId = ent303Course.Id,
                Name = "Barem Đánh Giá Vấn Đáp Tiếng Anh ENT303 (FPTU Standard Speaking Rubric)",
                Description = "Đánh giá 5 tiêu chuẩn kỹ năng nói tiếng Anh theo chuẩn FPTU (Ideas, Pronunciation, Grammar, Vocabulary, Fluency) thang điểm 10.0",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricEnt403Id,
                CourseId = ent403Course.Id,
                Name = "Barem Đánh Giá Vấn Đáp Tiếng Anh ENT403 (FPTU Standard Speaking Rubric)",
                Description = "Đánh giá 5 tiêu chuẩn kỹ năng nói tiếng Anh theo chuẩn FPTU (Ideas, Pronunciation, Grammar, Vocabulary, Fluency) thang điểm 10.0",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = RubricEnt503Id,
                CourseId = ent503Course.Id,
                Name = "Barem Đánh Giá Vấn Đáp Tiếng Anh ENT503 (FPTU Standard Speaking Rubric)",
                Description = "Đánh giá 5 tiêu chuẩn kỹ năng nói tiếng Anh theo chuẩn FPTU (Ideas, Pronunciation, Grammar, Vocabulary, Fluency) thang điểm 10.0",
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
            },

            // =========================================================================
            // ENT001 - ENT503 Standardized 10.0-point FPTU Speaking Rubric Criteria (5 criteria x 2.00 = 10.00m)
            // =========================================================================
            // ENT001 Criteria
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e21"),
                RubricId = RubricEnt001Id,
                CriterionName = "Ideas & Content Development",
                Description = "Express ideas clearly with relevant details, examples, and logical development answering the prompt questions.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e22"),
                RubricId = RubricEnt001Id,
                CriterionName = "Pronunciation",
                Description = "Clear pronunciation, correct word stress, natural intonation, and comprehensible connected speech.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e23"),
                RubricId = RubricEnt001Id,
                CriterionName = "Grammar & Accuracy",
                Description = "Use of appropriate grammatical structures, correct tenses, subject-verb agreement, and sentence complexity.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e24"),
                RubricId = RubricEnt001Id,
                CriterionName = "Vocabulary & Lexical Resource",
                Description = "Wide and accurate range of vocabulary, topic-appropriate expressions, idioms, and collocations.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 4,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e25"),
                RubricId = RubricEnt001Id,
                CriterionName = "Fluency & Cohesion",
                Description = "Smooth flow of speech, natural pacing without excessive hesitation, and effective use of cohesive devices.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 5,
                CreatedAt = SeedTimestampUtc
            },

            // ENT104 Criteria
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e31"),
                RubricId = RubricEnt104Id,
                CriterionName = "Ideas & Content Development",
                Description = "Express ideas clearly with relevant details, examples, and logical development answering the prompt questions.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e32"),
                RubricId = RubricEnt104Id,
                CriterionName = "Pronunciation",
                Description = "Clear pronunciation, correct word stress, natural intonation, and comprehensible connected speech.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e33"),
                RubricId = RubricEnt104Id,
                CriterionName = "Grammar & Accuracy",
                Description = "Use of appropriate grammatical structures, correct tenses, subject-verb agreement, and sentence complexity.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e34"),
                RubricId = RubricEnt104Id,
                CriterionName = "Vocabulary & Lexical Resource",
                Description = "Wide and accurate range of vocabulary, topic-appropriate expressions, idioms, and collocations.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 4,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e35"),
                RubricId = RubricEnt104Id,
                CriterionName = "Fluency & Cohesion",
                Description = "Smooth flow of speech, natural pacing without excessive hesitation, and effective use of cohesive devices.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 5,
                CreatedAt = SeedTimestampUtc
            },

            // ENT203 Criteria
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e41"),
                RubricId = RubricEnt203Id,
                CriterionName = "Ideas & Content Development",
                Description = "Express ideas clearly with relevant details, examples, and logical development answering the prompt questions.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e42"),
                RubricId = RubricEnt203Id,
                CriterionName = "Pronunciation",
                Description = "Clear pronunciation, correct word stress, natural intonation, and comprehensible connected speech.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e43"),
                RubricId = RubricEnt203Id,
                CriterionName = "Grammar & Accuracy",
                Description = "Use of appropriate grammatical structures, correct tenses, subject-verb agreement, and sentence complexity.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e44"),
                RubricId = RubricEnt203Id,
                CriterionName = "Vocabulary & Lexical Resource",
                Description = "Wide and accurate range of vocabulary, topic-appropriate expressions, idioms, and collocations.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 4,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e45"),
                RubricId = RubricEnt203Id,
                CriterionName = "Fluency & Cohesion",
                Description = "Smooth flow of speech, natural pacing without excessive hesitation, and effective use of cohesive devices.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 5,
                CreatedAt = SeedTimestampUtc
            },

            // ENT303 Criteria
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e51"),
                RubricId = RubricEnt303Id,
                CriterionName = "Ideas & Content Development",
                Description = "Express ideas clearly with relevant details, examples, and logical development answering the prompt questions.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e52"),
                RubricId = RubricEnt303Id,
                CriterionName = "Pronunciation",
                Description = "Clear pronunciation, correct word stress, natural intonation, and comprehensible connected speech.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e53"),
                RubricId = RubricEnt303Id,
                CriterionName = "Grammar & Accuracy",
                Description = "Use of appropriate grammatical structures, correct tenses, subject-verb agreement, and sentence complexity.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e54"),
                RubricId = RubricEnt303Id,
                CriterionName = "Vocabulary & Lexical Resource",
                Description = "Wide and accurate range of vocabulary, topic-appropriate expressions, idioms, and collocations.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 4,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e55"),
                RubricId = RubricEnt303Id,
                CriterionName = "Fluency & Cohesion",
                Description = "Smooth flow of speech, natural pacing without excessive hesitation, and effective use of cohesive devices.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 5,
                CreatedAt = SeedTimestampUtc
            },

            // ENT403 Criteria
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e61"),
                RubricId = RubricEnt403Id,
                CriterionName = "Ideas & Content Development",
                Description = "Express ideas clearly with relevant details, examples, and logical development answering the prompt questions.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e62"),
                RubricId = RubricEnt403Id,
                CriterionName = "Pronunciation",
                Description = "Clear pronunciation, correct word stress, natural intonation, and comprehensible connected speech.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e63"),
                RubricId = RubricEnt403Id,
                CriterionName = "Grammar & Accuracy",
                Description = "Use of appropriate grammatical structures, correct tenses, subject-verb agreement, and sentence complexity.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e64"),
                RubricId = RubricEnt403Id,
                CriterionName = "Vocabulary & Lexical Resource",
                Description = "Wide and accurate range of vocabulary, topic-appropriate expressions, idioms, and collocations.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 4,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e65"),
                RubricId = RubricEnt403Id,
                CriterionName = "Fluency & Cohesion",
                Description = "Smooth flow of speech, natural pacing without excessive hesitation, and effective use of cohesive devices.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 5,
                CreatedAt = SeedTimestampUtc
            },

            // ENT503 Criteria
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e71"),
                RubricId = RubricEnt503Id,
                CriterionName = "Ideas & Content Development",
                Description = "Express ideas clearly with relevant details, examples, and logical development answering the prompt questions.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Understand,
                OrderIndex = 1,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e72"),
                RubricId = RubricEnt503Id,
                CriterionName = "Pronunciation",
                Description = "Clear pronunciation, correct word stress, natural intonation, and comprehensible connected speech.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 2,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e73"),
                RubricId = RubricEnt503Id,
                CriterionName = "Grammar & Accuracy",
                Description = "Use of appropriate grammatical structures, correct tenses, subject-verb agreement, and sentence complexity.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 3,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e74"),
                RubricId = RubricEnt503Id,
                CriterionName = "Vocabulary & Lexical Resource",
                Description = "Wide and accurate range of vocabulary, topic-appropriate expressions, idioms, and collocations.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 4,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e75"),
                RubricId = RubricEnt503Id,
                CriterionName = "Fluency & Cohesion",
                Description = "Smooth flow of speech, natural pacing without excessive hesitation, and effective use of cohesive devices.",
                MaxScore = 2.00m,
                Weight = 0.20m,
                BloomLevel = BloomLevel.Apply,
                OrderIndex = 5,
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
        var ent001Course = await context.Courses.FirstAsync(c => c.Code == "ENT001", cancellationToken);
        var ent104Course = await context.Courses.FirstAsync(c => c.Code == "ENT104", cancellationToken);
        var ent203Course = await context.Courses.FirstAsync(c => c.Code == "ENT203", cancellationToken);
        var ent303Course = await context.Courses.FirstAsync(c => c.Code == "ENT303", cancellationToken);
        var ent403Course = await context.Courses.FirstAsync(c => c.Code == "ENT403", cancellationToken);
        var ent503Course = await context.Courses.FirstAsync(c => c.Code == "ENT503", cancellationToken);

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
            },
            new()
            {
                Id = StructureEnt001Id,
                CourseId = ent001Course.Id,
                CreatedBy = creatorId,
                Name = "FPTU Speaking Test Structure ENT001 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 20,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureEnt104Id,
                CourseId = ent104Course.Id,
                CreatedBy = creatorId,
                Name = "FPTU Speaking Test Structure ENT104 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 20,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureEnt203Id,
                CourseId = ent203Course.Id,
                CreatedBy = creatorId,
                Name = "FPTU Speaking Test Structure ENT203 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 20,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureEnt303Id,
                CourseId = ent303Course.Id,
                CreatedBy = creatorId,
                Name = "FPTU Speaking Test Structure ENT303 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 20,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureEnt403Id,
                CourseId = ent403Course.Id,
                CreatedBy = creatorId,
                Name = "FPTU Speaking Test Structure ENT403 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 20,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = StructureEnt503Id,
                CourseId = ent503Course.Id,
                CreatedBy = creatorId,
                Name = "FPTU Speaking Test Structure ENT503 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 20,
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
        var actualEnt001Struct = await context.ExamStructures.FirstAsync(s => s.CourseId == ent001Course.Id && s.IsActive, cancellationToken);
        var actualEnt104Struct = await context.ExamStructures.FirstAsync(s => s.CourseId == ent104Course.Id && s.IsActive, cancellationToken);
        var actualEnt203Struct = await context.ExamStructures.FirstAsync(s => s.CourseId == ent203Course.Id && s.IsActive, cancellationToken);
        var actualEnt303Struct = await context.ExamStructures.FirstAsync(s => s.CourseId == ent303Course.Id && s.IsActive, cancellationToken);
        var actualEnt403Struct = await context.ExamStructures.FirstAsync(s => s.CourseId == ent403Course.Id && s.IsActive, cancellationToken);
        var actualEnt503Struct = await context.ExamStructures.FirstAsync(s => s.CourseId == ent503Course.Id && s.IsActive, cancellationToken);

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
            },
            new()
            {
                Id = ExamSetEnt001Id,
                StructureId = actualEnt001Struct.Id,
                CourseId = ent001Course.Id,
                SetCode = "ENT001-SET01",
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetEnt104Id,
                StructureId = actualEnt104Struct.Id,
                CourseId = ent104Course.Id,
                SetCode = "ENT104-SET01",
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetEnt203Id,
                StructureId = actualEnt203Struct.Id,
                CourseId = ent203Course.Id,
                SetCode = "ENT203-SET01",
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetEnt303Id,
                StructureId = actualEnt303Struct.Id,
                CourseId = ent303Course.Id,
                SetCode = "ENT303-SET01",
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetEnt403Id,
                StructureId = actualEnt403Struct.Id,
                CourseId = ent403Course.Id,
                SetCode = "ENT403-SET01",
                GeneratedAt = SeedTimestampUtc,
                IsActive = true
            },
            new()
            {
                Id = ExamSetEnt503Id,
                StructureId = actualEnt503Struct.Id,
                CourseId = ent503Course.Id,
                SetCode = "ENT503-SET01",
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
        var ent001Course = await context.Courses.FirstAsync(c => c.Code == "ENT001", cancellationToken);
        var ent104Course = await context.Courses.FirstAsync(c => c.Code == "ENT104", cancellationToken);
        var ent203Course = await context.Courses.FirstAsync(c => c.Code == "ENT203", cancellationToken);
        var ent303Course = await context.Courses.FirstAsync(c => c.Code == "ENT303", cancellationToken);
        var ent403Course = await context.Courses.FirstAsync(c => c.Code == "ENT403", cancellationToken);
        var ent503Course = await context.Courses.FirstAsync(c => c.Code == "ENT503", cancellationToken);

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
            },

            // =========================================================================
            // 4. MÔN TIẾNG ANH ENT001 - ENT503 (CÂU HỎI THỰC TẾ THEO GUIDELINE FPTU)
            // =========================================================================
            // ENT001 - Part 1 Introduction
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e81"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 1 - Daily Activities and Personal Introduction",
                Content = "Topic: Activities. Please answer the following 5 personal questions in complete sentences:\n1. Where are you from?\n2. What are your favorite clothes to school?\n3. What time do you eat breakfast?\n4. What is your favorite leisure activity?\n5. What is your typical day like?",
                SampleAnswer = "I am from Da Nang city, Vietnam. When I go to school, I like wearing a comfortable white T-shirt and jeans with sneakers. I usually eat breakfast at around 7:00 AM before heading to class, having either pho or a banh mi. In my free time, my favorite leisure activity is listening to pop music and playing badminton with my friends. On a typical day, I attend university lectures in the morning, review study materials in the afternoon, and relax with my family in the evening.",
                KeyPoints = "[\"Answer all 5 questions using complete sentences\",\"Give specific details about hometown, clothing, breakfast time, leisure, and daily routine\",\"Demonstrate correct simple present tense and daily vocabulary\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Do you prefer spending your free time outdoors with friends or staying at home by yourself? Why?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            // ENT001 - Part 2 Picture Description (Multimodal Vision)
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e82"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 2 - Laundry Activity Picture Description",
                Content = "Look at the picture and describe what you see using the following guide questions:\n1. Who are they?\n2. Where are they?\n3. What are they wearing?\n4. What are they doing?",
                SampleAnswer = "In the picture, I see a mother and her young toddler daughter. They are in the laundry room or kitchen at home doing household chores. The mother is kneeling on the floor, wearing a casual light-colored top, and smiling warmly. The little toddler is standing next to her wearing colorful clothes. They are doing the laundry together in front of an open white front-loading washing machine. The child is reaching into a laundry basket filled with clean clothes, helping her mother put clothes into the machine.",
                KeyPoints = "[\"Identify the mother and young child in a domestic home setting\",\"Describe the white front-loading washing machine and laundry basket\",\"Describe their casual clothing and cooperative action of doing laundry\",\"Use present continuous tense accurately\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Do you often help your family with household chores like doing laundry or cleaning? What chores do you enjoy or dislike?",
                ImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png",
                ImageDescription = "A mother and young toddler doing laundry together in front of a white front-loading washing machine in a domestic room. The mother is kneeling on the wooden floor wearing casual clothes, smiling while holding colorful laundry. The toddler stands nearby reaching into a white laundry basket filled with clothes. The setting is bright and domestic with home appliances visible.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // ENT104 - Part 1 Introduction
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e83"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 1 - Getting Acquainted and Daily Life",
                Content = "Topic: Getting acquainted. Please answer the following questions:\n1. Where is your hometown?\n2. Who is your favorite actor?\n3. What job does your father or mother do?\n4. How often do you go out with your friends?\n5. When you go out with your friends, where do you usually go?",
                SampleAnswer = "My hometown is Can Tho, a peaceful city in the Mekong Delta region. My favorite actor is Tom Hanks because of his incredible versatility and inspiring performance in Forrest Gump. My father works as a civil engineer and my mother is a high school mathematics teacher. I usually hang out with my close university friends about twice a week, typically on Friday evenings or over the weekend. When we meet, we often go to a quiet coffee shop to chat or visit the cinema to watch newly released movies.",
                KeyPoints = "[\"Address hometown, favorite actor, parents' occupations, frequency of social outings, and preferred hangout spots\",\"Use adverbs of frequency (usually, twice a week) and descriptive reasons\",\"Speak with natural fluency and appropriate sentence connectors\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Why do you think maintaining friendships is important for university students during busy study periods?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            // ENT104 - Part 2 Picture Description (Multimodal Vision)
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e84"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 2 - Professional Business Meeting Picture Description",
                Content = "Look at the picture and describe what you see using the following guide questions:\n1. How many people are there in the picture?\n2. Where do you think they are?\n3. Who do you think they are?\n4. What do you think they are talking about?",
                SampleAnswer = "In the picture, there are five professional colleagues in a bright, modern corporate conference room with large glass walls and contemporary office decor. Three men and two women are gathered around a wooden conference table with open laptops and notepads. One man is standing near a whiteboard, holding a smartphone and actively gesturing as he presents an idea or product update. The other four team members are dressed in smart business attire and listening attentively. They appear to be discussing a business project, quarterly sales performance, or a new digital product launch.",
                KeyPoints = "[\"Count exactly five professionals in the conference room\",\"Identify the modern office setting with glass walls and meeting table\",\"Describe the presenter holding a smartphone and gesturing\",\"Explain likely discussion topics (business strategy, quarterly review, product update)\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "In your opinion, what makes a business meeting effective and productive versus a waste of time?",
                ImageUrl = "/uploads/questions/ent/ENT104_p2_Im1.png",
                ImageDescription = "A professional corporate business meeting in a modern conference room with glass walls. Five business colleagues (three men and two women) are seated around a wooden conference table with laptops and notebooks. A male presenter stands near a white board holding a smartphone and gesturing, while the others listen attentively in professional business attire.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // ENT203 - Part 1 Role-Play (Scenario 1 - Da Nang School Bus Trip)
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e85"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 1 - School Bus Trip to Da Nang (Information Transfer)",
                Content = "Role-Play Scenario: You are planning a school bus trip to Da Nang with your partner. Refer to the information card and answer your partner's questions:\n- Where is the trip going?\n- When is the departure time and meeting point?\n- What activities are planned during the trip?\n- What time and where is lunch served?\n- What items should participants bring?",
                SampleAnswer = "According to the trip poster, our destination is Da Nang city. We will depart on Saturday morning at 6:30 AM sharp from the main school campus bus stop. During the trip, we will visit Ba Na Hills, swim at My Khe Beach, and explore the scenic Marble Mountains. Lunch will be provided at 12:30 PM at a local seafood restaurant near the beach. Every participant should bring their student ID card, comfortable walking shoes, sunscreen, a swimming suit, and a camera or smartphone for photos. The total ticket price is 350,000 VND per student.",
                KeyPoints = "[\"State destination (Da Nang) and departure time (6:30 AM Saturday)\",\"List key activities: Ba Na Hills, My Khe Beach, Marble Mountains\",\"State lunch time (12:30 PM) and required personal items (ID, sunscreen, shoes, swimsuit)\",\"Use polite conversational questions and answers\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "If the weather turns rainy on Saturday, how should the organizers adjust the itinerary to keep the trip enjoyable?",
                ImageUrl = "/uploads/questions/ent/ENT203_p2_IM28.png",
                ImageDescription = "An informational poster for a School Bus Trip to Da Nang. The poster features key details: Destination: Da Nang city; Departure: Saturday morning at 6:30 AM from school campus; Activities: visiting Ba Na Hills, swimming at My Khe Beach, exploring Marble Mountains; Lunch: 12:30 PM at a local seafood restaurant; Things to bring: student ID card, comfortable walking shoes, sunscreen, swimming suit, and camera; Ticket price: 350,000 VND.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            // ENT203 - Part 1 Role-Play (Scenario 2 - Hanoi City Tour)
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e86"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 1 - Hanoi City Tour (Information Transfer)",
                Content = "Role-Play Scenario: You are inquiring about or explaining the Hanoi City Tour. Refer to the informational flyer and address the key details:\n- When is the tour held?\n- Where do participants meet?\n- What sights and attractions will you see?\n- What is the cost per person and what does it include?\n- How can students sign up?",
                SampleAnswer = "The Hanoi City Tour runs every weekend starting promptly at 8:00 AM. All participants will meet in front of the historic Hanoi Opera House. The tour covers iconic cultural landmarks including the Ho Chi Minh Mausoleum, the Temple of Literature, an Old Quarter walking tour, and Hoan Kiem Lake. The cost is 250,000 VND per student, which includes English-speaking tour guides and all entrance fees. Students can sign up online through the student portal or register directly at Room 204 of the Student Affairs office before Friday afternoon.",
                KeyPoints = "[\"State tour schedule (weekends at 8:00 AM) and meeting point (Hanoi Opera House)\",\"Highlight cultural attractions (Ho Chi Minh Mausoleum, Temple of Literature, Hoan Kiem Lake)\",\"Clarify cost (250,000 VND) and registration methods (online or Room 204)\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Why do you think cultural tours like this are valuable for international exchange students?",
                ImageUrl = "/uploads/questions/ent/ENT203_p2_IM40.png",
                ImageDescription = "An advertising flyer for a Hanoi City Tour for university students. Key details shown include: Tour dates: Every weekend starting at 8:00 AM; Meeting location: In front of the Hanoi Opera House; Highlights: visiting Ho Chi Minh Mausoleum, Temple of Literature, Old Quarter walking tour, and Hoan Kiem Lake; Tour cost: 250,000 VND per person including guide and entrance fees; Registration: sign up online at student portal or visit Room 204 Student Affairs before Friday.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            // ENT203 - Part 2 Discussion
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e87"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 2 - Discussion on Travel and Cultural Greetings",
                Content = "Topic: Getting Acquainted and Travel. Please answer the following discussion questions:\n1. What is a common greeting in Vietnam when you meet someone for the first time?\n2. Is it common to shake hands in Vietnam? When do people do this?\n3. What is your favorite tourist attraction in Vietnam and why?\n4. Do you prefer visiting natural attractions or cultural attractions?",
                SampleAnswer = "When meeting someone for the first time in Vietnam, people typically say 'Xin chao' accompanied by a friendly smile and a polite nod. In formal or business settings, handshakes are common among colleagues and business partners, whereas with older elders, a slight bow is shown as a mark of respect. My favorite tourist attraction in Vietnam is Hoi An Ancient Town because of its preserved heritage architecture, colorful lantern streets, and rich cuisine. Personally, I prefer cultural and historical attractions because they provide deeper insights into local customs and history.",
                KeyPoints = "[\"Explain traditional and business greetings in Vietnam (Xin chao, handshakes, polite bow)\",\"Identify favorite domestic destination with compelling reasons\",\"Compare natural vs cultural attractions with thoughtful justification\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "How can tourism in historical sites be balanced with environmental preservation?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // ENT303 - Part 1 Role-Play
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e88"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 1 - Conversation on Country Etiquette",
                Content = "Unit 1: Cultural Etiquette. Role-play a conversation advising a foreign friend or colleague about etiquette and manners in your country. Your conversation should include:\n- The ways people greet each other\n- Conversation topics that are appropriate versus those that should be avoided\n- The ways people address each other formally versus casually\n- Important do's and don'ts in table manners.",
                SampleAnswer = "Welcome to Vietnam! When greeting people here, a warm smile with a nod is standard for casual meetings, while a firm handshake is used in professional contexts. For conversation topics, talking about food, travel, and cultural experiences is always welcomed; however, you should avoid sensitive topics like personal salary, marital pressure, or political opinions upon first meeting. When addressing elders or seniors, always use respectful pronouns and honorifics. At the dinner table, always wait for the oldest person to invite everyone to eat before starting, never stick your chopsticks vertically into your rice bowl as it resembles funeral incense, and use both hands when offering food or drinks.",
                KeyPoints = "[\"Cover greetings (handshake vs nod)\",\"Identify safe topics (food, travel) vs taboo topics (salary, politics)\",\"Explain formal honorifics and addressing\",\"Detail table manners: waiting for elders, chopsticks etiquette, passing items with two hands\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "How has globalization changed the way young Vietnamese people communicate compared to older generations?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            // ENT303 - Part 2 Discussion
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e89"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 2 - Discussion on Politeness and Social Success",
                Content = "Unit 1: Politeness and Society. Please discuss the following questions:\n1. In Vietnam, what are some key things people do to show politeness?\n2. Why is being polite important in social and professional life?\n3. Is it appropriate to hug or kiss another person in greeting in Vietnam? Why or why not?\n4. How can having good manners help you become successful in your career?",
                SampleAnswer = "In Vietnam, politeness is expressed through respectful language, using both hands when handing items to elders, and acknowledging others with a smile and gentle nod. Being polite is essential because it builds mutual trust, prevents unnecessary conflict, and reflects emotional maturity. Physical greetings like hugging or kissing on the cheek are generally not traditional in Vietnam; they are reserved only for very close family or romantic partners, as public reserve is preferred. In professional careers, strong etiquette and courteous communication open doors to networking, earning the respect of colleagues and clients, and demonstrating leadership presence.",
                KeyPoints = "[\"Elaborate on cultural markers of politeness in Vietnam\",\"Analyze social value of courtesy in personal and business contexts\",\"Evaluate physical greeting boundaries in Vietnamese culture\",\"Connect good manners to professional networking and career advancement\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Do you believe strict etiquette can sometimes create unnecessary social distance between people? Why or why not?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // ENT403 - Part 1 Monologue
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e8a"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Monologue: Describing an Unseen Part of Personality",
                Content = "Description Monologue: Describe a part of your personality that others may not readily know.\nYou should explain:\n- What part of your personality it is\n- Why others may not know this about you\n- How you feel about this part of your personality\n- How this part of your personality affects your daily life and relationships.",
                SampleAnswer = "Most people who meet me think I am an outspoken, high-energy extrovert who loves being in the center of attention. However, an underlying part of my personality that few people know is that I have a deeply introverted, reflective side that craves solitude. Others rarely see this because in academic and team settings, I instinctively take charge of communication and leadership tasks to keep projects moving. I value this quiet side deeply because it provides me with emotional balance and self-awareness through writing and reading. It positively influences my life by helping me process complex feelings, recharge after demanding deadlines, and listen with genuine empathy when my friends need support.",
                KeyPoints = "[\"Identify specific hidden personality trait clearly\",\"Explain contrast between outward perception and internal reality\",\"Express personal feelings and self-acceptance\",\"Analyze concrete impacts on daily life, stress management, and interpersonal relationships\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "How can understanding your own personality type help you collaborate better in software engineering teams?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            // ENT403 - Part 2 Discussion
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e8b"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 2 - Discussion: Mindsets, Stress, and Social Media",
                Content = "Discussion Questions on Psychology and Behavior:\n1. How does social media influence human behavior and self-esteem?\n2. What are healthy strategies to manage academic and workplace stress?\n3. Which mindset—pessimistic, realistic, or optimistic—leads to greater long-term happiness and success?\n4. How do you balance being realistic with staying optimistic when facing difficult setbacks?",
                SampleAnswer = "Social media has a profound impact on behavior by creating comparison traps where people contrast their behind-the-scenes realities with others' curated highlights, often eroding self-esteem. To manage stress constructively, practicing regular physical exercise, setting digital boundaries, and adopting mindfulness techniques are far more effective than avoidance. Among mindsets, I believe an 'optimistic realist' achieves the highest fulfillment—one who maintains faith in a positive ultimate outcome while objectively acknowledging immediate constraints. When facing setbacks, I anchor myself in realistic problem-solving: breaking the challenge into actionable steps while keeping the optimistic belief that skills can be improved through perseverance.",
                KeyPoints = "[\"Critically evaluate social media's psychological impact on behavior and self-esteem\",\"Propose actionable, healthy stress management techniques\",\"Compare pessimistic, realistic, and optimistic mindsets with logical justification\",\"Synthesize realism and optimism into a resilient problem-solving strategy\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "What role does emotional intelligence (EQ) play compared to intellectual intelligence (IQ) when resolving team conflicts?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // ENT503 - Part 1 Monologue
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e8c"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Monologue: Achieving a Significant Goal",
                Content = "Description Monologue: Describe a time when you successfully achieved an important personal or academic goal.\nYou should discuss:\n- What the goal was and why it mattered to you\n- The steps you took and obstacles you overcame to achieve it\n- How you felt when you accomplished it\n- What key lessons you learned from the entire experience.",
                SampleAnswer = "A defining goal I accomplished was building and publishing an open-source technical project during my third semester of university. The goal was to prove to myself that I could create software that solves a real developer problem outside of classroom assignments. To achieve it, I spent two months studying Clean Architecture and modern API patterns, waking up early each morning to write code before classes. The biggest hurdle was managing burnout when complex bugs threatened my progress, which I overcame by breaking tasks into small daily commits. When the project received its first community stars and positive feedback, I felt an exhilarating sense of validation. The greatest lesson I took away is that consistent, disciplined incremental effort consistently outperforms erratic bursts of inspiration.",
                KeyPoints = "[\"Clearly define meaningful goal and motivation\",\"Detail specific actionable steps and obstacles overcome with grit\",\"Describe emotional impact of success\",\"Articulate enduring life lessons regarding discipline, resilience, and personal growth\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "How do you distinguish between a realistic, ambitious goal and an unrealistic dream that might lead to frustration?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            // ENT503 - Part 2 Discussion
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000e8d"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 2 - Discussion: Goal Setting, Family Influence, and Career Choices",
                Content = "Discussion Questions on Achievement and Decision-Making:\n1. How does establishing SMART goals help individuals realize their long-term ambitions?\n2. What are the most common pitfalls people encounter when striving to reach their goals?\n3. How can family guidance and support contribute positively to career success?\n4. Should parents have the final say in their children's choice of academic major and career path? Why or why not?",
                SampleAnswer = "Establishing SMART goals—Specific, Measurable, Achievable, Relevant, and Time-bound—translates abstract aspirations into actionable milestones, drastically increasing focus and accountability. The most frequent pitfall is lack of consistency and giving up at the first sign of friction or failure. Family support provides an invaluable psychological safety net and emotional encouragement during periods of intense difficulty. However, regarding career paths, parents should act as trusted advisors rather than sole decision-makers. Because the student must live with the consequences of their career and perform the daily work, intrinsic passion and aptitude are vital for sustained excellence and mental well-being.",
                KeyPoints = "[\"Explain mechanics and benefits of SMART goal framework\",\"Analyze root causes of goal failure (lack of discipline, vague objectives)\",\"Acknowledge the constructive role of family emotional and logistical support\",\"Argue persuasively for student autonomy in major/career choices while respecting parental counsel\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "How can higher education institutions better prepare graduates for an AI-driven, rapidly evolving job market?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            }            ,
            // =========================================================================
            // 5. BỔ SUNG ĐỦ 12 CÂU HỎI / MÔN CHO 6 MÔN ENT001 - ENT503 (4 EASY, 4 MEDIUM, 4 HARD)
            // =========================================================================

            // --- ENT001 Additional Questions (2 Easy, 4 Medium, 4 Hard) ---
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e03"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 1 - Family Members and Home Life",
                Content = "Topic: Family and Home. Please answer the following 5 personal questions in complete sentences:\n1. How many people are there in your family?\n2. Who do you live with right now?\n3. What does your mother or father like doing in their free time?\n4. What is your favorite room in your house?\n5. What do you and your family usually do together on weekends?",
                SampleAnswer = "There are four people in my nuclear family: my parents, my younger brother, and me. Right now, I live with my family in a cozy house in Ho Chi Minh City. In their free time, my father enjoys gardening on the balcony while my mother loves cooking traditional Vietnamese dishes. My favorite room in our house is my bedroom because it is quiet and has a large desk by the window. On weekends, my family usually eats dinner together and watches a comedy show in the living room.",
                KeyPoints = "[\"State family size and living arrangement clearly\",\"Describe parents hobbies and favorite room with specific details\",\"Explain weekend family activities using simple present tense\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Who are you closest to in your family, and why do you enjoy talking to them?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e04"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 1 - Food Preferences and Shopping Habits",
                Content = "Topic: Food and Shopping. Please answer the following questions in complete sentences:\n1. What is your favorite traditional food and drink?\n2. Do you prefer eating at home or eating at a restaurant?\n3. Who usually cooks meals in your home?\n4. How often do you go shopping for clothes or groceries?\n5. Where do you usually go shopping with your friends?",
                SampleAnswer = "My favorite traditional food is beef noodle soup, and my favorite drink is fresh orange juice. I prefer eating home-cooked meals because they are healthier, cleaner, and more affordable than restaurant food. My mother usually cooks dinner for our family, and I help her wash the vegetables. I go grocery shopping at the supermarket once a week on Sunday morning. When I shop for clothes with my friends, we usually visit a modern shopping mall in the city center.",
                KeyPoints = "[\"Identify favorite food and drink in full sentences\",\"Compare eating at home vs restaurants with clear reasons\",\"Describe cooking and shopping frequency using adverbs of frequency\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Can you cook any dish by yourself? Please describe how you make it.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e05"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 2 - Household Chores & Family Cooperation (Picture Analysis)",
                Content = "Look at the picture of the mother and child doing laundry. Please describe the scene and answer:\n1. How is the mother teaching her child good habits in this picture?\n2. Why is the child smiling while helping with the laundry?\n3. What safety precautions should parents take around washing machines?\n4. How do you share housework in your own home?",
                SampleAnswer = "In this picture, the mother is kneeling beside the white front-loading washing machine and patiently guiding her toddler to sort and load clothes from the laundry basket. The child is smiling happily because the mother turns household chores into a fun, interactive bonding activity. Around washing machines, parents should keep laundry detergent out of reach and never let small children climb near the drum. In my own home, we divide chores fairly: I sweep the floor and do the laundry while my brother washes the dishes.",
                KeyPoints = "[\"Connect the visual laundry scene to family cooperation\",\"Describe the mother and toddler interacting by the washing machine\",\"Explain household safety and personal chore sharing\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "At what age do you think children should start learning basic household chores?",
                ImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png",
                ImageDescription = "A mother and young toddler doing laundry together in front of a white front-loading washing machine in a domestic room. The mother is kneeling on the wooden floor wearing casual clothes, smiling while holding colorful laundry. The toddler stands nearby reaching into a white laundry basket filled with clothes.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e06"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 2 - Describing Campus Life and Study Routine",
                Content = "Topic: University Campus Life. Please describe your daily study routine at university:\n1. How do you travel to campus every day?\n2. What subjects are you studying this semester?\n3. Where do you like to study between classes?\n4. How do you practice speaking English outside the classroom?",
                SampleAnswer = "Every day, I travel to the FPT University campus by motorbike or electric bus, which takes about twenty minutes. This semester, I am studying English Preparation and foundational computing skills. Between classes, I enjoy studying on the second floor of the university library because it is air-conditioned and peaceful. Outside the classroom, I practice speaking English by shadowing English videos, chatting with classmates in the English club, and recording voice notes on my phone.",
                KeyPoints = "[\"Explain daily commute and current semester subjects\",\"Describe preferred campus study spot with reasons\",\"Detail practical methods for practicing spoken English\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "What is the biggest difficulty you face when speaking English in front of others?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e07"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 2 - Weather, Seasons, and Outdoor Leisure",
                Content = "Topic: Weather and Free Time. Please answer the following questions with examples:\n1. What is the weather like in your city today?\n2. Which season of the year do you like best and why?\n3. What outdoor activities do you enjoy when the weather is sunny?\n4. What do you usually do when it rains heavily all day?",
                SampleAnswer = "Today in Ho Chi Minh City, the weather is warm and sunny with a gentle breeze in the morning. My favorite season is the cool dry season from December to February because the temperature is pleasant for traveling and outdoor festivals. When the weather is sunny, I enjoy jogging in the park and playing badminton with my neighbors. When it rains heavily all day, I prefer staying indoors, drinking hot tea, reading books, and watching English movies.",
                KeyPoints = "[\"Describe current weather and favorite season accurately\",\"Contrast sunny outdoor activities with rainy indoor activities\",\"Use descriptive weather adjectives and clear examples\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Does bad weather ever affect your mood or motivation to study? How do you deal with it?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e08"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 2 - Describing a Memorable Weekend or Holiday",
                Content = "Topic: Weekend and Holidays. Please talk about a memorable weekend or holiday you had recently:\n1. Where did you go and who went with you?\n2. What activities did you do there?\n3. What food did you try?\n4. Why was this trip special to you?",
                SampleAnswer = "Last month, I had a memorable weekend trip to Vung Tau beach with my family. We traveled there by car early on Saturday morning. During the trip, we swam in the sea, walked along the coastal road at sunset, and took many family photos. We also ate fresh grilled seafood and local banh khot at a famous seaside restaurant. This weekend was special to me because it helped all of us relax and bond after a busy month of work and exams.",
                KeyPoints = "[\"Use past simple tense accurately to narrate a recent trip\",\"Specify destination, companions, activities, and local food\",\"Explain emotional significance of the experience\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "For your next holiday, would you prefer visiting the mountains or the beach? Why?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e09"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 3 - Modern Home Appliances & Family Bonding (Picture Discussion)",
                Content = "Look at the picture of the mother and child by the washing machine and discuss:\n1. How have modern home appliances like washing machines changed family life compared to the past?\n2. Why is it important for parents to spend quality time with young children even during daily chores?\n3. Do you think technology gives families more time together or makes them busier?",
                SampleAnswer = "Looking at the mother and toddler beside the modern front-loading washing machine, we can see how home appliances have transformed domestic life. In the past, washing clothes by hand took hours of exhausting labor, whereas modern machines save time and physical effort. Spending quality time with children during daily routines teaches them responsibility and strengthens emotional attachment. Overall, household technology gives families more free time together, provided we put away smartphones and interact face-to-face like the mother and child in the picture.",
                KeyPoints = "[\"Compare past manual housework with modern home appliances shown in the image\",\"Analyze the educational value of parent-child interaction during chores\",\"Evaluate how technology impacts family bonding time\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Which home appliance in your house do you think is the most indispensable, and why?",
                ImageUrl = "/uploads/questions/ent/ENT001_p3_Im2.png",
                ImageDescription = "A mother and young toddler doing laundry together in front of a white front-loading washing machine in a domestic room. The mother is kneeling on the wooden floor wearing casual clothes, smiling while holding colorful laundry. The toddler stands nearby reaching into a white laundry basket filled with clothes.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e0a"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 3 - Balancing Study, Hobbies, and Healthy Habits",
                Content = "Extended Discussion: Many first-year university students struggle to manage their time. Please discuss:\n1. Why is it important to balance academic study with physical exercise and hobbies?\n2. What happens when students stay up late playing games or using social media?\n3. What daily habits help you stay healthy and focused at university?",
                SampleAnswer = "Balancing study with exercise and hobbies is essential because physical activity refreshes the brain, reduces stress, and prevents burnout. When students stay up late playing video games or scrolling through social media, they suffer from sleep deprivation, poor concentration in morning lectures, and lower academic performance. To stay healthy and focused, I maintain a regular sleep schedule of seven hours, drink plenty of water, exercise for thirty minutes every afternoon, and review my lessons daily.",
                KeyPoints = "[\"Explain benefits of balancing study with exercise and hobbies\",\"Analyze negative consequences of late-night gaming and social media\",\"Propose concrete daily habits for student health and focus\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "If a friend of yours is always tired in class because of late-night gaming, what advice would you give them?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e0b"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 3 - Living with Family vs Living Independently in a Dormitory",
                Content = "Extended Discussion: Compare living at home with your parents versus living in a university dormitory:\n1. What are the main advantages of living with your family?\n2. What life skills do students learn when living in a dormitory?\n3. Which living arrangement do you think is better for freshmen, and why?",
                SampleAnswer = "Living at home with parents offers emotional warmth, nutritious home-cooked meals, and lower living expenses. On the other hand, living in a university dormitory teaches students valuable life skills such as budgeting money, managing time, doing laundry, and resolving conflicts with roommates. In my view, living in a dormitory for at least one semester is better for freshmen because it builds self-reliance, maturity, and lifelong campus friendships.",
                KeyPoints = "[\"Compare advantages of living with family vs living in a dormitory\",\"Identify independent life skills gained in dorm life\",\"Justify a clear recommendation for first-year students\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "What are the most important rules roommates should agree on to live together peacefully?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001e0c"),
                CourseId = ent001Course.Id,
                RubricId = RubricEnt001Id,
                Title = "ENT001 Part 3 - The Role of English in Daily Life and Future Jobs",
                Content = "Extended Discussion: Discuss why learning English from the foundation level is important:\n1. How does English help you in your daily entertainment and online learning?\n2. Why do technology companies require employees to communicate in English?\n3. What is your personal plan to improve your English speaking skills this year?",
                SampleAnswer = "English is a gateway to global knowledge and entertainment, allowing me to watch international videos, read technical articles, and take online courses without waiting for translations. Technology companies require English proficiency because software documentation, international clients, and multicultural engineering teams all operate in English. My personal plan this year is to practice speaking for twenty minutes daily, learn ten new topic collocations each week, and actively participate in every oral practice session.",
                KeyPoints = "[\"Explain practical value of English for online learning and media\",\"Connect English proficiency to career opportunities in technology\",\"Outline a structured personal improvement plan\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Do you think AI translation tools will ever replace the need to speak English fluently in person? Why or why not?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // --- ENT104 Additional Questions (3 Easy, 3 Medium, 4 Hard) ---
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e03"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 1 - Movies, Music, and Entertainment Preferences",
                Content = "Topic: Entertainment. Please answer the following 5 questions:\n1. What kind of music do you like listening to?\n2. When do you usually listen to music?\n3. What is your favorite movie genre?\n4. Do you prefer watching movies at the cinema or at home?\n5. Who is your favorite singer or band?",
                SampleAnswer = "I really enjoy listening to acoustic pop and lo-fi instrumental music. I usually listen to music in the evening while studying or when commuting on the bus. My favorite movie genre is science fiction and action adventure because the visual effects are exciting. I prefer watching blockbuster movies at the cinema with friends for the big screen and surround sound. My favorite singer is Taylor Swift because her storytelling and melodies are very catchy.",
                KeyPoints = "[\"Specify favorite music genres, listening habits, and movie preferences\",\"Compare cinema vs home viewing with clear reasons\",\"Use appropriate entertainment vocabulary and complete sentences\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Can listening to English songs and watching movies with English subtitles help improve your pronunciation? How?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e04"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 1 - Sports, Fitness, and Healthy Lifestyle",
                Content = "Topic: Health and Fitness. Please answer the following 5 questions:\n1. What sports can you play?\n2. How often do you exercise each week?\n3. Where do you usually go to work out or play sports?\n4. What healthy food do you eat every day?\n5. How many hours do you sleep each night?",
                SampleAnswer = "I can play badminton, football, and table tennis quite well. I exercise about three to four times a week after finishing my afternoon classes. I usually go to the university sports complex or the neighborhood park to jog and play badminton. Every day, I try to eat green vegetables, fresh fruit like bananas and apples, and lean protein. I make sure to sleep at least seven hours each night so I feel energetic the next morning.",
                KeyPoints = "[\"Describe sports abilities, exercise frequency, and workout locations\",\"Mention daily healthy diet and sleep duration\",\"Demonstrate accurate present tense and health vocabulary\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Do you prefer team sports like football or individual exercises like running? Why?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e05"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 1 - Observing Workplace Attire & Office Environment (Picture Intro)",
                Content = "Look at the picture of the office meeting room and answer these introductory questions:\n1. What kind of clothes are the people in the room wearing?\n2. What objects can you see on the wooden table?\n3. What is the man standing near the board holding in his hand?\n4. Does the room look bright and modern? Why?",
                SampleAnswer = "The people in the conference room are wearing neat, professional business attire such as blazers, dress shirts, and smart office clothes. On the wooden table, I can see open laptop computers, notebooks, pens, and water glasses. The man standing near the whiteboard is holding a smartphone in his hand while gesturing to his colleagues. The room looks very bright and modern because it has large floor-to-ceiling glass walls, indoor plants, and clean contemporary furniture.",
                KeyPoints = "[\"Identify professional business attire of the five colleagues\",\"List objects on the conference table (laptops, notebooks)\",\"Describe the standing presenter holding a smartphone and the glass-walled room\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Would you like to work in a modern office environment like the one in the picture after graduation?",
                ImageUrl = "/uploads/questions/ent/ENT104_p2_Im1.png",
                ImageDescription = "A professional corporate business meeting in a modern conference room with glass walls. Five business colleagues (three men and two women) are seated around a wooden conference table with laptops and notebooks. A male presenter stands near a white board holding a smartphone and gesturing, while the others listen attentively in professional business attire.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e06"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 2 - Describing a Part-Time Job or Volunteer Work",
                Content = "Topic: Work and Experience. Talk about a part-time job, club activity, or volunteer experience you have participated in:\n1. What was the role or activity?\n2. Where and when did you do it?\n3. What tasks were you responsible for?\n4. What skills did you learn from that experience?",
                SampleAnswer = "Last semester, I volunteered as an event organizer for the university orientation week on campus. My team was responsible for welcoming new freshmen, guiding them to lecture halls, and answering questions about campus facilities. Through this activity, I learned how to communicate clearly with strangers, coordinate tasks under time pressure, and solve unexpected scheduling problems calmly as a team.",
                KeyPoints = "[\"Describe the volunteer or part-time role and setting clearly\",\"Detail specific responsibilities and actions taken\",\"Reflect on soft skills gained such as teamwork and communication\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Should university students take a part-time job during the semester, or focus 100% on studying?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e07"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 2 - Technology and Smartphone Usage in Study",
                Content = "Topic: Digital Devices. Describe how you use your smartphone and laptop every day:\n1. What apps or software do you use most frequently for studying?\n2. How do you use technology to stay connected with classmates on group projects?\n3. What problems can happen if you rely too much on digital devices?",
                SampleAnswer = "Every day, I use my laptop for writing code, reading PDF textbooks, and submitting assignments on the learning portal, while I use my smartphone for dictionary apps and flashcard review. For group projects, my team uses messaging apps and online document sharing to divide tasks and track deadlines in real time. However, relying too much on digital devices can cause eye strain, distraction from constant notifications, and reduced critical thinking if students copy answers without understanding.",
                KeyPoints = "[\"Identify specific study apps and academic uses of laptop/smartphone\",\"Explain digital collaboration tools for group projects\",\"Discuss drawbacks of excessive screen time and notification distractions\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "How do you prevent smartphone notifications from distracting you when preparing for an exam?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e08"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 2 - Describing an Inspiring Person or Role Model",
                Content = "Topic: People and Personalities. Describe a person you admire (such as a family member, teacher, or leader):\n1. Who is this person and how do you know them?\n2. What is their personality like?\n3. What achievements or qualities make them special?\n4. How have they influenced your attitude toward study or life?",
                SampleAnswer = "A person I deeply admire is my high school informatics teacher, who taught me for two years. He is remarkably patient, approachable, and passionate about technology. Whenever students struggled with difficult algorithms, he stayed after class to explain the logic step by step using real-world analogies. His dedication inspired me to pursue Software Engineering at university and taught me never to give up when facing complex technical problems.",
                KeyPoints = "[\"Introduce the admired person and relationship clearly\",\"Use rich personality adjectives (patient, approachable, dedicated)\",\"Explain concrete ways they influenced personal or academic growth\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "What qualities do you think make a good team leader in a university project?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e09"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 3 - Teamwork Dynamics & Presentation Skills (Picture Analysis)",
                Content = "Look at the picture of the five business colleagues in the conference room and analyze:\n1. How do the body language and eye contact of the seated colleagues show active listening?\n2. Why is combining a whiteboard presentation with laptops and mobile devices effective in modern meetings?\n3. What communication skills are most important when presenting an idea to your team?",
                SampleAnswer = "In the conference room picture, the four seated colleagues demonstrate active listening by turning their posture toward the standing presenter, maintaining focused eye contact, and keeping their laptops and notebooks ready for taking notes. Combining a whiteboard with laptops and a smartphone allows the presenter to sketch high-level concepts visually while referencing real-time data. When presenting an idea to a team, structured organization, confident body language, and openness to constructive feedback are the most critical skills.",
                KeyPoints = "[\"Analyze body language and eye contact of the five colleagues in the image\",\"Explain synergy between visual whiteboards and digital laptops/smartphones\",\"Evaluate key presentation and active listening skills\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "How should a presenter handle a situation where two team members strongly disagree during a meeting?",
                ImageUrl = "/uploads/questions/ent/ENT104_p2_Im1.png",
                ImageDescription = "A professional corporate business meeting in a modern conference room with glass walls. Five business colleagues (three men and two women) are seated around a wooden conference table with laptops and notebooks. A male presenter stands near a white board holding a smartphone and gesturing, while the others listen attentively in professional business attire.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e0a"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 3 - In-Person Office Meetings vs Remote Online Meetings",
                Content = "Extended Discussion: Compare traditional face-to-face meetings with online video meetings:\n1. What are the main benefits of meeting face-to-face in a conference room?\n2. When are online meetings on Zoom or Microsoft Teams more convenient?\n3. Which mode of collaboration do you think is better for software engineering teams?",
                SampleAnswer = "Face-to-face meetings in a conference room build stronger interpersonal trust, allow natural reading of non-verbal cues, and make brainstorming on a physical whiteboard much faster. Conversely, online video meetings save commuting time, enable flexible remote work across different cities, and make screen-sharing code repositories effortless. For software engineering teams, a hybrid approach works best: holding in-person meetings for sprint planning and architecture design while using short online stand-ups for daily progress updates.",
                KeyPoints = "[\"Compare non-verbal communication in face-to-face meetings vs convenience of remote calls\",\"Identify scenarios where each meeting mode excels\",\"Synthesize a practical hybrid collaboration model\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Why do people sometimes feel exhausted after attending too many online video calls in one day?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e0b"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 3 - Online Shopping vs Traditional Retail Stores",
                Content = "Extended Discussion: E-commerce has grown rapidly in Vietnam. Please discuss:\n1. Why do young people prefer shopping on e-commerce platforms?\n2. What are the risks or disadvantages of buying products online?\n3. Will physical retail stores disappear in the future, or how will they adapt?",
                SampleAnswer = "Young consumers prefer e-commerce platforms because they can compare prices instantly, read verified customer reviews, and enjoy doorstep delivery with discount vouchers. However, online shopping carries risks such as receiving counterfeit or damaged goods, sizing mismatches for clothing, and delayed shipping. Physical retail stores will not disappear completely; instead, they will evolve into experiential showrooms where customers test products in person before ordering digitally.",
                KeyPoints = "[\"Analyze drivers of e-commerce popularity among youth\",\"Evaluate risks such as quality mismatch and shipping delays\",\"Predict how physical stores will adapt with omnichannel experiences\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Have you ever bought something online that looked completely different from the advertisement? What did you do?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000002e0c"),
                CourseId = ent104Course.Id,
                RubricId = RubricEnt104Id,
                Title = "ENT104 Part 3 - Preparing for Career Readiness at University",
                Content = "Extended Discussion: Beyond passing exams, how should students prepare for their future careers?\n1. Why are soft skills like teamwork and English communication just as important as technical grades?\n2. How can internships and student clubs bridge the gap between classroom theory and industry practice?\n3. What is one skill you want to master before your On-the-Job Training (OJT) semester?",
                SampleAnswer = "Technical grades alone are not enough because real-world engineering projects require constant collaboration, client requirement clarification, and cross-functional teamwork. Internships and student clubs expose students to real deadlines, version control workflows, and professional accountability that textbooks cannot simulate. Before my On-the-Job Training semester, I aim to master fluent English technical presentation so I can confidently explain software architecture to international mentors.",
                KeyPoints = "[\"Argue why soft skills and English complement technical knowledge\",\"Explain practical value of clubs and internships\",\"Identify a specific target skill for OJT readiness\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "If an employer had to choose between a candidate with high grades but poor teamwork and one with average grades but great attitude, who would they hire?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // --- ENT203 Additional Questions (4 Easy, 1 Medium, 4 Hard) ---
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e04"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 1 - Reading Key Details on the Da Nang Trip Poster (Visual Check)",
                Content = "Look at the School Bus Trip to Da Nang poster and answer these basic information questions:\n1. Which city is the bus trip going to?\n2. What day and time does the bus leave?\n3. Name two tourist places students will visit on this trip.\n4. How much does the ticket cost per student?",
                SampleAnswer = "Looking at the poster, the school bus trip is going to Da Nang city. The bus departs on Saturday morning at 6:30 AM from the school campus. During the trip, students will visit famous places including Ba Na Hills, My Khe Beach, and the Marble Mountains. The ticket price for the trip is 350,000 VND per student, including lunch at a local seafood restaurant.",
                KeyPoints = "[\"Correctly read destination (Da Nang) and departure time (6:30 AM Saturday) from poster\",\"Mention at least two attractions (Ba Na Hills, My Khe Beach, Marble Mountains)\",\"State the ticket price accurately\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Have you ever visited Da Nang or another coastal city in Vietnam? What did you like most?",
                ImageUrl = "/uploads/questions/ent/ENT203_p2_IM28.png",
                ImageDescription = "An informational poster for a School Bus Trip to Da Nang. The poster features key details: Destination: Da Nang city; Departure: Saturday morning at 6:30 AM from school campus; Activities: visiting Ba Na Hills, swimming at My Khe Beach, exploring Marble Mountains; Lunch: 12:30 PM at a local seafood restaurant; Things to bring: student ID card, comfortable walking shoes, sunscreen, swimming suit, and camera; Ticket price: 350,000 VND.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e05"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 1 - Reading Key Details on the Hanoi City Tour Flyer (Visual Check)",
                Content = "Look at the Hanoi City Tour flyer and answer these basic questions:\n1. What time does the Hanoi City Tour start on weekends?\n2. Where is the meeting point for participants?\n3. Name two historical or cultural sights included in the tour.\n4. Where can students register for the tour?",
                SampleAnswer = "According to the flyer, the Hanoi City Tour starts at 8:00 AM every weekend. Participants meet in front of the Hanoi Opera House. The tour visits famous cultural sights such as the Ho Chi Minh Mausoleum, the Temple of Literature, and Hoan Kiem Lake. Students can register online on the student portal or visit Room 204 of the Student Affairs office before Friday.",
                KeyPoints = "[\"Identify start time (8:00 AM weekends) and meeting point (Hanoi Opera House)\",\"Name two cultural sights from the flyer\",\"State registration options clearly\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Which attraction on the Hanoi City Tour flyer would you want to visit first, and why?",
                ImageUrl = "/uploads/questions/ent/ENT203_p2_IM40.png",
                ImageDescription = "An advertising flyer for a Hanoi City Tour for university students. Key details shown include: Tour dates: Every weekend starting at 8:00 AM; Meeting location: In front of the Hanoi Opera House; Highlights: visiting Ho Chi Minh Mausoleum, Temple of Literature, Old Quarter walking tour, and Hoan Kiem Lake; Tour cost: 250,000 VND per person including guide and entrance fees; Registration: sign up online at student portal or visit Room 204 Student Affairs before Friday.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e06"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 1 - Travel Preferences and Packing Essentials",
                Content = "Topic: Travel Preparation. Please answer the following questions:\n1. How do you usually travel when going on a trip (by bus, train, plane, or motorbike)?\n2. Do you prefer traveling with a large group of friends or with one or two close friends?\n3. What three essential items do you always pack in your backpack?\n4. Do you like taking photos when you travel?",
                SampleAnswer = "For long-distance trips, I usually travel by sleeper bus or airplane because it is safe and comfortable. I prefer traveling with a small group of three or four close friends so we can easily agree on the schedule and food choices. Three essential items I always pack in my backpack are my student ID card, a portable power bank for my phone, and sunscreen. I love taking photos of landscapes and local street food to keep memories of each trip.",
                KeyPoints = "[\"State preferred transport mode and travel group size with reasons\",\"List three practical travel items\",\"Express attitude toward travel photography\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Have you ever forgotten an important item when packing for a trip? How did you solve the problem?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e07"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 1 - Welcoming a New Student or Visitor to Your City",
                Content = "Topic: Hospitality and Local Guide. Please answer the following questions:\n1. If a friend visits your city for the first time, where would you take them for breakfast?\n2. What is the best time of day to walk around your city?\n3. What local souvenir or gift would you recommend they buy?\n4. How would you help them travel around the city safely?",
                SampleAnswer = "If a friend visits Ho Chi Minh City for the first time, I would take them to a famous local stall for broken rice (com tam) and iced milk coffee for breakfast. The best time to walk around the city center is early in the morning or after 5:00 PM when the weather cools down. I would recommend buying Vietnamese roasted coffee beans or handcrafted bamboo souvenirs. To help them travel safely, I would accompany them on the metro or book rides through reputable ride-hailing apps.",
                KeyPoints = "[\"Recommend local breakfast dish and best walking hours\",\"Suggest authentic local souvenirs\",\"Provide practical and safe transportation advice\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "What is one common mistake tourists sometimes make when visiting a busy city in Vietnam?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e08"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 2 - Role-Play: Resolving a Travel Booking or Hotel Issue",
                Content = "Role-Play Scenario: You arrived at a hotel for your class trip, but there is a problem with your room reservation (for example, the air conditioner is not working or the room type is wrong). Speak politely to the hotel receptionist:\n- Explain the problem clearly\n- State your booking details\n- Politely request a room change or immediate technician support.",
                SampleAnswer = "Excuse me, good afternoon. I just checked into Room 305 under the reservation for the FPT University student group, but I noticed that the air conditioner in our room is not blowing cool air at all. Since the weather outside is very hot today, it is quite uncomfortable to stay inside. Could you please send a maintenance technician to inspect it right away, or move us to another twin room on the same floor? I would really appreciate your prompt assistance.",
                KeyPoints = "[\"Use polite opening and formal register suitable for customer service\",\"Clearly describe the room issue and reservation context\",\"Propose actionable solutions (technician repair or room change)\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Why is remaining calm and polite more effective than getting angry when dealing with service staff?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e09"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 3 - Comparing the Da Nang Bus Trip vs Hanoi Cultural Tour (Poster Analysis)",
                Content = "Look at the Da Nang School Bus Trip poster and consider a cultural city tour like Hanoi. Please compare and analyze:\n1. How does an outdoor coastal trip (Ba Na Hills, My Khe Beach) differ from a historical city tour?\n2. What safety and logistical challenges must student organizers prepare for on a 6:30 AM bus trip?\n3. Which trip offers better team-building value for university classmates, and why?",
                SampleAnswer = "Comparing the Da Nang trip poster with a cultural city tour, the Da Nang itinerary focuses on dynamic outdoor recreation—visiting Ba Na Hills, swimming at My Khe Beach, and hiking the Marble Mountains—whereas a city tour emphasizes historical museums and architecture. For a 6:30 AM bus departure, organizers must manage punctuality, motion sickness medication, beach water safety, and sun protection. Overall, the Da Nang trip offers stronger team-building value because shared beach games and group dining create lively bonding moments.",
                KeyPoints = "[\"Reference specific details from the Da Nang trip poster (6:30 AM, Ba Na Hills, My Khe Beach)\",\"Analyze logistical and safety responsibilities for outdoor student trips\",\"Evaluate team-building benefits with persuasive justification\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "How can trip organizers ensure that introverted students also feel included during large group excursions?",
                ImageUrl = "/uploads/questions/ent/ENT203_p2_IM28.png",
                ImageDescription = "An informational poster for a School Bus Trip to Da Nang. The poster features key details: Destination: Da Nang city; Departure: Saturday morning at 6:30 AM from school campus; Activities: visiting Ba Na Hills, swimming at My Khe Beach, exploring Marble Mountains; Lunch: 12:30 PM at a local seafood restaurant; Things to bring: student ID card, comfortable walking shoes, sunscreen, swimming suit, and camera; Ticket price: 350,000 VND.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e0a"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 3 - Cultural Heritage Preservation on the Hanoi City Tour (Flyer Analysis)",
                Content = "Look at the Hanoi City Tour flyer (Ho Chi Minh Mausoleum, Temple of Literature, Old Quarter, Hoan Kiem Lake) and discuss:\n1. Why is it important for university students to visit historical landmarks like the Temple of Literature?\n2. What rules of dress code and respectful behavior should visitors follow at sacred historical sites?\n3. How can modern digital guides enhance the experience of walking through the Old Quarter?",
                SampleAnswer = "Visiting landmarks on the Hanoi City Tour flyer—especially the Temple of Literature, Vietnam's first national university, and the Ho Chi Minh Mausoleum—connects students with centuries of scholarly tradition and national history. At these solemn sites, visitors must wear modest clothing covering shoulders and knees, speak quietly, and refrain from littering. Modern mobile audio guides and QR-code storytelling in the Old Quarter can enrich the tour by allowing students to listen to historical narratives in multiple languages.",
                KeyPoints = "[\"Connect the flyers landmarks (Temple of Literature, Mausoleum, Old Quarter) to educational value\",\"Specify respectful dress code and conduct at historical sites\",\"Propose digital innovations for cultural tourism\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Do you think entrance fees to historical monuments should be free for all students? Why or why not?",
                ImageUrl = "/uploads/questions/ent/ENT203_p2_IM40.png",
                ImageDescription = "An advertising flyer for a Hanoi City Tour for university students. Key details shown include: Tour dates: Every weekend starting at 8:00 AM; Meeting location: In front of the Hanoi Opera House; Highlights: visiting Ho Chi Minh Mausoleum, Temple of Literature, Old Quarter walking tour, and Hoan Kiem Lake; Tour cost: 250,000 VND per person including guide and entrance fees; Registration: sign up online at student portal or visit Room 204 Student Affairs before Friday.",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e0b"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 3 - Guided Package Tours vs Independent Backpacking",
                Content = "Extended Discussion: Compare joining an organized package tour with planning an independent backpacking trip:\n1. What are the main advantages of an organized package tour?\n2. Why do many young travelers prefer independent self-planned trips?\n3. How do you evaluate safety, budget, and flexibility when choosing between the two?",
                SampleAnswer = "Organized package tours offer convenience, fixed budgeting, professional tour guides, and guaranteed transportation, which is ideal for families or large school groups. In contrast, young travelers often prefer independent backpacking because it provides total freedom to explore hidden local spots and adjust schedules spontaneously. When choosing between the two, I weigh familiarity with the destination: for a first-time visit to a remote area, a guided tour ensures safety, whereas for familiar cities, independent travel maximizes flexibility.",
                KeyPoints = "[\"Contrast convenience/safety of package tours with flexibility of independent travel\",\"Analyze decision factors: budget, group size, and destination familiarity\",\"Use cohesive comparative structures (whereas, in contrast, on the other hand)\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "How have travel vlogs and social media reviews changed the way people plan their trips today?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000003e0c"),
                CourseId = ent203Course.Id,
                RubricId = RubricEnt203Id,
                Title = "ENT203 Part 3 - Cross-Cultural Communication and Avoiding Misunderstandings",
                Content = "Extended Discussion: When traveling abroad or working with international students, cultural differences are common. Please discuss:\n1. What non-verbal gestures or habits can cause misunderstandings between different cultures?\n2. How should you react if you accidentally offend someone from another culture?\n3. Why is cultural curiosity important for future software engineers?",
                SampleAnswer = "Non-verbal gestures such as eye contact, personal space, hand signals, and punctuality norms vary widely across cultures and can easily cause unintended misunderstandings. If I accidentally offend someone from another culture, the best response is to apologize sincerely, ask politely about the custom, and show willingness to learn. For future software engineers, cultural curiosity is vital because we build products for diverse global users and collaborate daily with international clients.",
                KeyPoints = "[\"Identify cross-cultural differences in non-verbal communication and norms\",\"Explain how to handle cultural missteps with humility and respect\",\"Connect cultural awareness to global software engineering work\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Can you share an interesting custom from another country that you learned about from books or movies?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // --- ENT303 Additional Questions (4 Easy, 3 Medium, 3 Hard) ---
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e03"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 1 - Everyday Manners and Polite Expressions",
                Content = "Unit 1: Everyday Politeness. Please answer the following questions:\n1. How do you greet your university lecturers when you meet them on campus?\n2. What polite words do you use when asking a classmate for a favor?\n3. What should you do if you arrive late to a class or meeting?\n4. How do you show respect to elderly people on public transport?",
                SampleAnswer = "When I meet my lecturers on campus, I bow my head slightly, smile, and greet them respectfully. When asking a classmate for a favor, I always say 'Could you please help me' and thank them warmly afterward. If I arrive late to a class, I knock gently, apologize to the lecturer, and take a seat quietly without disturbing others. On public buses, I immediately stand up and offer my seat to elderly passengers, pregnant women, or young children.",
                KeyPoints = "[\"Describe respectful greetings for lecturers and polite request expressions\",\"Explain proper classroom etiquette when arriving late\",\"Give concrete examples of public transport courtesy\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "How do you feel when someone helps you on the street or holds the door open for you?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e04"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 1 - Reading Habits, News, and Information Sources",
                Content = "Unit 2: Media and Information. Please answer the following questions:\n1. How do you usually get your daily news?\n2. What topics of news or articles interest you the most?\n3. Do you prefer reading printed books or e-books on a screen?\n4. How much time do you spend reading every day?",
                SampleAnswer = "I usually get my daily news from reputable online news portals and technology newsletters on my smartphone each morning. The topics that interest me most are artificial intelligence breakthroughs, education, and sports. For long novels or textbooks, I still prefer reading printed paper books because they are easier on my eyes and help me remember details better. I try to spend at least thirty minutes reading before going to bed every night.",
                KeyPoints = "[\"State daily news sources and preferred topics\",\"Compare printed books vs e-books with personal reasons\",\"Specify daily reading duration\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "What is the most interesting book or article you have read recently?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e05"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 1 - Gift-Giving Customs and Special Occasions",
                Content = "Unit 3: Celebrations and Customs. Please answer the following questions:\n1. On what occasions do people in Vietnam usually give gifts?\n2. What kind of gifts do you like giving to your close friends on their birthdays?\n3. Is it better to open a gift immediately in front of the giver or later in private?\n4. What is the best gift you have ever received?",
                SampleAnswer = "In Vietnam, people commonly exchange gifts on birthdays, Lunar New Year (Tet), weddings, Teacher's Day, and housewarming parties. For my close friends' birthdays, I like giving practical gifts such as books, stationery, or a personalized mug. Traditionally in Vietnam, people often open gifts later in private to avoid appearing greedy, unless the giver invites them to open it right away. The best gift I ever received was a mechanical keyboard from my parents when I passed my university entrance exam.",
                KeyPoints = "[\"List common gift-giving occasions in Vietnam\",\"Explain cultural etiquette around opening gifts\",\"Describe a meaningful personal gift received\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Do you think a handmade gift is more meaningful than an expensive store-bought gift?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e06"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 1 - Environmental Habits and Green Living",
                Content = "Unit 4: Environment and Daily Life. Please answer the following questions:\n1. What do you do every day to save electricity and water at home?\n2. Do you bring your own reusable water bottle or bag when going to school?\n3. How is trash sorted in your neighborhood or university campus?\n4. Why is planting trees important for big cities?",
                SampleAnswer = "To save electricity and water at home, I always turn off lights, fans, and air conditioners when leaving a room and close the tap while brushing my teeth. I bring my own stainless-steel water bottle and canvas tote bag to campus every day to reduce single-use plastic waste. At our university, trash bins are clearly separated into recyclable plastic, paper, and general waste. Planting trees is vital for big cities because trees absorb carbon dioxide, provide shade, and cool down urban heat.",
                KeyPoints = "[\"Describe concrete daily energy and water saving habits\",\"Mention reusable bottles/bags and campus recycling\",\"Explain environmental benefits of urban trees\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "How can student clubs encourage more classmates to stop using disposable plastic cups?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e07"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 1 - Role-Play: Advising an Intern on Workplace & Email Etiquette",
                Content = "Role-Play Scenario: A friend is starting their first software engineering internship next week and asks for your advice on workplace manners. Explain:\n- How to dress and arrive on the first day\n- How to write polite, professional emails and chat messages to senior engineers\n- What habits to avoid during team meetings.",
                SampleAnswer = "Congratulations on your internship! On your first day, you should dress in smart-casual attire and arrive at least fifteen minutes early to show reliability. When sending emails or chat messages to senior engineers, always include a clear subject line, a polite greeting, a concise summary of what you have already tried, and appreciation for their time. During team meetings, avoid checking your phone, interrupting speakers, or staying silent when you do not understand a task—always take notes and ask constructive questions.",
                KeyPoints = "[\"Advise on punctuality and smart-casual dress code\",\"Detail professional email and technical chat etiquette\",\"Warn against distracting behaviors in team meetings\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "If an intern makes a mistake in their code during the first week, how should they report it to their mentor?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e08"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 1 - Role-Play: Discussing Community Service and Volunteering",
                Content = "Role-Play Scenario: You and your classmate are planning a weekend volunteer activity for your English club. Discuss and propose:\n- What community activity to organize (e.g., teaching kids, park clean-up, charity book drive)\n- Where and when it will take place\n- How to recruit student volunteers and measure the impact.",
                SampleAnswer = "For our English club's monthly volunteer project, I suggest organizing a 'Coding and English Fun Day' at a local community center this Sunday morning. We can teach primary school children basic English vocabulary through interactive games and simple visual puzzles. To recruit volunteers, we can post an announcement on our campus fanpage and assign pairs of members to prepare flashcards. This project will make a meaningful impact by inspiring local kids while helping our club members practice leadership.",
                KeyPoints = "[\"Propose a clear, feasible community volunteer project\",\"Specify time, location, and task division\",\"Explain benefit for both the community and student volunteers\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "What challenges might volunteers face when teaching young children, and how can they prepare?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e09"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 2 - Storytelling: A Time When Good Manners Solved a Problem",
                Content = "Narrative Task: Describe a real or memorable situation where being polite, patient, and respectful helped you resolve a misunderstanding or difficult situation:\n- What was the situation and who was involved?\n- What caused the problem?\n- How did you communicate politely to solve it?\n- What was the final outcome?",
                SampleAnswer = "Last semester, during a busy group project, a teammate accidentally overwrote part of my frontend code right before our rehearsal. Instead of blaming him angrily in the group chat, I called him privately, calmly explained the version conflict, and asked if we could review the Git commit history together. Because I spoke respectfully, he immediately apologized and stayed up with me for an hour to merge both changes cleanly. In the end, our presentation succeeded and our friendship became even stronger.",
                KeyPoints = "[\"Narrate a structured past incident with clear context and conflict\",\"Demonstrate how calm, respectful communication de-escalated tension\",\"Highlight positive resolution and lesson learned\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "How do you stop yourself from reacting emotionally when you feel frustrated or stressed?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e0a"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 2 - Digital Netiquette and Cyberbullying in Online Communities",
                Content = "Extended Discussion: Discuss the importance of digital etiquette ('netiquette') on social media and online forums:\n1. Why do some people behave more aggressively online than they do in face-to-face conversations?\n2. What are the key rules of respectful online communication?\n3. How should universities and platforms promote healthy digital citizenship?",
                SampleAnswer = "Many people behave more aggressively online because screen anonymity and the lack of immediate eye contact reduce their sense of empathy and accountability. Key rules of digital netiquette include verifying facts before sharing, critiquing ideas rather than attacking individuals, and respecting others' privacy. Universities and digital platforms can promote healthy online citizenship by educating students on media literacy, enforcing clear community guidelines, and encouraging constructive dialogue.",
                KeyPoints = "[\"Analyze psychological reasons behind online aggression (anonymity, distance)\",\"Formulate clear principles of digital netiquette\",\"Propose educational and platform solutions for healthy online culture\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "When you see a heated argument in the comments section of a post, how should you respond?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e0b"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 2 - Preserving Traditional Cultural Values in a Modernizing Society",
                Content = "Extended Discussion: As Vietnam integrates deeply with the global economy, lifestyle and culture are changing rapidly. Please discuss:\n1. Which traditional Vietnamese customs are most important to preserve for future generations?\n2. Are there any outdated customs that should be simplified in modern life?\n3. How can young people embrace global culture without losing their cultural identity?",
                SampleAnswer = "Core Vietnamese traditions such as filial piety, respect for teachers, family reunion during Tet, and community solidarity are timeless values that must be preserved. At the same time, overly lavish or wasteful ceremonies that place heavy financial burdens on families can be simplified to remain meaningful yet practical. Young people can embrace global languages and technology while preserving their roots by learning national history, honoring family traditions, and sharing Vietnamese culture proudly with international friends.",
                KeyPoints = "[\"Identify core traditional values worth preserving\",\"Critically evaluate which rituals can be modernized\",\"Synthesize a balanced approach to global integration and cultural identity\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "How can modern media such as music, movies, or video games make traditional culture more appealing to Gen Z?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000004e0c"),
                CourseId = ent303Course.Id,
                RubricId = RubricEnt303Id,
                Title = "ENT303 Part 2 - Generational Differences in Workplace Communication",
                Content = "Extended Discussion: Today's workplace brings together multiple generations (Gen X, Millennials, and Gen Z). Please discuss:\n1. What are the main differences in how older and younger generations prefer to communicate at work?\n2. What misunderstandings can arise from these differences?\n3. How can both generations adapt to build a harmonious and productive workplace?",
                SampleAnswer = "Older generations often value formal hierarchy, phone calls, or detailed face-to-face discussions, whereas Gen Z employees frequently prefer instant messaging, concise feedback, and flexible workflows. Misunderstandings arise when seniors view short chat replies as disrespectful or when younger staff feel micromanaged by formal procedures. To build harmony, both sides should practice mutual empathy: young professionals can adopt respectful workplace etiquette while senior leaders mentor with open-minded communication.",
                KeyPoints = "[\"Compare communication preferences across generations at work\",\"Analyze root causes of intergenerational friction\",\"Propose mutual adaptation strategies for workplace harmony\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "What is the most valuable lesson you have learned from working or talking with someone much older than you?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // --- ENT403 Additional Questions (4 Easy, 4 Medium, 2 Hard) ---
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e03"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Describing Personal Strengths and Daily Moods",
                Content = "Topic: Personality and Moods. Please answer the following questions:\n1. What three adjectives best describe your personality?\n2. Are you more of a morning person or a night owl?\n3. What small things in daily life always put you in a good mood?\n4. Who do you usually talk to when you feel stressed?",
                SampleAnswer = "The three adjectives that best describe my personality are reliable, curious, and calm. I am definitely a morning person because my mind feels sharpest between 6:00 AM and 10:00 AM. Small daily moments like drinking a warm cup of coffee, finishing a coding task without bugs, or seeing a sunny sky always put me in a cheerful mood. When I feel stressed, I usually talk to my mother or my best friend because they listen patiently and give practical encouragement.",
                KeyPoints = "[\"Identify three accurate personality adjectives with examples\",\"Explain chronotype (morning person vs night owl) and mood boosters\",\"Describe healthy social support when stressed\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Has your personality changed since you graduated from high school and entered university?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e04"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Stress Relief Activities and Relaxation",
                Content = "Topic: Relaxation and Well-Being. Please answer the following questions:\n1. What do you usually do to relax after a long day of exams?\n2. Does listening to music or exercising help you reduce stress more?\n3. Where is your favorite quiet place to clear your mind?\n4. How do you prepare yourself for a good night's sleep?",
                SampleAnswer = "After a long day of exams, I love taking a warm shower, eating a comforting meal, and watching a lighthearted comedy episode. For me, jogging outdoors reduces stress more effectively than sitting still because physical movement releases tension. My favorite quiet place to clear my mind is the lakeside walkway near my campus at sunset. To sleep well at night, I turn off my computer screen thirty minutes before bed and read a few pages of a book.",
                KeyPoints = "[\"Describe post-exam relaxation routines clearly\",\"Compare music vs physical exercise for stress relief\",\"Identify a peaceful place and pre-sleep routine\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Why do many students find it hard to put down their phones before going to sleep?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e05"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Social Media Habits and Online Friends",
                Content = "Topic: Social Networking. Please answer the following questions:\n1. Which social media platforms do you use every day?\n2. How much time do you spend on social media daily?\n3. What kind of content do you enjoy watching or sharing online?\n4. Do you prefer messaging friends online or meeting them in person?",
                SampleAnswer = "I use Facebook, YouTube, and Discord every day to stay updated with university announcements and tech communities. I try to limit my social media usage to about one hour per day so it does not cut into my study time. Online, I enjoy watching software development tutorials, science channels, and travel vlogs. Even though online messaging is convenient for quick updates, I strongly prefer meeting friends in person at a cafe because real conversations feel much warmer.",
                KeyPoints = "[\"State platforms used, daily screen time, and preferred content\",\"Compare online messaging with face-to-face meetings\",\"Demonstrate fluent delivery and clear time expressions\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Have you ever tried taking a digital detox (staying away from social media) for a weekend?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e06"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Teamwork Preferences and Study Habits",
                Content = "Topic: Learning Styles. Please answer the following questions:\n1. Do you prefer studying alone or studying in a group?\n2. What role do you usually take when working in a team project?\n3. How do you keep track of your homework and exam deadlines?\n4. What motivates you when a subject is very difficult?",
                SampleAnswer = "I prefer studying alone when I need to understand new theoretical concepts, but I enjoy group study when reviewing for exams and debugging projects. In team projects, I usually take the role of backend developer or coordinator who tracks milestones. I use a digital calendar and a daily to-do list on Notion to keep track of all my deadlines. When a subject is difficult, breaking it into small topics and seeing my progress each day keeps me motivated.",
                KeyPoints = "[\"Compare solo study vs group study contexts\",\"Identify typical team role and deadline tracking tools\",\"Explain personal motivation strategy for hard subjects\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "What do you do when a group member misses a deadline in a team assignment?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e07"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Monologue: A Stressful Situation and How You Coped With It",
                Content = "Description Monologue: Describe a stressful academic or personal situation you experienced recently.\nYou should explain:\n- What caused the stressful situation\n- How you felt physically and emotionally at that time\n- What specific actions you took to overcome the stress\n- What you learned about managing pressure.",
                SampleAnswer = "A stressful situation I faced occurred during midterms last semester when three project deadlines and an oral exam fell in the same week. At first, I felt overwhelmed and anxious because I tried to multitask everything at once. To regain control, I wrote down a prioritized hourly schedule, tackled the hardest programming module each morning, and took short walks to clear my head. By completing one task at a time and asking my lecturer for early feedback, I passed all assessments with high grades and learned that structured planning defeats panic.",
                KeyPoints = "[\"Describe the stressor and emotional reaction authentically\",\"Detail concrete coping actions (prioritization, time-blocking, exercise)\",\"Reflect on resilience and stress management lessons\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "How can universities better support students who experience high academic anxiety during exam weeks?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e08"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Monologue: A Positive Habit That Changed Your Daily Life",
                Content = "Description Monologue: Describe a positive habit you developed that significantly improved your mental or physical well-being.\nYou should explain:\n- What the habit is and when you started it\n- How you stayed consistent when it was difficult\n- What positive changes you noticed in your energy and mindset.",
                SampleAnswer = "Six months ago, I developed the habit of waking up at 6:00 AM every day to do twenty minutes of stretching and plan my top three daily priorities before checking my phone. During the first two weeks, it was tempting to hit the snooze button, so I placed my alarm across the room and went to sleep before 11:00 PM. Within a month, I noticed a dramatic improvement in my morning focus, lower anxiety during classes, and a strong sense of proactive control over my day.",
                KeyPoints = "[\"Define the positive habit and starting timeline clearly\",\"Explain practical strategies used to maintain consistency\",\"Highlight measurable benefits to mental clarity and energy\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Why is breaking a bad habit often much harder than starting a new one?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e09"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Monologue: An Experience of Stepping Outside Your Comfort Zone",
                Content = "Description Monologue: Describe a time when you stepped outside your comfort zone to try something new or challenging.\nYou should explain:\n- What the activity or challenge was\n- Why you felt nervous or hesitant at first\n- How you prepared and performed\n- How the experience changed your self-confidence.",
                SampleAnswer = "Stepping outside my comfort zone happened when I volunteered to deliver the live English pitch for our team's hackathon project in front of fifty students and judges. Initially, I felt nervous because I used to be shy about speaking English on stage. To prepare, I rehearsed my script ten times in front of a mirror, timed my slides, and focused on speaking slowly and clearly. When the audience applauded our demo, my fear vanished and I realized that confidence comes from preparation and courage.",
                KeyPoints = "[\"Narrate a specific comfort-zone challenge with clear context\",\"Contrast initial hesitation with disciplined preparation\",\"Explain lasting impact on personal self-confidence\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "What advice would you give to a student who is afraid of making mistakes when speaking English?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e0a"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 1 - Monologue: A Friend or Colleague With an Inspiring Mindset",
                Content = "Description Monologue: Describe a friend, classmate, or colleague whose positive mindset inspires you.\nYou should explain:\n- Who this person is and how you work or study together\n- How they react when facing setbacks or difficult tasks\n- What specific lesson you have adopted from their attitude.",
                SampleAnswer = "My project teammate Minh has the most resilient and optimistic mindset of anyone I know. We have worked together on three software engineering courses over the past year. Whenever our code fails a test or requirements change at the last minute, he never complains or blames others; instead, he smiles, brews a cup of tea, and calmly breaks the bug down into testable hypotheses. Working with him taught me to view failures as diagnostic feedback rather than personal defeat.",
                KeyPoints = "[\"Introduce the peer and shared context clearly\",\"Illustrate their resilient attitude with concrete examples\",\"Articulate the mindset lesson learned\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Do you think optimism is an inborn personality trait, or a skill that anyone can train?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e0b"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 2 - Discussion: Introverts vs Extroverts in Leadership and Teamwork",
                Content = "Extended Discussion on Personality Psychology:\n1. Do you think extroverts naturally make better leaders than introverts, or can introverts be equally effective leaders?\n2. How can a team leader bring out the best contributions from both introverted and extroverted team members?\n3. Why is diversity of personality types valuable in an engineering team?",
                SampleAnswer = "Introverts can be just as effective leaders as extroverts, and often excel when leading proactive, analytical teams because they listen deeply, think before speaking, and empower others without dominating the room. A wise leader brings out the best in everyone by combining open group brainstorming for extroverts with written pre-meeting agendas that give introverts time to formulate thoughtful insights. Personality diversity prevents groupthink and balances bold communication with careful risk analysis.",
                KeyPoints = "[\"Compare leadership strengths of introverts and extroverts objectively\",\"Propose inclusive management techniques for diverse personalities\",\"Explain how personality balance strengthens engineering teams\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Have you ever had to adapt your own communication style to work effectively with someone very different from you?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000005e0c"),
                CourseId = ent403Course.Id,
                RubricId = RubricEnt403Id,
                Title = "ENT403 Part 2 - Discussion: Academic Burnout, Perfectionism, and Growth Mindset",
                Content = "Extended Discussion on Mental Resilience:\n1. How does unhealthy perfectionism contribute to academic burnout among high-achieving students?\n2. What is the difference between a 'fixed mindset' and a 'growth mindset' when receiving critical feedback?\n3. How can students maintain high standards of excellence without sacrificing their mental health?",
                SampleAnswer = "Unhealthy perfectionism drives burnout because students equate a single imperfect grade or bug with personal inadequacy, leading to procrastination out of fear of failure. According to Carol Dweck's psychology framework, a fixed mindset views critical feedback as a judgment of permanent intelligence, whereas a growth mindset welcomes critique as a roadmap for skill improvement. Students can pursue excellence sustainably by focusing on continuous progress, celebrating incremental milestones, and treating rest as part of peak performance.",
                KeyPoints = "[\"Analyze link between perfectionism, fear of failure, and burnout\",\"Contrast fixed mindset vs growth mindset when handling criticism\",\"Synthesize sustainable practices for high achievement and mental health\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "How do you personally respond when an AI grader or lecturer points out weaknesses in your speaking performance?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },

            // --- ENT503 Additional Questions (4 Easy, 4 Medium, 2 Hard) ---
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e03"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Short-Term Study Goals and Daily Planning",
                Content = "Topic: Personal Goals. Please answer the following questions:\n1. What is your main academic goal for this semester?\n2. How do you plan your study tasks at the beginning of each week?\n3. Who do you share your goals with to stay accountable?\n4. How do you reward yourself when you finish a big project?",
                SampleAnswer = "My main academic goal this semester is to achieve a distinction grade in both my software engineering project and my advanced English speaking course. Every Sunday evening, I write down my weekly goals and assign specific tasks to each morning and afternoon block. I share my study targets with my project partner so we can check on each other's progress. When I complete a major project, I reward myself by going out for a nice hotpot dinner with my friends.",
                KeyPoints = "[\"State clear semester goal and weekly planning method\",\"Explain peer accountability and self-reward habits\",\"Speak with clear structure and accurate vocabulary\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Do you prefer writing your goals on paper or using a digital app on your phone?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e04"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Choosing a University Major and Career Interest",
                Content = "Topic: Academic Major and Career. Please answer the following questions:\n1. Why did you choose your current major at university?\n2. Which subject in your major do you enjoy the most?\n3. What kind of company would you like to work for after graduation?\n4. Did your parents support your choice of major?",
                SampleAnswer = "I chose Software Engineering because I love building practical applications that solve everyday problems for users. My favorite subject is Backend Web Development with .NET because designing clean APIs and databases is logical and rewarding. After graduation, I would love to work for an international product company where I can collaborate with global engineers. My parents wholeheartedly supported my choice and encouraged me to follow my passion for technology.",
                KeyPoints = "[\"Explain motivation for choosing major and favorite course\",\"Describe ideal future workplace and parental support\",\"Use appropriate academic and career vocabulary\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "If you could study a second minor subject outside of IT, what subject would you choose?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e05"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Time Management and Overcoming Procrastination",
                Content = "Topic: Productivity. Please answer the following questions:\n1. What time of day are you most productive?\n2. What distractions usually cause you to procrastinate?\n3. What simple trick do you use to start a task you don't feel like doing?\n4. How do you feel at the end of a productive day?",
                SampleAnswer = "I am most productive in the morning between 7:00 AM and 11:00 AM when my energy is high and the house is quiet. Social media notifications and short video feeds are the main distractions that tempt me to procrastinate. When I don't feel like starting a hard task, I use the five-minute rule: I tell myself to work on it for just five minutes, which builds momentum to keep going. At the end of a productive day, I feel proud, peaceful, and ready to rest.",
                KeyPoints = "[\"Identify peak productivity hours and common distractions\",\"Explain a practical anti-procrastination technique (e.g., 5-minute rule, Pomodoro)\",\"Express positive feelings associated with productivity\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Do you listen to background music while working, or do you need complete silence?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e06"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Mentors, Advice, and Important Decisions",
                Content = "Topic: Advice and Decisions. Please answer the following questions:\n1. Who do you usually ask for advice when making an important decision?\n2. What is the best piece of advice your parents or teachers ever gave you?\n3. Do you prefer making decisions quickly or taking time to research carefully?\n4. How do you feel when you help a younger student with their studies?",
                SampleAnswer = "When making an important academic or career decision, I usually consult both my parents and my academic advisor. The best advice my father ever gave me was to focus on mastering fundamentals rather than chasing shortcuts. I prefer taking time to research options and list pros and cons carefully before committing to a major decision. Whenever I help a younger freshman debug their code, I feel happy and find that teaching others deepens my own understanding.",
                KeyPoints = "[\"Identify trusted advisors and share a memorable piece of advice\",\"Explain personal decision-making style\",\"Reflect on the value of mentoring younger peers\"]",
                Difficulty = QuestionDifficulty.Easy,
                BloomLevel = BloomLevel.Understand,
                HasFollowUp = true,
                FollowUpPrompt = "Why is it sometimes helpful to write down the pros and cons on paper before making a choice?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e07"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Monologue: A Difficult Decision You Had to Make",
                Content = "Description Monologue: Describe an important or difficult decision you had to make in your academic or personal life.\nYou should discuss:\n- What the options were and why the decision was difficult\n- Who you consulted or how you weighed the pros and cons\n- What final choice you made and why\n- How that decision shaped your current path.",
                SampleAnswer = "A difficult decision I faced at the end of high school was choosing between studying Economics at a local university near home or pursuing Software Engineering at FPT University in a new city. The choice was tough because staying near home was comfortable, whereas moving away required financial discipline and independence. After discussing with my parents and hablar with alumni about industry prospects, I chose Software Engineering because of my passion for building technology. Looking back, that decision transformed my confidence and opened doors to exciting engineering projects.",
                KeyPoints = "[\"Present the dilemma and competing options clearly\",\"Explain analytical process of weighing trade-offs and seeking counsel\",\"Reflect on long-term impact of the decision\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "When making a big life decision, should people listen more to logical analysis or intuition?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e08"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Monologue: Learning from a Setback or Failure",
                Content = "Description Monologue: Describe a time when you did not achieve a goal on your first attempt and had to try again.\nYou should discuss:\n- What the goal or competition was\n- Why your first attempt did not succeed\n- What adjustments you made for the second attempt\n- How this setback made you stronger.",
                SampleAnswer = "During my freshman year, I applied to join the core technical team of our campus programming club, but I failed the live coding interview on my first attempt. I realized my failure happened because I memorized solutions without truly understanding data structures and panicked under time pressure. Over the next semester, I practiced solving two algorithm problems every morning and did mock interviews with a friend. When I reapplied the following term, I passed with top marks and learned that failure is simply a stepping stone when paired with honest reflection.",
                KeyPoints = "[\"Describe initial failure honestly and identify root cause\",\"Detail concrete corrective actions taken before retrying\",\"Connect perseverance to personal growth\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "Why are many people afraid to talk about their failures, and how can we change that culture?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e09"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Monologue: Your Five-Year Professional Career Plan",
                Content = "Description Monologue: Describe your five-year career vision after graduating from university.\nYou should discuss:\n- What professional role or specialization you aim to achieve\n- What certifications, languages, or technical skills you need to build\n- What challenges you expect in the industry\n- Why this career path gives you a sense of purpose.",
                SampleAnswer = "Over the next five years after graduation, my goal is to grow from a junior backend developer into a Lead Cloud & AI System Architect. To reach this milestone, I am focusing on mastering distributed systems in .NET, cloud infrastructure, and fluent professional English communication. I anticipate rapid changes in AI tooling and cybersecurity requirements, which will demand continuous lifelong learning. This career path gives me a deep sense of purpose because designing reliable software systems can positively impact thousands of users every day.",
                KeyPoints = "[\"Articulate a coherent 5-year career progression roadmap\",\"Identify specific technical and language competencies required\",\"Connect career ambition to continuous learning and purpose\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "How often should professionals review and adjust their five-year career plan as technology evolves?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e0a"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 1 - Monologue: A Team Project Achievement You Are Proud Of",
                Content = "Description Monologue: Describe a challenging team project at university that you and your teammates completed successfully.\nYou should discuss:\n- What the project was about and your specific responsibility\n- How your team coordinated tasks and resolved disagreements\n- What the final result was and why you are proud of it.",
                SampleAnswer = "A team achievement I am deeply proud of is building our Capstone Oral Exam System with three fellow software engineering teammates. My responsibility was designing the backend architecture, real-time SignalR notification hubs, and automated grading workers. Whenever we had different views on API contracts, we held short design reviews grounded in objective test evidence rather than personal ego. Delivering a complete, verified system with over one thousand passing tests taught us the true power of disciplined engineering teamwork.",
                KeyPoints = "[\"Explain project scope and individual technical ownership\",\"Describe collaborative conflict resolution and coordination\",\"Highlight measurable outcome and shared team pride\"]",
                Difficulty = QuestionDifficulty.Medium,
                BloomLevel = BloomLevel.Apply,
                HasFollowUp = true,
                FollowUpPrompt = "What is the single most important factor that separates a high-performing student team from a struggling one?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e0b"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 2 - Discussion: Artificial Intelligence, Automation, and the Future of Work",
                Content = "Extended Discussion on Technology and Careers:\n1. How is Artificial Intelligence transforming the skills required of newly graduated engineers and business professionals?\n2. Which human qualities—such as critical thinking, empathy, and ethical judgment—cannot be replaced by AI?\n3. How should university students use AI tools responsibly to accelerate learning rather than become dependent on them?",
                SampleAnswer = "Artificial Intelligence is automating repetitive coding and routine drafting, shifting the value of young professionals toward system architecture, problem formulation, and rigorous verification. Human qualities such as genuine empathy, ethical accountability, cross-cultural negotiation, and first-principles critical thinking remain irreplaceable because AI lacks lived human responsibility. Students should treat AI as an interactive tutor and pair-programming partner to challenge their understanding, while always verifying every output independently.",
                KeyPoints = "[\"Analyze how AI shifts entry-level job expectations\",\"Defend uniquely human strengths: critical thinking, ethics, empathy\",\"Formulate responsible, verification-driven practices for student AI usage\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "In oral examinations, why is evaluating a student's live spoken reasoning and follow-up defense more authentic than grading take-home essays?",
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000006e0c"),
                CourseId = ent503Course.Id,
                RubricId = RubricEnt503Id,
                Title = "ENT503 Part 2 - Discussion: Defining Personal Success, Financial Ambition, and Social Contribution",
                Content = "Extended Discussion on Success and Values:\n1. How do different people define 'success'—is it measured more by financial wealth, career title, or positive impact on others?\n2. What happens when young professionals sacrifice their health and family relationships in pursuit of rapid career promotion?\n3. How can a person achieve ambitious professional goals while living a balanced and meaningful life?",
                SampleAnswer = "While society often measures success by salary or job title, enduring fulfillment comes from mastering meaningful craft, uplifting family, and contributing value to the community. When young professionals sacrifice health and relationships for short-term promotion, they frequently experience emptiness and severe burnout even after reaching their financial target. True sustainable success requires aligning daily habits with core values: setting ambitious career milestones while protecting non-negotiable time for health, family, and personal integrity.",
                KeyPoints = "[\"Critically evaluate multidimensional definitions of success\",\"Analyze long-term costs of sacrificing health and family for work\",\"Synthesize a philosophy of ambitious yet balanced living\"]",
                Difficulty = QuestionDifficulty.Hard,
                BloomLevel = BloomLevel.Analyze,
                HasFollowUp = true,
                FollowUpPrompt = "Looking ten years into the future, what achievement would make you feel most proud of yourself?",
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
        var ent001Course = await context.Courses.FirstAsync(c => c.Code == "ENT001", cancellationToken);
        var ent104Course = await context.Courses.FirstAsync(c => c.Code == "ENT104", cancellationToken);
        var ent203Course = await context.Courses.FirstAsync(c => c.Code == "ENT203", cancellationToken);
        var ent303Course = await context.Courses.FirstAsync(c => c.Code == "ENT303", cancellationToken);
        var ent403Course = await context.Courses.FirstAsync(c => c.Code == "ENT403", cancellationToken);
        var ent503Course = await context.Courses.FirstAsync(c => c.Code == "ENT503", cancellationToken);

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
            },
            new()
            {
                Id = ClassEnt001Id,
                Code = "SE1801-ENT001",
                CourseId = ent001Course.Id,
                SemesterId = semesterId,
                LecturerId = lecturerId,
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassEnt104Id,
                Code = "SE1801-ENT104",
                CourseId = ent104Course.Id,
                SemesterId = semesterId,
                LecturerId = lecturerId,
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassEnt203Id,
                Code = "SE1801-ENT203",
                CourseId = ent203Course.Id,
                SemesterId = semesterId,
                LecturerId = lecturerId,
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassEnt303Id,
                Code = "SE1801-ENT303",
                CourseId = ent303Course.Id,
                SemesterId = semesterId,
                LecturerId = lecturerId,
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassEnt403Id,
                Code = "SE1801-ENT403",
                CourseId = ent403Course.Id,
                SemesterId = semesterId,
                LecturerId = lecturerId,
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = ClassEnt503Id,
                Code = "SE1801-ENT503",
                CourseId = ent503Course.Id,
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
                OrderIndex = i + 1,
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
