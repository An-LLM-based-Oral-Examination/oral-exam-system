using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetPracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Persistence;

public class DevelopmentDataSeederTests
{
    private static OralExamDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"SeederTestDb_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. SeedAsync chạy nhiều lần liên tiếp phải đảm bảo tính Idempotent 100% không trùng lặp dữ liệu")]
    public async Task SeedAsync_Should_Be_Idempotent_When_Called_Multiple_Times()
    {
        await using var context = CreateInMemoryContext();

        // Lần chạy 1
        await DevelopmentDataSeeder.SeedAsync(context);

        var coursesCountAfterFirst = await context.Courses.CountAsync();
        var questionsCountAfterFirst = await context.PracticeQuestions.CountAsync();
        var rubricsCountAfterFirst = await context.Rubrics.CountAsync();
        var criteriaCountAfterFirst = await context.RubricCriteria.CountAsync();
        var structuresCountAfterFirst = await context.ExamStructures.CountAsync();
        var practiceSessionsAfterFirst = await context.PracticeSessions.CountAsync();
        var mockSessionsAfterFirst = await context.MockExamSessions.CountAsync();

        practiceSessionsAfterFirst.Should().Be(0, "SeedAsync mặc định không được chèn sẵn phiên PracticeSession lịch sử");
        mockSessionsAfterFirst.Should().Be(0, "SeedAsync mặc định không được chèn sẵn phiên MockExamSession lịch sử");

        // Lần chạy 2
        await DevelopmentDataSeeder.SeedAsync(context);

        (await context.Courses.CountAsync()).Should().Be(coursesCountAfterFirst);
        (await context.PracticeQuestions.CountAsync()).Should().Be(questionsCountAfterFirst);
        (await context.Rubrics.CountAsync()).Should().Be(rubricsCountAfterFirst);
        (await context.RubricCriteria.CountAsync()).Should().Be(criteriaCountAfterFirst);
        (await context.ExamStructures.CountAsync()).Should().Be(structuresCountAfterFirst);
        (await context.PracticeSessions.CountAsync()).Should().Be(0);
        (await context.MockExamSessions.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "2. Retained course seeds preserve balanced questions, CLOs, rubrics and structures")]
    public async Task SeedAsync_Should_Seed_All_Retained_Courses_With_Balanced_Questions_Rubrics_And_Structures()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var expectedCourseCodes = new[]
        {
            "PRN231",
            "SWT301", "SWR302", "SWD392",
            "ENT001", "ENT104", "ENT203", "ENT303", "ENT403", "ENT503"
        };
        var expectedCloCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["PRN231"] = 4,
            ["SWT301"] = 10,
            ["SWR302"] = 9,
            ["SWD392"] = 7,
            ["ENT001"] = 4,
            ["ENT104"] = 4,
            ["ENT203"] = 4,
            ["ENT303"] = 4,
            ["ENT403"] = 4,
            ["ENT503"] = 4
        };
        var courses = await context.Courses
            .Include(c => c.Rubrics)
                .ThenInclude(r => r.Criteria)
            .Include(c => c.ExamStructures)
            .Include(c => c.PracticeQuestions)
            .Include(c => c.CourseOfferings)
            .Where(c => expectedCourseCodes.Contains(c.Code))
            .ToListAsync();

        courses.Should().HaveCount(10);

        foreach (var course in courses)
        {
            course.IsActive.Should().BeTrue($"Học phần {course.Code} phải ở trạng thái active");
            course.MaxMockExamsPerDay.Should().Be(3, $"Học phần {course.Code} phải có hạn ngạch thi thử = 3 lượt/ngày theo chuẩn Admin");
            course.CourseOfferings.Should().Contain(o => o.IsEnabled, $"Học phần {course.Code} phải có CourseOffering đang bật");

            // Kiểm tra Rubric chuẩn tổng 10.00đ
            course.Rubrics.Should().NotBeEmpty($"Học phần {course.Code} phải có Rubric");
            foreach (var activeRubric in course.Rubrics.Where(r => r.IsActive))
            {
                activeRubric.TotalMaxScore.Should().Be(10.00m);
                if (course.Code == "PRN231")
                {
                    activeRubric.Criteria.Should().HaveCount(3);
                }
                else if (course.Code.StartsWith("ENT", StringComparison.OrdinalIgnoreCase))
                {
                    activeRubric.Criteria.Should().HaveCount(5);
                }
                else
                {
                    activeRubric.Criteria.Count.Should().BeGreaterThanOrEqualTo(2);
                }
                activeRubric.Criteria.Sum(c => c.MaxScore).Should().Be(10.00m);
                activeRubric.Criteria.Sum(c => c.Weight).Should().Be(1.00m);
            }

            // Kiểm tra ExamStructure active
            course.ExamStructures.Should().Contain(s => s.IsActive && s.TotalQuestions == 5);

            // Kiểm tra đủ >= 12 câu hỏi PracticeQuestions (>= 4 Easy, >= 4 Medium, >= 4 Hard)
            var activeQuestions = course.PracticeQuestions.Where(q => q.IsActive && !q.IsDeleted).ToList();
            activeQuestions.Count.Should().BeGreaterThanOrEqualTo(12, $"Học phần {course.Code} phải có tối thiểu 12 câu hỏi luyện tập/thi thử");

            activeQuestions.Count(q => q.Difficulty == QuestionDifficulty.Easy).Should().BeGreaterThanOrEqualTo(4);
            activeQuestions.Count(q => q.Difficulty == QuestionDifficulty.Medium).Should().BeGreaterThanOrEqualTo(4);
            activeQuestions.Count(q => q.Difficulty == QuestionDifficulty.Hard).Should().BeGreaterThanOrEqualTo(4);

            var activeCloIds = (await context.CourseClos
                .Where(c => c.CourseId == course.Id && c.IsActive)
                .Select(c => c.Id)
                .ToListAsync())
                .ToHashSet();
            activeCloIds.Should().HaveCount(expectedCloCounts[course.Code], $"Học phần {course.Code} phải có đúng {expectedCloCounts[course.Code]} CLOs active");

            foreach (var q in activeQuestions)
            {
                q.CloId.Should().NotBeNull($"Câu hỏi '{q.Title}' ({course.Code}) phải có CloId hợp lệ cho MF-05 SELECTED_CLOS");
                activeCloIds.Should().Contain(q.CloId!.Value, $"CloId của câu hỏi '{q.Title}' phải thuộc danh sách CLO active của {course.Code}");
                q.SampleAnswer.Should().NotBeNullOrWhiteSpace();
                q.SampleAnswer!.Length.Should().BeGreaterThanOrEqualTo(50, $"Câu hỏi '{q.Title}' phải có câu trả lời mẫu >= 50 ký tự");
                if (q.HasFollowUp)
                {
                    q.FollowUpPrompt.Should().NotBeNullOrWhiteSpace($"Câu hỏi '{q.Title}' có HasFollowUp = true phải có FollowUpPrompt");
                }

                var keyPoints = JsonSerializer.Deserialize<List<string>>(q.KeyPoints);
                keyPoints.Should().NotBeNullOrEmpty($"Câu hỏi '{q.Title}' phải có KeyPoints JSON array hợp lệ");
            }

            foreach (var cloId in activeCloIds)
            {
                var cloQuestions = activeQuestions.Where(q => q.CloId == cloId).ToList();
                cloQuestions.Count(q => q.Difficulty == QuestionDifficulty.Easy).Should().BeGreaterThanOrEqualTo(1, $"CLO {cloId} của {course.Code} phải có câu Easy");
                cloQuestions.Count(q => q.Difficulty == QuestionDifficulty.Medium).Should().BeGreaterThanOrEqualTo(1, $"CLO {cloId} của {course.Code} phải có câu Medium");
                cloQuestions.Count(q => q.Difficulty == QuestionDifficulty.Hard).Should().BeGreaterThanOrEqualTo(1, $"CLO {cloId} của {course.Code} phải có câu Hard");
            }
        }
    }

    [Fact(DisplayName = "3. Dữ liệu Seed cho phép chạy mượt mà Full-Session Progressive 10 câu (MF-01) và khởi tạo đề Thi thử 5 câu (MF-02) trên tất cả các môn")]
    public async Task Seeded_Data_Should_Support_FullSession_10_Questions_And_StartMockExam_For_All_Retained_Courses()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var startPracticeHandler = new StartPracticeSessionCommandHandler(context);
        var startMockExamHandler = new StartMockExamCommandHandler(context);

        var courseIds = new[]
        {
            DevelopmentDataSeeder.CoursePrn231Id,
            AcademicCourseBankImporter.CourseSwt301Id,
            AcademicCourseBankImporter.CourseSwr302Id,
            AcademicCourseBankImporter.CourseSwd392Id,
            DevelopmentDataSeeder.CourseEnt001Id,
            DevelopmentDataSeeder.CourseEnt104Id,
            DevelopmentDataSeeder.CourseEnt203Id,
            DevelopmentDataSeeder.CourseEnt303Id,
            DevelopmentDataSeeder.CourseEnt403Id,
            DevelopmentDataSeeder.CourseEnt503Id
        };

        foreach (var courseId in courseIds)
        {
            // Kiểm tra MF-01 Full-Session 10 câu
            var practiceResult = await startPracticeHandler.Handle(
                new StartPracticeSessionCommand(
                    StudentId: DevelopmentDataSeeder.StudentHoangId,
                    CourseId: courseId,
                    Difficulty: "progressive",
                    QuestionCount: 10,
                    IsFullSession: true,
                    Topic: null,
                    Difficulties: new List<string> { "easy", "medium", "hard" }),
                CancellationToken.None);

            practiceResult.IsSuccess.Should().BeTrue($"MF-01 Full-Session 10 câu phải thành công cho môn {courseId}");
            practiceResult.Value.Questions.Should().HaveCount(10);

            // Kiểm tra MF-01 Per-Question On-Demand trên cả 3 độ khó
            foreach (var diff in new[] { "easy", "medium", "hard" })
            {
                var perQResult = await startPracticeHandler.Handle(
                    new StartPracticeSessionCommand(
                        StudentId: DevelopmentDataSeeder.StudentHoangId,
                        CourseId: courseId,
                        Difficulty: diff,
                        QuestionCount: null,
                        IsFullSession: false,
                        Topic: null,
                        Difficulties: new List<string> { diff }),
                    CancellationToken.None);

                perQResult.IsSuccess.Should().BeTrue($"MF-01 Per-Question ({diff}) phải thành công cho môn {courseId}");
                perQResult.Value.Questions.Should().HaveCount(1);
            }

            // Kiểm tra MF-02 StartMockExam 5 câu
            var mockResult = await startMockExamHandler.Handle(
                new StartMockExamCommand(
                    CourseId: courseId,
                    StudentId: DevelopmentDataSeeder.StudentHoangId,
                    HasFollowUp: true),
                CancellationToken.None);

            mockResult.IsSuccess.Should().BeTrue($"MF-02 StartMockExam phải thành công cho môn {courseId}");
            mockResult.Value.Questions.Should().HaveCount(5);
        }
    }

    [Fact(DisplayName = "4. SeedAsync giữ lịch sử sinh viên rỗng cho UAT sạch, và SeedStudentPracticeAndMockExamHistoryAsync tạo sẵn lịch sử kèm Scorecard chi tiết cho mọi sinh viên khi cần")]
    public async Task SeedAsync_Should_Keep_History_Empty_And_SeedStudentPracticeAndMockExamHistoryAsync_Should_Populate_History()
    {
        await using var context = CreateInMemoryContext();

        // Thêm 1 tài khoản sinh viên đăng nhập Google mới để kiểm tra tính năng tự động tạo lịch sử cho mọi student
        var googleStudentId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = googleStudentId,
            Email = "sinhvien.google@fpt.edu.vn",
            FullName = "Sinh Vien Google OAuth",
            StudentCode = "SE189999",
            Role = UserRole.Student,
            IsActive = true
        });
        await context.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(context);

        var targetStudents = new[]
        {
            DevelopmentDataSeeder.StudentHoangId,
            DevelopmentDataSeeder.StudentMaiId,
            googleStudentId
        };

        var practiceHistoryHandler = new GetStudentPracticeHistoryQueryHandler(context);
        var practiceDetailHandler = new GetPracticeSessionQueryHandler(context);
        var mockHistoryHandler = new GetMockExamHistoryQueryHandler(context);
        var mockDetailHandler = new GetMockExamSessionDetailQueryHandler(context);

        // 1. Kiểm tra sau SeedAsync, lịch sử sinh viên hoàn toàn rỗng (Zero-Mock / UAT sạch)
        foreach (var studentId in targetStudents)
        {
            var initialPracticeRes = await practiceHistoryHandler.Handle(
                new GetStudentPracticeHistoryQuery(studentId),
                CancellationToken.None);
            initialPracticeRes.IsSuccess.Should().BeTrue();
            initialPracticeRes.Value.Should().BeEmpty();

            var initialMockRes = await mockHistoryHandler.Handle(
                new GetMockExamHistoryQuery(studentId, null, 1, 10),
                CancellationToken.None);
            initialMockRes.IsSuccess.Should().BeTrue();
            initialMockRes.Value.Items.Should().BeEmpty();
        }

        // 2. Gọi tường minh phương thức SeedStudentPracticeAndMockExamHistoryAsync để tạo dữ liệu mẫu khi cần
        await DevelopmentDataSeeder.SeedStudentPracticeAndMockExamHistoryAsync(context);

        // 3. Kiểm tra rằng lịch sử được tạo thành công (1 session PRN231) kèm Scorecard chi tiết
        foreach (var studentId in targetStudents)
        {
            // Kiểm tra lịch sử MF-01
            var practiceHistoryRes = await practiceHistoryHandler.Handle(
                new GetStudentPracticeHistoryQuery(studentId),
                CancellationToken.None);

            practiceHistoryRes.IsSuccess.Should().BeTrue();
            practiceHistoryRes.Value.Should().HaveCount(1);

            foreach (var item in practiceHistoryRes.Value)
            {
                item.Status.Should().Be(PracticeSessionStatus.Completed);
                item.AverageScore.Should().NotBeNull();
                item.AverageScore.Should().BeGreaterThan(0m);

                var detailRes = await practiceDetailHandler.Handle(
                    new GetPracticeSessionQuery(item.SessionId),
                    CancellationToken.None);
                detailRes.IsSuccess.Should().BeTrue();
                detailRes.Value.Answers.Should().NotBeEmpty();
                detailRes.Value.Answers.All(a => a.CriteriaScores.Count == 3).Should().BeTrue();
            }

            // Kiểm tra lịch sử MF-02
            var mockHistoryRes = await mockHistoryHandler.Handle(
                new GetMockExamHistoryQuery(studentId, null, 1, 10),
                CancellationToken.None);

            mockHistoryRes.IsSuccess.Should().BeTrue();
            mockHistoryRes.Value.Items.Should().HaveCount(1);

            foreach (var mockItem in mockHistoryRes.Value.Items)
            {
                mockItem.Status.Should().Be("graded");
                mockItem.TotalScore.Should().NotBeNull();
                mockItem.TotalScore.Should().BeGreaterThan(0m);
                mockItem.AnsweredQuestions.Should().Be(5);
                mockItem.TotalQuestions.Should().Be(5);

                var scorecardRes = await mockDetailHandler.Handle(
                    new GetMockExamSessionDetailQuery(mockItem.SessionId, studentId),
                    CancellationToken.None);
                scorecardRes.IsSuccess.Should().BeTrue();
                scorecardRes.Value.Questions.Should().HaveCount(5);
                scorecardRes.Value.Questions.All(q => q.CriteriaScores.Count == 3).Should().BeTrue();
            }
        }
    }

    [Fact(DisplayName = "5. SeedAsync gỡ cờ MustChangePassword cho 6 tài khoản Dev và khởi tạo đầy đủ ExamQuestions (MF-03), Bằng chứng Hậu kiểm & Phúc khảo (MF-04)")]
    public async Task SeedAsync_Should_Reset_MustChangePassword_And_Seed_ExamQuestions_And_AuditEvidence()
    {
        await using var context = CreateInMemoryContext();

        // Giả lập trạng thái từ Migration: tài khoản sinh viên có MustChangePassword = true
        context.Users.Add(new User
        {
            Id = DevelopmentDataSeeder.StudentHoangId,
            Email = "hoanglvse170001@fpt.edu.vn",
            FullName = "Le Vu Hoang",
            StudentCode = "SE170001",
            Username = "hoalvse170001",
            MustChangePassword = true,
            Role = UserRole.Student,
            IsActive = true
        });
        await context.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(context);

        // 1. Kiểm tra 6 tài khoản dev đều có MustChangePassword == false và đầy đủ Username + PasswordHash
        var devUserIds = new[]
        {
            DevelopmentDataSeeder.AdminUserId,
            DevelopmentDataSeeder.DepartmentHeadUserId,
            DevelopmentDataSeeder.LecturerUserId,
            DevelopmentDataSeeder.ProctorUserId,
            DevelopmentDataSeeder.StudentHoangId,
            DevelopmentDataSeeder.StudentMaiId
        };

        var devUsers = await context.Users
            .Where(u => devUserIds.Contains(u.Id))
            .ToListAsync();

        devUsers.Should().HaveCount(6);
        foreach (var devUser in devUsers)
        {
            devUser.MustChangePassword.Should().BeFalse($"Tài khoản dev {devUser.Email} không được bật cờ MustChangePassword");
            devUser.Username.Should().NotBeNullOrWhiteSpace();
            devUser.PasswordHash.Should().NotBeNullOrWhiteSpace();
        }

        // 2. Kiểm tra PRN231 ExamQuestions có 4 câu hỏi và đủ 4 trạng thái duyệt (SWT301/SWR302/SWD392 có 0 ExamQuestions giả theo R1)
        var examQuestions = await context.ExamQuestions.ToListAsync();
        examQuestions.Count.Should().BeGreaterThanOrEqualTo(4);
        examQuestions.Should().Contain(q => q.ApprovalStatus == ApprovalStatus.Draft);
        examQuestions.Should().Contain(q => q.ApprovalStatus == ApprovalStatus.SubmittedForReview);
        examQuestions.Should().Contain(q => q.ApprovalStatus == ApprovalStatus.NeedsRevision && !string.IsNullOrWhiteSpace(q.ReviewNotes));
        examQuestions.Should().Contain(q => q.ApprovalStatus == ApprovalStatus.Approved && q.ApprovedBy == DevelopmentDataSeeder.DepartmentHeadUserId);
        examQuestions.All(q => q.SubmittedBy == DevelopmentDataSeeder.LecturerUserId).Should().BeTrue();
        examQuestions.All(q => !string.IsNullOrWhiteSpace(q.SampleAnswer) && q.SampleAnswer.Length >= 60).Should().BeTrue();

        // 3. Kiểm tra ExamQuestionSubmissions và AiEvaluations cho ca thi 00000000-0000-0000-0000-000000000023
        var shiftTickets = await context.StudentExamTickets
            .Include(t => t.Submissions)
                .ThenInclude(s => s.AiEvaluations)
                    .ThenInclude(e => e.Details)
            .Where(t => t.ShiftId == DevelopmentDataSeeder.OfficialShiftPrn231Id)
            .ToListAsync();

        shiftTickets.Should().HaveCountGreaterThanOrEqualTo(2);

        var submissions = shiftTickets.SelectMany(t => t.Submissions).ToList();
        submissions.Count.Should().BeGreaterThanOrEqualTo(2);
        submissions.Should().Contain(s => s.AudioR2Url!.EndsWith("01_SE170001.webm") && s.AiEvaluations.Any(e => e.ConfidenceScore < 0.70m));
        submissions.Should().Contain(s => s.AudioR2Url!.EndsWith("02_SE170002.webm") && s.AiEvaluations.Any(e => e.ConfidenceScore >= 0.70m));

        // 4. Kiểm tra AppealRequest đã được gắn SubmissionId và khớp workflow 5 bước
        var appeal = await context.AppealRequests.FirstOrDefaultAsync(a => a.Id == DevelopmentDataSeeder.AppealHoangId);
        appeal.Should().NotBeNull();
        appeal!.SubmissionId.Should().NotBeNull();
        appeal.Status.Should().Be(AppealStatus.AssignedToLecturer);
        appeal.AssignedTo.Should().Be(DevelopmentDataSeeder.Lecturer2UserId);
    }

    [Fact(DisplayName = "6. SeedAsync nạp đủ SWT301 (50Q), SWR302 (45Q), SWD392 (36Q) kèm ExamStructure MF-02, 0 ExamQuestion giả và ẩn mềm PRF192/CSD201/DBI202")]
    public async Task SeedAsync_Should_Import_Swt301_Swr302_Swd392_With_Plus27_And_SoftHide_Legacy_Three_Courses()
    {
        await using var context = CreateInMemoryContext();

        // Giả lập DB đã có sẵn 3 môn cũ PRF192, CSD201, DBI202 cùng CourseOffering, Class và Shift đang mở
        var sem = new Semester
        {
            Id = DevelopmentDataSeeder.SemesterFa26Id,
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        context.Semesters.Add(sem);
        context.Courses.AddRange(
            new Course { Id = DevelopmentDataSeeder.CoursePrf192Id, Code = "PRF192", Name = "Programming Fundamentals", SemesterId = sem.Id, IsActive = true },
            new Course { Id = DevelopmentDataSeeder.CourseCsd201Id, Code = "CSD201", Name = "Data Structures and Algorithms", SemesterId = sem.Id, IsActive = true },
            new Course { Id = DevelopmentDataSeeder.CourseDbi202Id, Code = "DBI202", Name = "Introduction to Databases", SemesterId = sem.Id, IsActive = true });
        context.CourseOfferings.AddRange(
            new CourseOffering { Id = DevelopmentDataSeeder.OfferingPrf192Fa26Id, CourseId = DevelopmentDataSeeder.CoursePrf192Id, SemesterId = sem.Id, IsEnabled = true },
            new CourseOffering { Id = DevelopmentDataSeeder.OfferingCsd201Fa26Id, CourseId = DevelopmentDataSeeder.CourseCsd201Id, SemesterId = sem.Id, IsEnabled = true },
            new CourseOffering { Id = DevelopmentDataSeeder.OfferingDbi202Fa26Id, CourseId = DevelopmentDataSeeder.CourseDbi202Id, SemesterId = sem.Id, IsEnabled = true });
        context.Classes.Add(new Class
        {
            Id = DevelopmentDataSeeder.ClassPrf192Id,
            CourseId = DevelopmentDataSeeder.CoursePrf192Id,
            SemesterId = sem.Id,
            Code = "SE1804-PRF",
            Name = "Lớp SE1804-PRF",
            JoinCode = "SE1804PRF",
            IsActive = true,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true
        });
        context.InClassExamShifts.Add(new InClassExamShift
        {
            Id = DevelopmentDataSeeder.InClassShiftPrf192Id,
            ClassId = DevelopmentDataSeeder.ClassPrf192Id,
            LecturerId = DevelopmentDataSeeder.LecturerUserId,
            ShiftName = "Legacy PRF shift",
            TotpSecretKey = "U0VDUkVU",
            Status = "IN_PROGRESS",
            IsLocked = false,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(2)
        });
        await context.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(context);

        // 1. Kiểm tra 3 môn mới (SWT301, SWR302, SWD392)
        var expectedNewCourses = new (Guid CourseId, string Code, int Clos, int TotalQ, int Easy, int Medium, int Hard)[]
        {
            (AcademicCourseBankImporter.CourseSwt301Id, "SWT301", 10, 50, 20, 20, 10),
            (AcademicCourseBankImporter.CourseSwr302Id, "SWR302", 9, 45, 18, 18, 9),
            (AcademicCourseBankImporter.CourseSwd392Id, "SWD392", 7, 36, 9, 20, 7)
        };

        foreach (var (courseId, code, expectedCloCount, totalQ, easy, medium, hard) in expectedNewCourses)
        {
            var course = await context.Courses.AsNoTracking().FirstAsync(c => c.Id == courseId);
            course.Code.Should().Be(code);
            course.IsActive.Should().BeTrue();
            course.MaxMockExamsPerDay.Should().Be(3);
            course.HasFollowUp.Should().BeFalse();

            var clos = await context.CourseClos
                .AsNoTracking()
                .Where(c => c.CourseId == courseId && c.IsActive)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();
            clos.Should().HaveCount(expectedCloCount);
            var cloIds = clos.Select(c => c.Id).ToHashSet();

            var practiceQs = await context.PracticeQuestions
                .AsNoTracking()
                .Where(q => q.CourseId == courseId && q.IsActive && !q.IsDeleted)
                .ToListAsync();
            practiceQs.Should().HaveCount(totalQ);
            practiceQs.Count(q => q.Difficulty == QuestionDifficulty.Easy).Should().Be(easy);
            practiceQs.Count(q => q.Difficulty == QuestionDifficulty.Medium).Should().Be(medium);
            practiceQs.Count(q => q.Difficulty == QuestionDifficulty.Hard).Should().Be(hard);

            foreach (var pq in practiceQs)
            {
                pq.CloId.Should().NotBeNull();
                cloIds.Should().Contain(pq.CloId!.Value);
                pq.ApprovedBy.Should().BeNull();
                pq.SubmittedBy.Should().BeNull();
                pq.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
                pq.SampleAnswer.Should().NotBeNullOrWhiteSpace();
                pq.SampleAnswer!.Length.Should().BeGreaterThanOrEqualTo(50);
            }

            // 0 ExamQuestions giả cho SWT301/SWR302/SWD392
            (await context.ExamQuestions.AnyAsync(eq => eq.CourseId == courseId)).Should().BeFalse();

            // ExamStructure MF-02 active chuẩn 5Q / 30m / Understand:2, Apply:2, Analyze:1
            var activeStruct = await context.ExamStructures.AsNoTracking()
                .FirstOrDefaultAsync(s => s.CourseId == courseId && s.IsActive);
            activeStruct.Should().NotBeNull();
            activeStruct!.TotalQuestions.Should().Be(5);
            activeStruct.DurationMinutes.Should().Be(30);
            var bloom = JsonSerializer.Deserialize<Dictionary<string, int>>(activeStruct.BloomDistribution);
            bloom.Should().Contain(new KeyValuePair<string, int>("Understand", 2));
            bloom.Should().Contain(new KeyValuePair<string, int>("Apply", 2));
            bloom.Should().Contain(new KeyValuePair<string, int>("Analyze", 1));
        }

        // 2. Kiểm tra 3 môn cũ bị ẩn mềm (giữ nguyên bản ghi, không DELETE)
        var legacyCodes = new[] { "PRF192", "CSD201", "DBI202" };
        var legacyCourses = await context.Courses.AsNoTracking().Where(c => legacyCodes.Contains(c.Code)).ToListAsync();
        legacyCourses.Should().HaveCount(3);
        legacyCourses.Should().OnlyContain(c => !c.IsActive);

        var legacyOfferings = await context.CourseOfferings.AsNoTracking()
            .Where(o => legacyCourses.Select(c => c.Id).Contains(o.CourseId))
            .ToListAsync();
        legacyOfferings.Should().HaveCount(3);
        legacyOfferings.Should().OnlyContain(o => !o.IsEnabled);

        var legacyClass = await context.Classes.AsNoTracking().FirstAsync(c => c.Id == DevelopmentDataSeeder.ClassPrf192Id);
        legacyClass.IsActive.Should().BeFalse();
        legacyClass.IsJoinEnabled.Should().BeFalse();

        var legacyShift = await context.InClassExamShifts.AsNoTracking().FirstAsync(s => s.Id == DevelopmentDataSeeder.InClassShiftPrf192Id);
        legacyShift.Status.Should().Be("CLOSED");
    }

    [Fact(DisplayName = "7. SeedAsync khởi tạo Ca thi thật Kiosk (MF-04) ở trạng thái Ready-to-Test (in_progress, published) với vé thi IP 127.0.0.1 và ::1")]
    public async Task SeedAsync_Should_Seed_ReadyToTest_MF04_LabKiosk_Shift_And_Tickets()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var readyShift = await context.RealExamSessionShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == DevelopmentDataSeeder.OfficialShiftReadyToTestPrn231Id);

        readyShift.Should().NotBeNull("Ca thi Kíp 2 Lab 302 (Ready-to-Test) phải được khởi tạo");
        readyShift!.Status.Should().Be("in_progress");
        readyShift.IsExamPublished.Should().BeTrue("Đề thi phải được xuất bản sẵn sàng để Kiosk tải đề");
        readyShift.RoomLab.Should().Be("Lab 302");
        readyShift.StartTime.Should().BeBefore(DateTime.UtcNow);
        readyShift.EndTime.Should().BeAfter(DateTime.UtcNow);

        var tickets = await context.StudentExamTickets
            .AsNoTracking()
            .Where(t => t.ShiftId == readyShift.Id)
            .ToListAsync();

        tickets.Should().HaveCount(3);

        var hoangTicket = tickets.FirstOrDefault(t => t.Id == DevelopmentDataSeeder.TicketReadyToTestHoangId);
        hoangTicket.Should().NotBeNull();
        hoangTicket!.IpAddress.Should().Be("127.0.0.1", "Vé của Hoàng phải gắn IP 127.0.0.1 để test trên localhost Kiosk");
        hoangTicket.Status.Should().Be(ExamTicketStatus.Scheduled);
        hoangTicket.IsLocked.Should().BeFalse();

        var thanhTicket = tickets.FirstOrDefault(t => t.Id == DevelopmentDataSeeder.TicketReadyToTestThanhId);
        thanhTicket.Should().NotBeNull();
        thanhTicket!.IpAddress.Should().Be("::1", "Vé của Thành phải gắn IP IPv6 loopback ::1");
        thanhTicket.Status.Should().Be(ExamTicketStatus.Scheduled);

        var maiTicket = tickets.FirstOrDefault(t => t.Id == DevelopmentDataSeeder.TicketReadyToTestMaiId);
        maiTicket.Should().NotBeNull();
        maiTicket!.IpAddress.Should().Be("192.168.1.103", "Vé của Mai phải gắn IP LAN test");
        maiTicket.Status.Should().Be(ExamTicketStatus.Scheduled);
        maiTicket.ExamSetId.Should().BeNull(
            "vé chờ phân đề không được tái sử dụng bộ đề đã gán cho sinh viên khác trong cùng ca");

        tickets
            .Where(t => t.ExamSetId.HasValue)
            .GroupBy(t => t.ExamSetId)
            .Should()
            .OnlyContain(group => group.Count() == 1,
                "uq_tickets_shift_exam_set cấm hai sinh viên trong cùng ca nhận cùng một bộ đề");
    }

    [Fact(DisplayName = "8. SeedAsync seeds the PRN231 in-class shift with static and dynamic OTP")]
    public async Task SeedAsync_Should_Seed_ReadyToTest_MF05_InClass_Shifts_With_Static_And_Dynamic_Otp()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var inClassShifts = await context.InClassExamShifts
            .AsNoTracking()
            .Where(s => s.Id == DevelopmentDataSeeder.InClassShiftPrn231Id)
            .ToListAsync();

        inClassShifts.Should().HaveCount(1, "only the retained PRN231 sample shift is seeded");

        foreach (var shift in inClassShifts)
        {
            shift.Status.Should().Be("IN_PROGRESS");
            shift.IsLocked.Should().BeFalse();
            shift.StartTime.Should().BeBefore(DateTime.UtcNow);
            shift.EndTime.Should().BeAfter(DateTime.UtcNow);
            shift.TotpSecretKey.Should().NotBeNullOrWhiteSpace();

            // Kiểm tra mật khẩu tĩnh dự phòng "123456"
            var staticPassword = await context.InClassShiftPasswords
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ShiftId == shift.Id && p.PlainCode == "123456");

            staticPassword.Should().NotBeNull($"Ca thi {shift.ShiftName} phải có mật khẩu tĩnh fallback 123456");
            staticPassword!.ValidFrom.Should().BeBefore(DateTime.UtcNow);
            staticPassword.ValidTo.Should().BeAfter(DateTime.UtcNow);
        }
    }

    [Fact(DisplayName = "9. Empirical: MF-04 Lab Kiosk Kíp 2 cho phép sinh viên Hoàng (127.0.0.1) và Thành (::1) truy cập vé thi SCHEDULED, và chặn IP không khớp (10.0.0.99)")]
    public async Task MF04_LabKiosk_WorkstationTicket_Access_ValidAndInvalidIps()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var validator = new GetMyWorkstationTicketQueryValidator();
        var handler = new GetMyWorkstationTicketQueryHandler(context, validator);

        // 1. Sinh viên Hoàng truy cập từ IPv4 Loopback 127.0.0.1 -> Thành công trả về vé SCHEDULED
        var hoangResult = await handler.Handle(
            new GetMyWorkstationTicketQuery(DevelopmentDataSeeder.StudentHoangId, "127.0.0.1", DateTime.UtcNow),
            CancellationToken.None);

        hoangResult.IsSuccess.Should().BeTrue("Sinh viên Hoàng tại máy trạm 127.0.0.1 phải lấy được vé thi");
        hoangResult.Value.Id.Should().Be(DevelopmentDataSeeder.TicketReadyToTestHoangId);
        hoangResult.Value.ShiftId.Should().Be(DevelopmentDataSeeder.OfficialShiftReadyToTestPrn231Id);
        hoangResult.Value.Status.Should().Be("SCHEDULED");
        hoangResult.Value.ShiftStatus.Should().Be("in_progress");
        hoangResult.Value.IsExamPublished.Should().BeTrue();
        hoangResult.Value.StartTime.Should().BeBefore(DateTime.UtcNow);
        hoangResult.Value.EndTime.Should().BeAfter(DateTime.UtcNow);
        hoangResult.Value.Questions.Should().BeNull("Vé ở trạng thái SCHEDULED chưa check-in tuyệt đối không được lộ danh sách câu hỏi");

        // 1b. Sau khi Hoàng check-in gắn thiết bị BYOD sang IN_PROGRESS -> mới trả danh sách câu hỏi từ bộ đề đã xuất bản
        const string hoangDeviceId = "BYOD-HOANG-LAPTOP-0001";
        const string hoangSessionToken = "hoang-kiosk-session-token-0001";
        var hoangTicket = await context.StudentExamTickets.FirstAsync(t => t.Id == DevelopmentDataSeeder.TicketReadyToTestHoangId);
        hoangTicket.Status = ExamTicketStatus.InProgress;
        hoangTicket.DeviceId = hoangDeviceId;
        hoangTicket.DeviceLockedAt = DateTime.UtcNow;
        hoangTicket.DeviceSessionTokenHash = OralExamination.Application.Features.OfficialExams.Common.OfficialExamDeviceGuard.HashSessionToken(hoangSessionToken);
        await context.SaveChangesAsync();

        var hoangCheckedInResult = await handler.Handle(
            new GetMyWorkstationTicketQuery(DevelopmentDataSeeder.StudentHoangId, "127.0.0.1", DateTime.UtcNow, hoangDeviceId, hoangSessionToken),
            CancellationToken.None);

        hoangCheckedInResult.IsSuccess.Should().BeTrue();
        hoangCheckedInResult.Value.Status.Should().Be(ExamTicketStatus.InProgress);
        hoangCheckedInResult.Value.Questions.Should().NotBeNullOrEmpty("Đề thi đã xuất bản phải kèm danh sách câu hỏi cho Kiosk sau khi vé đã check-in sang IN_PROGRESS");

        // 2. Sinh viên Thành truy cập từ IPv6 Loopback ::1 -> Thành công trả về vé SCHEDULED
        var thanhResult = await handler.Handle(
            new GetMyWorkstationTicketQuery(DevelopmentDataSeeder.StudentThanhId, "::1", DateTime.UtcNow),
            CancellationToken.None);

        thanhResult.IsSuccess.Should().BeTrue("Sinh viên Thành tại máy trạm ::1 phải lấy được vé thi");
        thanhResult.Value.Id.Should().Be(DevelopmentDataSeeder.TicketReadyToTestThanhId);
        thanhResult.Value.ShiftId.Should().Be(DevelopmentDataSeeder.OfficialShiftReadyToTestPrn231Id);
        thanhResult.Value.Status.Should().Be("SCHEDULED");
        thanhResult.Value.ShiftStatus.Should().Be("in_progress");

        // 3. BYOD: đổi địa chỉ IP Wi-Fi/NAT (10.0.0.99) vẫn được phép (audit-only), nhưng sai X-Device-Id khi vé đã khóa thiết bị phải bị chặn 409 DEVICE_MISMATCH
        var roamedIpSameDeviceResult = await handler.Handle(
            new GetMyWorkstationTicketQuery(DevelopmentDataSeeder.StudentHoangId, "10.0.0.99", DateTime.UtcNow, hoangDeviceId, hoangSessionToken),
            CancellationToken.None);
        roamedIpSameDeviceResult.IsSuccess.Should().BeTrue("Đổi IP Wi-Fi/NAT trên cùng thiết bị BYOD đã gắn phải được phép (ip_address chỉ dùng kiểm toán)");

        var wrongDeviceResult = await handler.Handle(
            new GetMyWorkstationTicketQuery(DevelopmentDataSeeder.StudentHoangId, "10.0.0.99", DateTime.UtcNow, "BYOD-OTHER-LAPTOP-9999"),
            CancellationToken.None);

        wrongDeviceResult.IsSuccess.Should().BeFalse("Thiết bị BYOD khác với DeviceId đã khóa trên vé phải bị chặn");
        wrongDeviceResult.Error.Should().Be(OfficialExamErrors.DeviceMismatch);
    }

    [Fact(DisplayName = "10. Empirical: MF-05 Ca thi trong lớp PRN231 cho phép Hoàng vào thi bằng OTP tĩnh 123456, Thành vào thi bằng dynamic TOTP, và từ chối SV chưa ghi danh")]
    public async Task MF05_InClass_Shift_Entry_StaticOtp_DynamicTotp_And_NonEnrolledRejection()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var enterValidator = new EnterInClassShiftCommandValidator();
        var enterHandler = new EnterInClassShiftCommandHandler(
            context,
            enterValidator,
            new Mock<IGradingQueueChannel>().Object);

        // 1. Sinh viên Hoàng vào ca thi PRN231 bằng mã OTP tĩnh "123456" -> Thành công nhận bộ câu hỏi anti-collision
        var hoangResult = await enterHandler.Handle(
            new EnterInClassShiftCommand(DevelopmentDataSeeder.StudentHoangId, DevelopmentDataSeeder.InClassShiftPrn231Id, "123456"),
            CancellationToken.None);

        hoangResult.IsSuccess.Should().BeTrue("Sinh viên Hoàng phải vào được ca thi PRN231 bằng OTP tĩnh 123456");
        hoangResult.Value.ShiftId.Should().Be(DevelopmentDataSeeder.InClassShiftPrn231Id);
        hoangResult.Value.StudentId.Should().Be(DevelopmentDataSeeder.StudentHoangId);
        hoangResult.Value.TotalQuestionsPerStudent.Should().Be(2);
        hoangResult.Value.AssignedQuestionIds.Should().HaveCount(2);
        hoangResult.Value.AssignedQuestions.Should().HaveCount(2);
        hoangResult.Value.HasFollowUp.Should().BeTrue();
        hoangResult.Value.MaxFollowUpQuestions.Should().Be(2);

        // 2. Sinh viên Thành vào ca thi PRN231 bằng mã dynamic TOTP sinh từ TotpSecretKey -> Thành công nhận bộ câu hỏi
        var shift = await context.InClassExamShifts.FirstAsync(s => s.Id == DevelopmentDataSeeder.InClassShiftPrn231Id);
        var dynamicOtp = TotpPasswordHelper.GenerateCode(shift.TotpSecretKey, DateTime.UtcNow);

        var thanhResult = await enterHandler.Handle(
            new EnterInClassShiftCommand(DevelopmentDataSeeder.StudentThanhId, DevelopmentDataSeeder.InClassShiftPrn231Id, dynamicOtp),
            CancellationToken.None);

        thanhResult.IsSuccess.Should().BeTrue("Sinh viên Thành phải vào được ca thi PRN231 bằng dynamic TOTP");
        thanhResult.Value.StudentId.Should().Be(DevelopmentDataSeeder.StudentThanhId);
        thanhResult.Value.AssignedQuestionIds.Should().HaveCount(2);
        thanhResult.Value.AssignedQuestions.Should().HaveCount(2);

        // 3. Sinh viên chưa ghi danh vào lớp PRN231 nhập OTP 123456 -> Bị từ chối lỗi StudentNotEnrolled
        var strangerStudentId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = strangerStudentId,
            Email = "stranger.student@fpt.edu.vn",
            FullName = "Sinh Vien Chua Ghi Danh",
            StudentCode = "SE199999",
            Role = UserRole.Student,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var nonEnrolledResult = await enterHandler.Handle(
            new EnterInClassShiftCommand(strangerStudentId, DevelopmentDataSeeder.InClassShiftPrn231Id, "123456"),
            CancellationToken.None);

        nonEnrolledResult.IsSuccess.Should().BeFalse("Sinh viên chưa ghi danh phải bị từ chối");
        nonEnrolledResult.Error.Should().Be(InClassShiftErrors.StudentNotEnrolled);
    }

    [Fact(DisplayName = "11. Empirical: Cơ chế Self-healing tự động phục hồi ca thi MF-04 và MF-05 về IN_PROGRESS kèm khung giờ hợp lệ khi ca thi bị đóng hoặc hết hạn")]
    public async Task SelfHealing_When_Shifts_Closed_Or_Expired_SeedAsync_Restores_Both_Shifts_To_InProgress()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        // 1. Phá vỡ trạng thái: Đóng ca thi MF-04 Lab Kiosk và làm hết hạn khung giờ
        var readyShift = await context.RealExamSessionShifts.FirstAsync(s => s.Id == DevelopmentDataSeeder.OfficialShiftReadyToTestPrn231Id);
        readyShift.Status = "completed";
        readyShift.StartTime = DateTime.UtcNow.AddHours(-10);
        readyShift.EndTime = DateTime.UtcNow.AddHours(-2);
        readyShift.IsExamPublished = false;

        var hoangTicket = await context.StudentExamTickets.FirstAsync(t => t.Id == DevelopmentDataSeeder.TicketReadyToTestHoangId);
        hoangTicket.Status = ExamTicketStatus.Submitted;
        hoangTicket.IpAddress = "10.0.0.1";
        hoangTicket.IsLocked = true;

        // 2. Phá vỡ trạng thái: Đóng ca thi MF-05 In-Class và làm hết hạn khung giờ
        var inClassShift = await context.InClassExamShifts.FirstAsync(s => s.Id == DevelopmentDataSeeder.InClassShiftPrn231Id);
        inClassShift.Status = "CLOSED";
        inClassShift.StartTime = DateTime.UtcNow.AddHours(-8);
        inClassShift.EndTime = DateTime.UtcNow.AddHours(-1);
        inClassShift.IsLocked = true;

        await context.SaveChangesAsync();

        // 3. Thực thi Seeder lần nữa để kích hoạt Self-healing logic
        await DevelopmentDataSeeder.SeedAsync(context);

        // 4. Kiểm chứng MF-04 Lab Kiosk đã được phục hồi hoàn toàn
        var healedShift = await context.RealExamSessionShifts.FirstAsync(s => s.Id == DevelopmentDataSeeder.OfficialShiftReadyToTestPrn231Id);
        healedShift.Status.Should().Be("in_progress", "Ca thi MF-04 phải được tự phục hồi về in_progress");
        healedShift.IsExamPublished.Should().BeTrue("Đề thi MF-04 phải được tự xuất bản lại");
        healedShift.StartTime.Should().BeBefore(DateTime.UtcNow);
        healedShift.EndTime.Should().BeAfter(DateTime.UtcNow);

        var healedHoangTicket = await context.StudentExamTickets.FirstAsync(t => t.Id == DevelopmentDataSeeder.TicketReadyToTestHoangId);
        healedHoangTicket.Status.Should().Be(ExamTicketStatus.Scheduled, "Vé của Hoàng phải được phục hồi về Scheduled");
        healedHoangTicket.IpAddress.Should().Be("127.0.0.1", "Vé của Hoàng phải gắn lại IP 127.0.0.1");
        healedHoangTicket.IsLocked.Should().BeFalse();

        // 5. Kiểm chứng MF-05 In-Class đã được phục hồi hoàn toàn
        var healedInClass = await context.InClassExamShifts.FirstAsync(s => s.Id == DevelopmentDataSeeder.InClassShiftPrn231Id);
        healedInClass.Status.Should().Be("IN_PROGRESS", "Ca thi MF-05 phải được tự phục hồi về IN_PROGRESS");
        healedInClass.IsLocked.Should().BeFalse();
        healedInClass.StartTime.Should().BeBefore(DateTime.UtcNow);
        healedInClass.EndTime.Should().BeAfter(DateTime.UtcNow);

        var staticPassword = await context.InClassShiftPasswords.FirstOrDefaultAsync(p => p.ShiftId == healedInClass.Id && p.PlainCode == "123456");
        staticPassword.Should().NotBeNull();
        staticPassword!.ValidFrom.Should().BeBefore(DateTime.UtcNow);
        staticPassword.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact(DisplayName = "12. AZ-C01: SeedAsync backfill idempotent CloId khi câu hỏi PracticeQuestion đã tồn tại nhưng CloId == null")]
    public async Task SeedAsync_Should_Backfill_Null_CloId_On_Existing_PracticeQuestions()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var prnQuestion = await context.PracticeQuestions
            .FirstAsync(q => q.CourseId == DevelopmentDataSeeder.CoursePrn231Id);
        var entQuestion = await context.PracticeQuestions
            .FirstAsync(q => q.CourseId == DevelopmentDataSeeder.CourseEnt001Id);
        var swtQuestion = await context.PracticeQuestions
            .FirstAsync(q => q.CourseId == AcademicCourseBankImporter.CourseSwt301Id);

        prnQuestion.CloId = null;
        entQuestion.CloId = null;
        swtQuestion.CloId = null;
        await context.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(context);

        var reloadedPrn = await context.PracticeQuestions.AsNoTracking().FirstAsync(q => q.Id == prnQuestion.Id);
        var reloadedEnt = await context.PracticeQuestions.AsNoTracking().FirstAsync(q => q.Id == entQuestion.Id);
        var reloadedSwt = await context.PracticeQuestions.AsNoTracking().FirstAsync(q => q.Id == swtQuestion.Id);

        reloadedPrn.CloId.Should().NotBeNull();
        reloadedEnt.CloId.Should().NotBeNull();
        reloadedSwt.CloId.Should().NotBeNull();

        (await context.CourseClos.AnyAsync(c => c.Id == reloadedPrn.CloId && c.CourseId == DevelopmentDataSeeder.CoursePrn231Id && c.IsActive)).Should().BeTrue();
        (await context.CourseClos.AnyAsync(c => c.Id == reloadedEnt.CloId && c.CourseId == DevelopmentDataSeeder.CourseEnt001Id && c.IsActive)).Should().BeTrue();
        (await context.CourseClos.AnyAsync(c => c.Id == reloadedSwt.CloId && c.CourseId == AcademicCourseBankImporter.CourseSwt301Id && c.IsActive)).Should().BeTrue();
    }

    [Theory]
    [InlineData("SWR302", "00000000-0000-0000-0000-00000000000a", "00000000-0000-0000-0000-000000000361", "00000000-0000-0000-0000-000000000311", "00000000-0000-0000-0000-000000000408", "00000000-0000-0000-0000-000000000301", "00000000-0000-0000-0000-000000000305")]
    [InlineData("SWD392", "00000000-0000-0000-0000-000000000009", "00000000-0000-0000-0000-000000000261", "00000000-0000-0000-0000-000000000211", "00000000-0000-0000-0000-000000000404", "00000000-0000-0000-0000-000000000201", "00000000-0000-0000-0000-000000000205")]
    public async Task SeedAsync_Should_SoftDeactivate_And_Rename_Legacy_Swr_Swd_Data_Without_Deleting_Rows(
        string courseCode, string courseIdStr, string legacyCloIdStr, string legacyPqIdStr, string legacyEqIdStr, string legacyRubricIdStr, string legacyStructIdStr)
    {
        await using var context = CreateInMemoryContext();
        var courseId = Guid.Parse(courseIdStr);
        var legacyCloId = Guid.Parse(legacyCloIdStr);
        var legacyPqId = Guid.Parse(legacyPqIdStr);
        var legacyEqId = Guid.Parse(legacyEqIdStr);
        var legacyRubricId = Guid.Parse(legacyRubricIdStr);
        var legacyStructId = Guid.Parse(legacyStructIdStr);

        var sem = new Semester
        {
            Id = DevelopmentDataSeeder.SemesterFa26Id,
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        context.Semesters.Add(sem);
        context.Courses.Add(new Course { Id = courseId, Code = courseCode, Name = courseCode, SemesterId = sem.Id, IsActive = true });
        context.CourseClos.Add(new CourseClo { Id = legacyCloId, CourseId = courseId, CloCode = "CLO1", Description = "Legacy CLO1", BloomLevel = BloomLevel.Understand, OrderIndex = 1, IsActive = true });
        context.Rubrics.Add(new Rubric { Id = legacyRubricId, CourseId = courseId, Name = "Legacy Rubric", TotalMaxScore = 10.00m, IsActive = true });
        context.PracticeQuestions.Add(new PracticeQuestion { Id = legacyPqId, CourseId = courseId, CloId = legacyCloId, RubricId = legacyRubricId, Title = "Legacy PQ", Content = "Legacy", IsActive = true });
        context.ExamQuestions.Add(new ExamQuestion { Id = legacyEqId, CourseId = courseId, CloId = legacyCloId, RubricId = legacyRubricId, Title = "Legacy EQ", Content = "Legacy", IsActive = true });
        context.ExamStructures.Add(new ExamStructure { Id = legacyStructId, CourseId = courseId, CreatedBy = DevelopmentDataSeeder.AdminUserId, Name = "Legacy Struct", TotalQuestions = 3, DurationMinutes = 15, BloomDistribution = "{}", IsActive = true });
        await context.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(context);

        var renamedLegacyClo = await context.CourseClos.AsNoTracking().FirstAsync(c => c.Id == legacyCloId);
        renamedLegacyClo.IsActive.Should().BeFalse();
        renamedLegacyClo.CloCode.Should().StartWith("LEGACY-CLO1-");
        renamedLegacyClo.CloCode.Length.Should().BeLessThanOrEqualTo(50);

        (await context.PracticeQuestions.AsNoTracking().FirstAsync(q => q.Id == legacyPqId)).IsActive.Should().BeFalse();
        (await context.ExamQuestions.AsNoTracking().FirstAsync(q => q.Id == legacyEqId)).IsActive.Should().BeFalse();
        (await context.Rubrics.AsNoTracking().FirstAsync(r => r.Id == legacyRubricId)).IsActive.Should().BeFalse();
        (await context.ExamStructures.AsNoTracking().FirstAsync(s => s.Id == legacyStructId)).IsActive.Should().BeFalse();

        // New CLO1 is active without collision, and 0 new ExamQuestions are generated
        (await context.CourseClos.AnyAsync(c => c.CourseId == courseId && c.CloCode == "CLO1" && c.IsActive)).Should().BeTrue();
        (await context.ExamQuestions.AnyAsync(q => q.CourseId == courseId && q.IsActive)).Should().BeFalse();
    }

    [Fact(DisplayName = "M1-SEED-01: SeedAsync phân giải động PRN231-SET02 theo SetCode và chuẩn hóa AppealHoangId từ PENDING sang ASSIGNED_TO_LECTURER")]
    public async Task SeedAsync_WhenPrn231Set02ExistsWithNonCanonicalId_AndAppealPending_ReconcilesSafely()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        // Giả lập DB cũ có PRN231-SET02 mang GUID khác và bị tắt IsActive, đồng thời AppealHoangId mang status PENDING
        var existingSet02 = await context.ExamSets.FirstAsync(s => s.SetCode == "PRN231-SET02");
        var customSet02Id = Guid.NewGuid();

        var linkedTickets = await context.StudentExamTickets.Where(t => t.ExamSetId == existingSet02.Id).ToListAsync();
        var linkedSetQuestions = await context.ExamSetQuestions.Where(sq => sq.ExamSetId == existingSet02.Id).ToListAsync();
        context.StudentExamTickets.RemoveRange(linkedTickets);
        context.ExamSetQuestions.RemoveRange(linkedSetQuestions);
        context.ExamSets.Remove(existingSet02);
        await context.SaveChangesAsync();

        context.ExamSets.Add(new ExamSet
        {
            Id = customSet02Id,
            StructureId = existingSet02.StructureId,
            CourseId = existingSet02.CourseId,
            SetCode = "PRN231-SET02",
            Status = ExamSetStatus.Draft,
            IsActive = false,
            GeneratedAt = DateTime.UtcNow
        });

        var appeal = await context.AppealRequests.FirstAsync(a => a.Id == DevelopmentDataSeeder.AppealHoangId);
        appeal.Status = "PENDING";
        appeal.AssignedTo = DevelopmentDataSeeder.LecturerUserId;
        await context.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(context);

        var healedSet02 = await context.ExamSets.AsNoTracking().FirstAsync(s => s.SetCode == "PRN231-SET02");
        healedSet02.Id.Should().Be(customSet02Id);
        healedSet02.IsActive.Should().BeTrue();
        healedSet02.Status.Should().Be(ExamSetStatus.Official);

        var healedThanhTicket = await context.StudentExamTickets.AsNoTracking()
            .FirstAsync(t => t.Id == DevelopmentDataSeeder.TicketReadyToTestThanhId);
        healedThanhTicket.ExamSetId.Should().Be(customSet02Id);

        var healedAppeal = await context.AppealRequests.AsNoTracking()
            .FirstAsync(a => a.Id == DevelopmentDataSeeder.AppealHoangId);
        healedAppeal.Status.Should().Be(AppealStatus.AssignedToLecturer);
        healedAppeal.AssignedTo.Should().Be(DevelopmentDataSeeder.Lecturer2UserId);
    }

    [Fact(DisplayName = "M1-SEED-02: SeedAsync chuẩn hóa AppealHoangId từ PENDING với AssignedTo == Guid.Empty sang ASSIGNED_TO_LECTURER của Lecturer2UserId")]
    public async Task SeedAsync_WhenAppealHoangIsPendingWithEmptyAssignee_ReconcilesToAssignedToLecturer2()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var appeal = await context.AppealRequests.FirstAsync(a => a.Id == DevelopmentDataSeeder.AppealHoangId);
        appeal.Status = "PENDING";
        appeal.AssignedTo = Guid.Empty;
        await context.SaveChangesAsync();

        await DevelopmentDataSeeder.SeedAsync(context);

        var reconciled = await context.AppealRequests.AsNoTracking()
            .FirstAsync(a => a.Id == DevelopmentDataSeeder.AppealHoangId);
        reconciled.Status.Should().Be(AppealStatus.AssignedToLecturer);
        reconciled.AssignedTo.Should().Be(DevelopmentDataSeeder.Lecturer2UserId);
    }
}
