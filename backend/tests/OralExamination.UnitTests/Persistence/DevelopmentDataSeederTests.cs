using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetPracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
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

        // Lần chạy 2
        await DevelopmentDataSeeder.SeedAsync(context);

        (await context.Courses.CountAsync()).Should().Be(coursesCountAfterFirst);
        (await context.PracticeQuestions.CountAsync()).Should().Be(questionsCountAfterFirst);
        (await context.Rubrics.CountAsync()).Should().Be(rubricsCountAfterFirst);
        (await context.RubricCriteria.CountAsync()).Should().Be(criteriaCountAfterFirst);
        (await context.ExamStructures.CountAsync()).Should().Be(structuresCountAfterFirst);
        (await context.PracticeSessions.CountAsync()).Should().Be(practiceSessionsAfterFirst);
        (await context.MockExamSessions.CountAsync()).Should().Be(mockSessionsAfterFirst);
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

    [Fact(DisplayName = "3. Dữ liệu Seed cho phép chạy mượt mà Full-Session Progressive 10 câu (MF-01) và khởi tạo đề Thi thử 5 câu (MF-02) trên cả 3 môn")]
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
            DevelopmentDataSeeder.CourseSwr302Id
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

    [Fact(DisplayName = "4. SeedAsync tạo sẵn lịch sử PracticeSession và MockExamSession đã chấm điểm kèm Scorecard chi tiết cho mọi sinh viên")]
    public async Task SeedAsync_Should_Create_Graded_Practice_And_MockExam_History_For_Students()
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
}
