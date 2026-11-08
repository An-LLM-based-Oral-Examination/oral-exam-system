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

    [Fact(DisplayName = "2. SeedAsync khởi tạo đầy đủ 3 học phần PRN231, SWD392, SWR302 kèm Rubric 10.0đ, ExamStructure và 12 câu hỏi (4 Easy, 4 Medium, 4 Hard) mỗi môn")]
    public async Task SeedAsync_Should_Seed_All_Three_Courses_With_Balanced_Questions_Rubrics_And_Structures()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var expectedCourseCodes = new[] { "PRN231", "SWD392", "SWR302" };
        var courses = await context.Courses
            .Include(c => c.Rubrics)
                .ThenInclude(r => r.Criteria)
            .Include(c => c.ExamStructures)
            .Include(c => c.PracticeQuestions)
            .Where(c => expectedCourseCodes.Contains(c.Code))
            .ToListAsync();

        courses.Should().HaveCount(3);

        foreach (var course in courses)
        {
            course.IsActive.Should().BeTrue($"Học phần {course.Code} phải ở trạng thái active");
            course.MaxMockExamsPerDay.Should().Be(3, $"Học phần {course.Code} phải có hạn ngạch thi thử = 3 lượt/ngày theo chuẩn Admin");

            // Kiểm tra Rubric chuẩn tổng 10.00đ
            course.Rubrics.Should().NotBeEmpty($"Học phần {course.Code} phải có Rubric");
            var activeRubric = course.Rubrics.First(r => r.IsActive);
            activeRubric.TotalMaxScore.Should().Be(10.00m);
            activeRubric.Criteria.Should().HaveCount(3);
            activeRubric.Criteria.Sum(c => c.MaxScore).Should().Be(10.00m);
            activeRubric.Criteria.Sum(c => c.Weight).Should().Be(1.00m);

            // Kiểm tra ExamStructure active
            course.ExamStructures.Should().Contain(s => s.IsActive && s.TotalQuestions == 5);

            // Kiểm tra đủ 12 câu hỏi PracticeQuestions (4 Easy, 4 Medium, 4 Hard)
            var activeQuestions = course.PracticeQuestions.Where(q => q.IsActive).ToList();
            activeQuestions.Count.Should().BeGreaterThanOrEqualTo(12, $"Học phần {course.Code} phải có tối thiểu 12 câu hỏi luyện tập/thi thử");

            activeQuestions.Count(q => q.Difficulty == QuestionDifficulty.Easy).Should().BeGreaterThanOrEqualTo(4);
            activeQuestions.Count(q => q.Difficulty == QuestionDifficulty.Medium).Should().BeGreaterThanOrEqualTo(4);
            activeQuestions.Count(q => q.Difficulty == QuestionDifficulty.Hard).Should().BeGreaterThanOrEqualTo(4);

            foreach (var q in activeQuestions)
            {
                q.SampleAnswer.Should().NotBeNullOrWhiteSpace();
                q.SampleAnswer!.Length.Should().BeGreaterThanOrEqualTo(60, $"Câu hỏi '{q.Title}' phải có câu trả lời mẫu >= 60 ký tự");
                q.FollowUpPrompt.Should().NotBeNullOrWhiteSpace($"Câu hỏi '{q.Title}' phải có FollowUpPrompt");

                var keyPoints = JsonSerializer.Deserialize<List<string>>(q.KeyPoints);
                keyPoints.Should().NotBeNullOrEmpty($"Câu hỏi '{q.Title}' phải có KeyPoints JSON array hợp lệ");
            }
        }
    }

    [Fact(DisplayName = "3. Dữ liệu Seed cho phép chạy mượt mà Full-Session Progressive 10 câu (MF-01) và khởi tạo đề Thi thử 5 câu (MF-02) trên tất cả các môn")]
    public async Task Seeded_Data_Should_Support_FullSession_10_Questions_And_StartMockExam_For_All_Three_Courses()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var startPracticeHandler = new StartPracticeSessionCommandHandler(context);
        var startMockExamHandler = new StartMockExamCommandHandler(context);

        var courseIds = new[]
        {
            DevelopmentDataSeeder.CoursePrn231Id,
            DevelopmentDataSeeder.CourseSwd392Id,
            DevelopmentDataSeeder.CourseSwr302Id,
            DevelopmentDataSeeder.CoursePrf192Id,
            DevelopmentDataSeeder.CourseCsd201Id,
            DevelopmentDataSeeder.CourseDbi202Id,
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

        // 3. Kiểm tra rằng lịch sử được tạo thành công (>= 3 sessions) kèm Scorecard chi tiết
        foreach (var studentId in targetStudents)
        {
            // Kiểm tra lịch sử MF-01
            var practiceHistoryRes = await practiceHistoryHandler.Handle(
                new GetStudentPracticeHistoryQuery(studentId),
                CancellationToken.None);

            practiceHistoryRes.IsSuccess.Should().BeTrue();
            practiceHistoryRes.Value.Should().HaveCountGreaterThanOrEqualTo(3);

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
            mockHistoryRes.Value.Items.Should().HaveCountGreaterThanOrEqualTo(3);

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

        // 2. Kiểm tra ExamQuestions có >= 12 câu hỏi và đủ 4 trạng thái duyệt
        var examQuestions = await context.ExamQuestions.ToListAsync();
        examQuestions.Count.Should().BeGreaterThanOrEqualTo(12);
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

        // 4. Kiểm tra AppealRequest đã được gắn SubmissionId
        var appeal = await context.AppealRequests.FirstOrDefaultAsync(a => a.Id == DevelopmentDataSeeder.AppealHoangId);
        appeal.Should().NotBeNull();
        appeal!.SubmissionId.Should().NotBeNull();
    }

    [Fact(DisplayName = "6. SeedAdditionalThreeCoursesAndClosAsync khởi tạo chuẩn 1-1 FLM CLOs (9 PRF192, 8 CSD201, 7 DBI202) và đủ 186 câu hỏi (62 câu/môn = 36 Practice + 26 Exam) cô lập R8")]
    public async Task SeedAsync_Should_Seed_24_Flm_Clos_And_186_Isolated_Questions_For_Prf192_Csd201_Dbi202()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var expectedCourses = new (Guid CourseId, string Code, int ExpectedCloCount)[]
        {
            (DevelopmentDataSeeder.CoursePrf192Id, "PRF192", 9),
            (DevelopmentDataSeeder.CourseCsd201Id, "CSD201", 8),
            (DevelopmentDataSeeder.CourseDbi202Id, "DBI202", 7)
        };

        foreach (var (courseId, code, expectedCloCount) in expectedCourses)
        {
            var clos = await context.CourseClos
                .AsNoTracking()
                .Where(c => c.CourseId == courseId && c.IsActive)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();

            clos.Should().HaveCount(expectedCloCount, $"Học phần {code} phải có đúng {expectedCloCount} CLOs khớp 1-1 với FLM Syllabus");
            var cloIds = clos.Select(c => c.Id).ToHashSet();

            var practiceQs = await context.PracticeQuestions
                .AsNoTracking()
                .Where(q => q.CourseId == courseId && q.IsActive)
                .ToListAsync();

            practiceQs.Should().HaveCount(36, $"Học phần {code} phải có đúng 36 câu hỏi Kho Luyện tập (practice_questions)");
            practiceQs.Count(q => q.Difficulty == QuestionDifficulty.Easy).Should().Be(12);
            practiceQs.Count(q => q.Difficulty == QuestionDifficulty.Medium).Should().BeGreaterThanOrEqualTo(12);
            practiceQs.Count(q => q.Difficulty == QuestionDifficulty.Hard).Should().BeGreaterThanOrEqualTo(10);

            foreach (var pq in practiceQs)
            {
                pq.CloId.Should().NotBeNull($"Câu hỏi luyện tập '{pq.Title}' ({code}) phải được ánh xạ CloId 3NF");
                cloIds.Should().Contain(pq.CloId!.Value);
                pq.SampleAnswer.Should().NotBeNullOrWhiteSpace();
                pq.SampleAnswer!.Length.Should().BeGreaterThanOrEqualTo(50);
                pq.FollowUpPrompt.Should().NotBeNullOrWhiteSpace();
            }

            var examQs = await context.ExamQuestions
                .AsNoTracking()
                .Where(q => q.CourseId == courseId && q.IsActive)
                .ToListAsync();

            examQs.Should().HaveCount(26, $"Học phần {code} phải có đúng 26 câu hỏi Kho Thi thật (exam_questions)");
            examQs.Count(q => q.BloomLevel == BloomLevel.Remember).Should().Be(6);
            examQs.Count(q => q.BloomLevel == BloomLevel.Understand).Should().Be(8);
            examQs.Count(q => q.BloomLevel == BloomLevel.Apply).Should().Be(7);
            examQs.Count(q => q.BloomLevel == BloomLevel.Analyze).Should().Be(5);

            foreach (var eq in examQs)
            {
                eq.CloId.Should().NotBeNull($"Câu hỏi thi thật '{eq.Title}' ({code}) phải được ánh xạ CloId 3NF");
                cloIds.Should().Contain(eq.CloId!.Value);
                eq.SampleAnswer.Should().NotBeNullOrWhiteSpace();
                eq.SampleAnswer!.Length.Should().BeGreaterThanOrEqualTo(50);
            }

            // Kiểm tra cô lập R8: Không có bất kỳ câu hỏi nào trùng tiêu đề giữa 2 kho
            var practiceTitles = practiceQs.Select(q => q.Title.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var examTitles = examQs.Select(q => q.Title.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            practiceTitles.Intersect(examTitles, StringComparer.OrdinalIgnoreCase).Should().BeEmpty(
                $"Kho luyện tập và kho thi thật của môn {code} phải cô lập tuyệt đối theo quy tắc R8");

            // Kiểm tra độ phủ CLO: Mọi CLO của môn đều có câu hỏi đánh giá
            var coveredCloIds = practiceQs.Select(q => q.CloId!.Value)
                .Concat(examQs.Select(q => q.CloId!.Value))
                .ToHashSet();
            coveredCloIds.Should().BeEquivalentTo(cloIds, $"Toàn bộ {expectedCloCount} CLOs của môn {code} đều phải có câu hỏi phủ");
        }
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
    }

    [Fact(DisplayName = "8. SeedAsync khởi tạo Ca thi trong lớp (MF-05) cho PRN231 và SWD392 ở trạng thái IN_PROGRESS kèm mã OTP tĩnh 123456")]
    public async Task SeedAsync_Should_Seed_ReadyToTest_MF05_InClass_Shifts_With_Static_And_Dynamic_Otp()
    {
        await using var context = CreateInMemoryContext();
        await DevelopmentDataSeeder.SeedAsync(context);

        var inClassShifts = await context.InClassExamShifts
            .AsNoTracking()
            .Where(s => s.Id == DevelopmentDataSeeder.InClassShiftPrn231Id || s.Id == DevelopmentDataSeeder.InClassShiftSwd392Id)
            .ToListAsync();

        inClassShifts.Should().HaveCount(2, "Cả hai ca thi MF-05 cho PRN231 và SWD392 phải được khởi tạo");

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
        hoangResult.Value.StartTime.Should().BeBefore(DateTime.UtcNow);
        hoangResult.Value.EndTime.Should().BeAfter(DateTime.UtcNow);
        hoangResult.Value.Questions.Should().NotBeNullOrEmpty("Đề thi đã xuất bản phải kèm danh sách câu hỏi cho Kiosk");

        // 2. Sinh viên Thành truy cập từ IPv6 Loopback ::1 -> Thành công trả về vé SCHEDULED
        var thanhResult = await handler.Handle(
            new GetMyWorkstationTicketQuery(DevelopmentDataSeeder.StudentThanhId, "::1", DateTime.UtcNow),
            CancellationToken.None);

        thanhResult.IsSuccess.Should().BeTrue("Sinh viên Thành tại máy trạm ::1 phải lấy được vé thi");
        thanhResult.Value.Id.Should().Be(DevelopmentDataSeeder.TicketReadyToTestThanhId);
        thanhResult.Value.ShiftId.Should().Be(DevelopmentDataSeeder.OfficialShiftReadyToTestPrn231Id);
        thanhResult.Value.Status.Should().Be("SCHEDULED");
        thanhResult.Value.ShiftStatus.Should().Be("in_progress");

        // 3. Sinh viên Hoàng truy cập từ địa chỉ IP không hợp lệ (10.0.0.99) -> Bị từ chối lỗi WorkstationTicketNotFound
        var invalidIpResult = await handler.Handle(
            new GetMyWorkstationTicketQuery(DevelopmentDataSeeder.StudentHoangId, "10.0.0.99", DateTime.UtcNow),
            CancellationToken.None);

        invalidIpResult.IsSuccess.Should().BeFalse("Địa chỉ IP 10.0.0.99 không khớp máy trạm đã đăng ký phải bị từ chối");
        invalidIpResult.Error.Should().Be(OfficialExamErrors.WorkstationTicketNotFound);
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
}
