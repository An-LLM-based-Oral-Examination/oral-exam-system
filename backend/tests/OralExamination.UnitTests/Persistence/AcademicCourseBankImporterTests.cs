using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Persistence;

public sealed class AcademicCourseBankImporterTests
{
    private static OralExamDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"AcademicImporterTestDb_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "Importer: Nạp Production-safe gói gốc (446 GUID) + 27 câu bổ sung, giữ nguyên (Y1)/(Y2)/(Y3), ExamStructure MF-02, 0 user giả và idempotent")]
    public async Task ImportAsync_OnFreshDbWithBootstrapAdmin_ImportsAllBanksAndStartsMockExamsIdempotently()
    {
        await using var context = CreateInMemoryContext();
        var adminId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = adminId,
            Email = "bootstrap-admin@fpt.edu.vn",
            Username = "bootstrapadmin",
            FullName = "Bootstrap Admin",
            Role = UserRole.Admin,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var first = await AcademicCourseBankImporter.ImportAsync(
            context,
            NullLogger.Instance,
            allowDeferExamStructuresWhenNoUsers: false);

        first.CoursesProcessed.Should().Be(3);
        first.ExamStructuresDeferred.Should().Be(0);
        first.QuestionsInserted.Should().Be(131); // 104 + 27

        // Không tự tạo user mẫu (Production-safe)
        (await context.Users.CountAsync()).Should().Be(1);

        // 10 môn học active: PRN231 + SWT301 + SWR302 + SWD392 + ENT001..503
        var activeCourses = await context.Courses.AsNoTracking().Where(c => c.IsActive).ToListAsync();
        activeCourses.Should().HaveCount(10);
        (await context.CourseOfferings.AsNoTracking().CountAsync(o => o.IsEnabled)).Should().Be(10);

        // Kiểm tra SWT301 giữ nguyên (Y1)/(Y2)/(Y3) trong DB và +10 câu bổ sung
        var swtQuestions = await context.PracticeQuestions.AsNoTracking()
            .Where(q => q.CourseId == AcademicCourseBankImporter.CourseSwt301Id)
            .ToListAsync();
        swtQuestions.Should().HaveCount(50);
        swtQuestions.Count(q => q.Content.Contains("(Y1)", StringComparison.Ordinal) && q.Content.Contains("(Y2)", StringComparison.Ordinal)).Should().Be(40);
        swtQuestions.Should().Contain(q => q.Id == Guid.Parse("19902848-ae48-52b6-a03c-392a91f16d7f") && q.Content.Contains("(Y3)", StringComparison.Ordinal));

        var supplementaryQuestions = await context.PracticeQuestions.AsNoTracking()
            .Where(q => q.ReviewNotes != null && q.ReviewNotes.Contains(AcademicCourseBankImporter.SupplementarySourceMarker))
            .ToListAsync();
        supplementaryQuestions.Should().HaveCount(27);
        supplementaryQuestions.Should().OnlyContain(q =>
            q.ApprovedBy == null &&
            q.ApprovedAt == null &&
            q.SubmittedBy == null &&
            q.ApprovalStatus == ApprovalStatus.Approved &&
            q.Source == QuestionSource.Manual &&
            q.SampleAnswer != null &&
            q.SampleAnswer.Length >= 150);

        // Kiểm tra 26/26 CLOs đều đủ Easy/Medium/Hard
        foreach (var courseId in new[]
        {
            AcademicCourseBankImporter.CourseSwt301Id,
            AcademicCourseBankImporter.CourseSwr302Id,
            AcademicCourseBankImporter.CourseSwd392Id
        })
        {
            var clos = await context.CourseClos.AsNoTracking().Where(c => c.CourseId == courseId && c.IsActive).ToListAsync();
            var qs = await context.PracticeQuestions.AsNoTracking().Where(q => q.CourseId == courseId && q.IsActive && !q.IsDeleted).ToListAsync();
            foreach (var clo in clos)
            {
                var cloQs = qs.Where(q => q.CloId == clo.Id).ToList();
                cloQs.Should().Contain(q => q.Difficulty == QuestionDifficulty.Easy);
                cloQs.Should().Contain(q => q.Difficulty == QuestionDifficulty.Medium);
                cloQs.Should().Contain(q => q.Difficulty == QuestionDifficulty.Hard);
            }
        }

        // StartMockExamCommandHandler chạy được trên SWT301/SWR302/SWD392 khi SV được ghi danh
        var studentId = Guid.NewGuid();
        var sem = await context.Semesters.FirstAsync(s => s.Code == "FA26");
        context.Users.Add(new User { Id = studentId, Email = "sv@fpt.edu.vn", FullName = "SV", StudentCode = "SE190001", Role = UserRole.Student, IsActive = true });
        foreach (var cid in new[]
        {
            AcademicCourseBankImporter.CourseSwt301Id,
            AcademicCourseBankImporter.CourseSwr302Id,
            AcademicCourseBankImporter.CourseSwd392Id
        })
        {
            var cls = new Class { Id = Guid.NewGuid(), CourseId = cid, SemesterId = sem.Id, Code = $"TEST-{cid:N}"[..16], Name = "Test", JoinCode = $"J{cid:N}"[..10], IsActive = true };
            context.Classes.Add(cls);
            context.ClassEnrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = studentId, Status = "enrolled" });
        }
        await context.SaveChangesAsync();

        var mockHandler = new StartMockExamCommandHandler(context);
        foreach (var cid in new[]
        {
            AcademicCourseBankImporter.CourseSwt301Id,
            AcademicCourseBankImporter.CourseSwr302Id,
            AcademicCourseBankImporter.CourseSwd392Id
        })
        {
            var res = await mockHandler.Handle(new StartMockExamCommand(cid, studentId, false), CancellationToken.None);
            res.IsSuccess.Should().BeTrue($"StartMockExam must succeed for {cid}: {res.Error}");
            res.Value.Questions.Should().HaveCount(5);
        }

        // Idempotency run #2
        var second = await AcademicCourseBankImporter.ImportAsync(
            context,
            NullLogger.Instance,
            allowDeferExamStructuresWhenNoUsers: false);
        second.QuestionsInserted.Should().Be(0);
        second.RubricsInserted.Should().Be(0);
        (await context.PracticeQuestions.CountAsync()).Should().Be(131 + 12 + 72); // 131 + PRN231(12) + ENT*6(72)
    }

    [Fact(DisplayName = "Importer: Ánh xạ CourseId khác nhau theo Code, bảo vệ sửa đổi giảng viên, giữ sharedLegacyRubric và lọc legacy SWT301 ExamQuestions")]
    public async Task ImportAsync_RemapsCourseId_PreservesLecturerEdits_AndRespectsLegacyMarkers()
    {
        await using var context = CreateInMemoryContext();
        var adminId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();
        var semId = DevelopmentDataSeeder.SemesterFa26Id;
        var customSwtCourseId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = adminId, Email = "admin@fpt.edu.vn", FullName = "Admin", Role = UserRole.Admin, IsActive = true },
            new User { Id = lecturerId, Email = "lec@fe.edu.vn", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true });
        context.Semesters.Add(new Semester { Id = semId, Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true });

        // SWT301 tồn tại với Course.Id khác JSON
        context.Courses.Add(new Course { Id = customSwtCourseId, Code = "SWT301", Name = "Old Name", SemesterId = semId, IsActive = true });

        // SWT301 legacy ExamQuestion 1 (unapproved importer marker -> deactivate) vs ExamQuestion 2 (approved by lecturer/head -> keep active)
        var swtDummyRubricId = Guid.NewGuid();
        context.Rubrics.Add(new Rubric { Id = swtDummyRubricId, CourseId = customSwtCourseId, Name = "R", TotalMaxScore = 10.00m, IsActive = true });
        var legacyImportedEqId = Guid.Parse("03554a0d-7d8f-5122-9a8d-e91cdde29823");
        var lecturerApprovedEqId = Guid.Parse("8df2be71-2a77-54d9-8038-d1886515bc57");
        context.ExamQuestions.AddRange(
            new ExamQuestion
            {
                Id = legacyImportedEqId,
                CourseId = customSwtCourseId,
                RubricId = swtDummyRubricId,
                Title = "Imported EQ",
                Content = "Content",
                ApprovedBy = null,
                SubmittedBy = null,
                ReviewNotes = "imported without impersonating an OES account",
                IsActive = true
            },
            new ExamQuestion
            {
                Id = lecturerApprovedEqId,
                CourseId = customSwtCourseId,
                RubricId = swtDummyRubricId,
                Title = "Lecturer EQ",
                Content = "Content",
                ApprovedBy = adminId,
                SubmittedBy = lecturerId,
                ReviewNotes = "Approved by head",
                IsActive = true
            });

        // SWR302 sharedLegacyRubric still used by a custom active PracticeQuestion -> rubric stays IsActive = true
        var swrCourseId = AcademicCourseBankImporter.CourseSwr302Id;
        var swrLegacyRubricId = Guid.Parse("00000000-0000-0000-0000-000000000301");
        context.Courses.Add(new Course { Id = swrCourseId, Code = "SWR302", Name = "SWR302", SemesterId = semId, IsActive = true });
        context.Rubrics.Add(new Rubric { Id = swrLegacyRubricId, CourseId = swrCourseId, Name = "Shared Legacy Rubric", TotalMaxScore = 10.00m, IsActive = true });
        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = swrCourseId,
            RubricId = swrLegacyRubricId,
            Title = "Custom Active Question Using Legacy Rubric",
            Content = "Custom content",
            IsActive = true,
            IsDeleted = false
        });

        // Pre-seed one SWT301 question with lecturer edit
        var editedQuestionId = Guid.Parse("20d04e99-8ab6-5778-a7f0-66f138281946");
        var editedRubricId = Guid.Parse("98cb6736-53e8-565e-8ab7-8fcddafcff98");
        context.Rubrics.Add(new Rubric { Id = editedRubricId, CourseId = customSwtCourseId, Name = "Custom Edited Rubric", Description = "Edited by lecturer", TotalMaxScore = 10.00m, IsActive = true });
        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = editedQuestionId,
            CourseId = customSwtCourseId,
            RubricId = editedRubricId,
            CloId = null,
            Title = "Lecturer Edited Title",
            Content = "Lecturer Edited Content",
            SampleAnswer = "Lecturer Edited SampleAnswer >= 50 chars for verification.",
            IsActive = true
        });
        await context.SaveChangesAsync();

        await AcademicCourseBankImporter.ImportAsync(context, NullLogger.Instance);

        // SWT301 children remapped to customSwtCourseId
        (await context.Courses.CountAsync(c => c.Code == "SWT301")).Should().Be(1);
        (await context.PracticeQuestions.CountAsync(q => q.CourseId == customSwtCourseId)).Should().Be(50);

        // Lecturer edit preserved and CloId backfilled
        var reloadedEdited = await context.PracticeQuestions.AsNoTracking().FirstAsync(q => q.Id == editedQuestionId);
        reloadedEdited.Title.Should().Be("Lecturer Edited Title");
        reloadedEdited.CloId.Should().NotBeNull();
        (await context.Rubrics.AsNoTracking().FirstAsync(r => r.Id == editedRubricId)).Description.Should().Be("Edited by lecturer");

        // SWT301 marker rule respected
        (await context.ExamQuestions.AsNoTracking().FirstAsync(q => q.Id == legacyImportedEqId)).IsActive.Should().BeFalse();
        (await context.ExamQuestions.AsNoTracking().FirstAsync(q => q.Id == lecturerApprovedEqId)).IsActive.Should().BeTrue();

        // Shared legacy rubric stays active because custom active question references it
        (await context.Rubrics.AsNoTracking().FirstAsync(r => r.Id == swrLegacyRubricId)).IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "Importer: Khi 0 user tồn tại, auto-startup defer ExamStructure (allowDefer=true), còn CLI chế độ chặt fail-closed (allowDefer=false)")]
    public async Task ImportAsync_WhenZeroUsers_DefersInStartupModeAndFailsClosedInCliMode()
    {
        await using var context = CreateInMemoryContext();

        var startupResult = await AcademicCourseBankImporter.ImportAsync(
            context,
            NullLogger.Instance,
            allowDeferExamStructuresWhenNoUsers: true);

        startupResult.ExamStructuresDeferred.Should().Be(10); // 3 SE + PRN231 + 6 ENT
        (await context.ExamStructures.CountAsync()).Should().Be(0);
        (await context.PracticeQuestions.CountAsync()).Should().Be(131 + 12 + 72);

        var actCli = () => AcademicCourseBankImporter.ImportAsync(
            context,
            NullLogger.Instance,
            allowDeferExamStructuresWhenNoUsers: false);

        await actCli.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[FAIL CLOSED]*");
    }

    [Fact(DisplayName = "Importer: ActiveAssessmentGuard chặn vô hiệu hóa legacyData khi đang có phiên thi thử/luyện tập in_progress")]
    public async Task ImportAsync_ActiveAssessmentGuard_BlocksWhenLegacyDeactivationNeededDuringActiveSession()
    {
        await using var context = CreateInMemoryContext();
        var semId = DevelopmentDataSeeder.SemesterFa26Id;
        var swrId = AcademicCourseBankImporter.CourseSwr302Id;
        var studentId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = adminId, Email = "admin@fpt.edu.vn", FullName = "Admin", Role = UserRole.Admin, IsActive = true },
            new User { Id = studentId, Email = "sv@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true });
        context.Semesters.Add(new Semester { Id = semId, Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true });
        context.Courses.Add(new Course { Id = swrId, Code = "SWR302", Name = "SWR302", SemesterId = semId, IsActive = true });
        context.CourseClos.Add(new CourseClo
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000361"),
            CourseId = swrId,
            CloCode = "CLO1",
            Description = "Legacy",
            BloomLevel = BloomLevel.Understand,
            IsActive = true
        });
        context.PracticeSessions.Add(new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = swrId,
            StudentId = studentId,
            PracticeMode = PracticeMode.PerQuestion,
            Status = PracticeSessionStatus.InProgress
        });
        await context.SaveChangesAsync();

        var act = () => AcademicCourseBankImporter.ImportAsync(context, NullLogger.Instance);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ActiveAssessmentGuard*SWR302*");
    }
}
