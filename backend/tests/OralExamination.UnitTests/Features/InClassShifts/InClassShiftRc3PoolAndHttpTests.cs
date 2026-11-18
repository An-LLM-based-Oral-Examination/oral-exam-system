using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.ResumeInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftById;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftPoolStats;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftsByClass;
using OralExamination.Application.Features.InClassShifts.Queries.GetMyInClassShifts;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.InClassShifts;

public sealed class InClassShiftRc3PoolAndHttpTests
{
    private const string JwtSigningKey = "SuperSecretKeyForInClassShiftRc3PipelineMustBeAtLeast32BytesLong!";
    private const string JwtIssuer = "OralExamInClassRc3Issuer";
    private const string JwtAudience = "OralExamInClassRc3Audience";

    private static OralExamDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static PracticeQuestion BuildApprovedQuestion(
        Guid courseId,
        Guid? cloId,
        string difficulty,
        string title,
        string approvalStatus = ApprovalStatus.Approved,
        bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            CloId = cloId,
            RubricId = Guid.NewGuid(),
            Title = title,
            Content = $"Nội dung câu hỏi: {title}",
            SampleAnswer = "Đáp án mẫu chuẩn học thuật có độ dài lớn hơn năm mươi ký tự để đảm bảo chuẩn dữ liệu.",
            KeyPoints = "[\"Clean Architecture\", \"CQRS\"]",
            Difficulty = difficulty,
            BloomLevel = BloomLevel.Apply,
            ApprovalStatus = approvalStatus,
            IsActive = isActive
        };

    [Fact(DisplayName = "MF-05 RC-3: PoolStats chỉ đếm câu luyện tập APPROVED & active đúng môn, nhóm chuẩn theo CLO active")]
    public async Task GetPoolStats_CountsOnlyApprovedActivePracticeQuestions_AndOrdersActiveClos()
    {
        using var db = CreateInMemoryContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv@fpt.edu.vn", FullName = "GV Chủ Lớp", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Building Web APIs", IsActive = true };
        var classEntity = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "PRN231 - SE1801", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };

        var clo2 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "Thiết kế RESTful API", OrderIndex = 2, IsActive = true };
        var clo1 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Nắm vững Clean Architecture", OrderIndex = 1, IsActive = true };
        var inactiveClo = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO_OLD", Description = "CLO ngừng dùng", OrderIndex = 3, IsActive = false };

        db.Users.Add(lecturer);
        db.Courses.Add(course);
        db.Classes.Add(classEntity);
        db.CourseClos.AddRange(clo2, clo1, inactiveClo);

        db.PracticeQuestions.AddRange(
            BuildApprovedQuestion(course.Id, clo1.Id, "easy", "CLO1 Easy 1"),
            BuildApprovedQuestion(course.Id, clo1.Id, "medium", "CLO1 Medium 1"),
            BuildApprovedQuestion(course.Id, clo2.Id, "hard", "CLO2 Hard 1"),
            BuildApprovedQuestion(course.Id, null, "easy", "Unlinked Approved Easy"),
            BuildApprovedQuestion(course.Id, clo1.Id, "easy", "CLO1 Draft", approvalStatus: ApprovalStatus.Draft),
            BuildApprovedQuestion(course.Id, clo1.Id, "hard", "CLO1 Inactive", isActive: false)
        );

        // Câu thi thật (ExamQuestions) tuyệt đối không được tính vào kho MF-05 (Cô lập R8)
        db.ExamQuestions.Add(new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloId = clo1.Id,
            RubricId = Guid.NewGuid(),
            Title = "Official Exam Question",
            Content = "Nội dung câu hỏi thi thật Kiosk",
            SampleAnswer = "Đáp án mẫu thi thật dài hơn năm mươi ký tự để kiểm chứng cô lập R8.",
            Difficulty = "hard",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });

        await db.SaveChangesAsync();

        var handler = new GetInClassShiftPoolStatsQueryHandler(db);
        var result = await handler.Handle(new GetInClassShiftPoolStatsQuery(lecturer.Id, classEntity.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CourseCode.Should().Be("PRN231");
        result.Value.CourseBankCounts.Should().BeEquivalentTo(new InClassDifficultyCountsDto(Easy: 2, Medium: 1, Hard: 1, Total: 4));

        result.Value.Clos.Select(c => c.CloCode).Should().Equal("CLO1", "CLO2");
        result.Value.Clos[0].Counts.Should().BeEquivalentTo(new InClassDifficultyCountsDto(Easy: 1, Medium: 1, Hard: 0, Total: 2));
        result.Value.Clos[1].Counts.Should().BeEquivalentTo(new InClassDifficultyCountsDto(Easy: 0, Medium: 0, Hard: 1, Total: 1));
    }

    [Fact(DisplayName = "MF-05 RC-3: Tạo ca mới từ chối SELECTED_POOL, selectedQuestionIds, số CLO > số câu/SV và báo cụ thể CLO/mức thiếu")]
    public async Task CreateInClassShift_EnforcesRc3ValidationAndPoolInvariants()
    {
        using var db = CreateInMemoryContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-rc3@fpt.edu.vn", FullName = "GV RC3", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Software Architecture", IsActive = true };
        var otherCourse = new Course { Id = Guid.NewGuid(), Code = "SWR302", Name = "Software Requirements", IsActive = true };
        var classEntity = new Class { Id = Guid.NewGuid(), Code = "SE1802", Name = "SWD392 - SE1802", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };

        var clo1 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Architectural Styles", OrderIndex = 1, IsActive = true };
        var clo2 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "Design Patterns", OrderIndex = 2, IsActive = true };
        var clo3 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO3", Description = "Quality Attributes", OrderIndex = 3, IsActive = true };
        var foreignClo = new CourseClo { Id = Guid.NewGuid(), CourseId = otherCourse.Id, CloCode = "CLO_FOREIGN", Description = "Môn khác", OrderIndex = 1, IsActive = true };

        db.Users.Add(lecturer);
        db.Courses.AddRange(course, otherCourse);
        db.Classes.Add(classEntity);
        db.CourseClos.AddRange(clo1, clo2, clo3, foreignClo);

        // CLO1 chỉ có Easy (2 câu), CLO2 chỉ có Medium (2 câu), CLO3 có Easy (1 câu); cả môn KHÔNG có câu Hard nào
        db.PracticeQuestions.AddRange(
            BuildApprovedQuestion(course.Id, clo1.Id, "easy", "CLO1 Easy A"),
            BuildApprovedQuestion(course.Id, clo1.Id, "easy", "CLO1 Easy B"),
            BuildApprovedQuestion(course.Id, clo2.Id, "medium", "CLO2 Medium A"),
            BuildApprovedQuestion(course.Id, clo2.Id, "medium", "CLO2 Medium B"),
            BuildApprovedQuestion(course.Id, clo3.Id, "easy", "CLO3 Easy A")
        );
        await db.SaveChangesAsync();

        var validator = new CreateInClassShiftCommandValidator();
        var handler = new CreateInClassShiftCommandHandler(db, validator);
        var start = DateTime.UtcNow.AddMinutes(5);
        var end = start.AddMinutes(45);

        // 1. Từ chối SELECTED_POOL khi tạo ca mới
        var legacyModeRes = await handler.Handle(new CreateInClassShiftCommand(
            lecturer.Id, classEntity.Id, "Ca legacy", start, end,
            QuestionPoolMode: "SELECTED_POOL"), CancellationToken.None);
        legacyModeRes.IsSuccess.Should().BeFalse();
        legacyModeRes.Error.Should().Be(InClassShiftErrors.InvalidPoolMode);

        // 2. Từ chối gửi selectedQuestionIds khi tạo ca mới
        var manualIdsRes = await handler.Handle(new CreateInClassShiftCommand(
            lecturer.Id, classEntity.Id, "Ca gửi ID thủ công", start, end,
            QuestionPoolMode: "ALL_COURSE_BANK",
            SelectedQuestionIds: new[] { Guid.NewGuid() }), CancellationToken.None);
        manualIdsRes.IsSuccess.Should().BeFalse();
        manualIdsRes.Error.Should().Be(InClassShiftErrors.LegacySelectedQuestionIdsForbidden);

        // 3. Từ chối khi số CLO > số câu mỗi sinh viên (3 CLO > 2 câu/SV)
        var tooManyClosRes = await handler.Handle(new CreateInClassShiftCommand(
            lecturer.Id, classEntity.Id, "Ca 3 CLO nhưng 2 câu", start, end,
            QuestionPoolMode: "SELECTED_CLOS",
            TotalQuestionsPerStudent: 2,
            SelectedDifficulties: new[] { "easy", "medium" },
            SelectedCloIds: new[] { clo1.Id, clo2.Id, clo3.Id }), CancellationToken.None);
        tooManyClosRes.IsSuccess.Should().BeFalse();
        tooManyClosRes.Error.Should().Contain("Số CLO đã chọn (3) lớn hơn số câu mỗi sinh viên (2)");

        // 4. Từ chối khi CLO2 không có câu ở mức Dễ (không tự bù bằng Medium của CLO2 hay Easy của CLO1)
        var missingCloForDiffRes = await handler.Handle(new CreateInClassShiftCommand(
            lecturer.Id, classEntity.Id, "Ca thiếu câu CLO2 mức Dễ", start, end,
            QuestionPoolMode: "SELECTED_CLOS",
            TotalQuestionsPerStudent: 2,
            SelectedDifficulties: new[] { "easy" },
            SelectedCloIds: new[] { clo1.Id, clo2.Id }), CancellationToken.None);
        missingCloForDiffRes.IsSuccess.Should().BeFalse();
        missingCloForDiffRes.Error.Should().Contain("CLO2").And.Contain("Dễ");

        // 5. Từ chối khi mức Khó = 0 câu trong ngân hàng môn
        var missingHardBankRes = await handler.Handle(new CreateInClassShiftCommand(
            lecturer.Id, classEntity.Id, "Ca thiếu câu Khó", start, end,
            QuestionPoolMode: "ALL_COURSE_BANK",
            TotalQuestionsPerStudent: 2,
            SelectedDifficulties: new[] { "easy", "hard" }), CancellationToken.None);
        missingHardBankRes.IsSuccess.Should().BeFalse();
        missingHardBankRes.Error.Should().Contain("Khó");

        // 6. Từ chối CLO thuộc môn khác
        var foreignCloRes = await handler.Handle(new CreateInClassShiftCommand(
            lecturer.Id, classEntity.Id, "Ca CLO ngoại lai", start, end,
            QuestionPoolMode: "SELECTED_CLOS",
            TotalQuestionsPerStudent: 2,
            SelectedDifficulties: new[] { "easy" },
            SelectedCloIds: new[] { clo1.Id, foreignClo.Id }), CancellationToken.None);
        foreignCloRes.IsSuccess.Should().BeFalse();
        foreignCloRes.Error.Should().Be(InClassShiftErrors.InvalidCourseClo);
    }

    [Fact(DisplayName = "MF-05 RC-3: Phân bổ câu hỏi cân bằng giữa CLO & Độ khó, chống trùng (Anti-Collision) và cố định bộ câu khi Resume")]
    public async Task EnterAndResume_SelectedClos_BalancesCloAndDifficulty_AntiCollision_AndKeepsFixedQuestionsOnResume()
    {
        using var db = CreateInMemoryContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-alloc@fpt.edu.vn", FullName = "GV Alloc", Role = UserRole.Lecturer, IsActive = true };
        var student1 = new User { Id = Guid.NewGuid(), Email = "sv1@fpt.edu.vn", FullName = "Sinh Viên 1", StudentCode = "SE181001", Role = UserRole.Student, IsActive = true };
        var student2 = new User { Id = Guid.NewGuid(), Email = "sv2@fpt.edu.vn", FullName = "Sinh Viên 2", StudentCode = "SE181002", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", IsActive = true };
        var classEntity = new Class { Id = Guid.NewGuid(), Code = "SE1805", Name = "PRN231 - SE1805", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };

        var clo1 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "HTTP & REST", OrderIndex = 1, IsActive = true };
        var clo2 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "EF Core 3NF", OrderIndex = 2, IsActive = true };
        var excludedClo3 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO3", Description = "SignalR", OrderIndex = 3, IsActive = true };

        db.Users.AddRange(lecturer, student1, student2);
        db.Courses.Add(course);
        db.Classes.Add(classEntity);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = classEntity.Id, StudentId = student1.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = classEntity.Id, StudentId = student2.Id, Status = "enrolled" }
        );
        db.CourseClos.AddRange(clo1, clo2, excludedClo3);

        var clo1Easy1 = BuildApprovedQuestion(course.Id, clo1.Id, "easy", "CLO1 Easy 1");
        var clo1Easy2 = BuildApprovedQuestion(course.Id, clo1.Id, "easy", "CLO1 Easy 2");
        var clo1Hard1 = BuildApprovedQuestion(course.Id, clo1.Id, "hard", "CLO1 Hard 1");
        var clo1Hard2 = BuildApprovedQuestion(course.Id, clo1.Id, "hard", "CLO1 Hard 2");

        var clo2Easy1 = BuildApprovedQuestion(course.Id, clo2.Id, "easy", "CLO2 Easy 1");
        var clo2Easy2 = BuildApprovedQuestion(course.Id, clo2.Id, "easy", "CLO2 Easy 2");
        var clo2Hard1 = BuildApprovedQuestion(course.Id, clo2.Id, "hard", "CLO2 Hard 1");
        var clo2Hard2 = BuildApprovedQuestion(course.Id, clo2.Id, "hard", "CLO2 Hard 2");

        // Các câu ngoài CLO chọn hoặc ngoài mức chọn tuyệt đối không được lọt vào đề
        var excludedMedium = BuildApprovedQuestion(course.Id, clo1.Id, "medium", "CLO1 Medium Excluded");
        var excludedCloQuestion = BuildApprovedQuestion(course.Id, excludedClo3.Id, "easy", "CLO3 Easy Excluded");

        db.PracticeQuestions.AddRange(
            clo1Easy1, clo1Easy2, clo1Hard1, clo1Hard2,
            clo2Easy1, clo2Easy2, clo2Hard1, clo2Hard2,
            excludedMedium, excludedCloQuestion);
        await db.SaveChangesAsync();

        var createHandler = new CreateInClassShiftCommandHandler(db, new CreateInClassShiftCommandValidator());
        var start = DateTime.UtcNow.AddMinutes(-2);
        var end = DateTime.UtcNow.AddMinutes(45);
        var createRes = await createHandler.Handle(new CreateInClassShiftCommand(
            lecturer.Id,
            classEntity.Id,
            "Kiểm tra Tiến độ RC-3",
            start,
            end,
            Description: "Kiểm tra cân bằng CLO & Độ khó",
            QuestionPoolMode: "SELECTED_CLOS",
            TotalQuestionsPerStudent: 4,
            ExamDurationMinutes: 20,
            SelectedDifficulties: new[] { "easy", "hard" },
            SelectedCloIds: new[] { clo1.Id, clo2.Id }), CancellationToken.None);

        createRes.IsSuccess.Should().BeTrue();
        createRes.Value.QuestionPoolMode.Should().Be("SELECTED_CLOS");
        createRes.Value.SelectedDifficulties.Should().Equal("easy", "hard");
        createRes.Value.SelectedCloCodes.Should().Equal("CLO1", "CLO2");

        var shift = await db.InClassExamShifts.FirstAsync(s => s.Id == createRes.Value.Id);
        shift.Status = "IN_PROGRESS";
        await db.SaveChangesAsync();

        var otp = TotpPasswordHelper.GenerateCode(shift.TotpSecretKey);
        var queueMock = new Mock<IGradingQueueChannel>();
        var enterHandler = new EnterInClassShiftCommandHandler(db, new EnterInClassShiftCommandValidator(), queueMock.Object);

        // Sinh viên 1 vào ca
        var enter1 = await enterHandler.Handle(new EnterInClassShiftCommand(student1.Id, shift.Id, otp), CancellationToken.None);
        enter1.IsSuccess.Should().BeTrue();
        var s1Ids = enter1.Value.AssignedQuestionIds!;
        s1Ids.Should().HaveCount(4);
        s1Ids.Should().NotContain(excludedMedium.Id);
        s1Ids.Should().NotContain(excludedCloQuestion.Id);

        var s1Questions = await db.PracticeQuestions.Where(q => s1Ids.Contains(q.Id)).ToListAsync();
        s1Questions.Count(q => q.CloId == clo1.Id).Should().Be(2, "phải cân bằng 2 câu CLO1");
        s1Questions.Count(q => q.CloId == clo2.Id).Should().Be(2, "phải cân bằng 2 câu CLO2");
        s1Questions.Count(q => q.Difficulty == "easy").Should().Be(2, "phải cân bằng 2 câu Dễ");
        s1Questions.Count(q => q.Difficulty == "hard").Should().Be(2, "phải cân bằng 2 câu Khó");

        // Sinh viên 2 vào ca -> Anti-Collision bắt buộc cấp 4 câu còn lại chưa xuất hiện (usageCount = 0)
        var enter2 = await enterHandler.Handle(new EnterInClassShiftCommand(student2.Id, shift.Id, otp), CancellationToken.None);
        enter2.IsSuccess.Should().BeTrue();
        var s2Ids = enter2.Value.AssignedQuestionIds!;
        s2Ids.Should().HaveCount(4);
        s2Ids.Intersect(s1Ids).Should().BeEmpty("Anti-Collision phải ưu tiên 100% các câu chưa giao cho SV1 khi kho có đủ 8 câu");

        // Thêm câu mới vào kho và gọi Resume cho SV1 -> bộ câu cố định không đổi
        db.PracticeQuestions.Add(BuildApprovedQuestion(course.Id, clo1.Id, "easy", "Brand New CLO1 Easy"));
        await db.SaveChangesAsync();

        var resumeHandler = new ResumeInClassShiftCommandHandler(db, queueMock.Object);
        var resume1 = await resumeHandler.Handle(new ResumeInClassShiftCommand(student1.Id, shift.Id), CancellationToken.None);
        resume1.IsSuccess.Should().BeTrue();
        resume1.Value.AssignedQuestionIds.Should().Equal(s1Ids, "Refresh/Resume bắt buộc giữ nguyên thứ tự và danh sách câu đã cấp cho SV1");

        // Kiểm tra các query đọc ca trả đầy đủ cấu hình đã lưu
        var byClassRes = await new GetInClassShiftsByClassQueryHandler(db)
            .Handle(new GetInClassShiftsByClassQuery(lecturer.Id, classEntity.Id), CancellationToken.None);
        byClassRes.IsSuccess.Should().BeTrue();
        byClassRes.Value[0].SelectedCloCodes.Should().Equal("CLO1", "CLO2");
        byClassRes.Value[0].SelectedDifficulties.Should().Equal("easy", "hard");

        var myShiftsRes = await new GetMyInClassShiftsQueryHandler(db)
            .Handle(new GetMyInClassShiftsQuery(student1.Id), CancellationToken.None);
        myShiftsRes.IsSuccess.Should().BeTrue();
        myShiftsRes.Value[0].SelectedCloCodes.Should().Equal("CLO1", "CLO2");
        myShiftsRes.Value[0].MyRemainingSeconds.Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "MF-05 RC-3 HTTP Pipeline: Kiểm chứng RBAC 401/403 và RFC 7807 422 cho pool-stats & create-shift")]
    public async Task HttpPipeline_EnforcesRbacAndReturns422ProblemDetails()
    {
        var dbName = Guid.NewGuid().ToString();
        var ownerLecturer = new User { Id = Guid.NewGuid(), Email = "owner@fpt.edu.vn", FullName = "Owner GV", Role = UserRole.Lecturer, IsActive = true, AuthVersion = 1 };
        var colleagueLecturer = new User { Id = Guid.NewGuid(), Email = "colleague@fpt.edu.vn", FullName = "Colleague GV", Role = UserRole.Lecturer, IsActive = true, AuthVersion = 1 };
        var student = new User { Id = Guid.NewGuid(), Email = "student@fpt.edu.vn", FullName = "Student", StudentCode = "SE182001", Role = UserRole.Student, IsActive = true, AuthVersion = 1 };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtSigningKey"] = JwtSigningKey,
                ["Authentication:JwtIssuer"] = JwtIssuer,
                ["Authentication:JwtAudience"] = JwtAudience,
                ["Authentication:AccessTokenMinutes"] = "60"
            })
            .Build();

        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration((_, c) => c.AddConfiguration(config))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
                services.AddApplication();
                services.AddOralExamAuthentication(ctx.Configuration);
                services.AddSingleton(new Mock<IGradingQueueChannel>().Object);
                services.AddControllers().AddApplicationPart(typeof(InClassShiftsController).Assembly);
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();

        Guid classId;
        Guid clo1Id;
        Guid clo2Id;

        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", IsActive = true };
            var cls = new Class { Id = Guid.NewGuid(), Code = "SE1810", Name = "PRN231 - SE1810", CourseId = course.Id, LecturerId = ownerLecturer.Id, IsActive = true };
            var clo1 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "REST", OrderIndex = 1, IsActive = true };
            var clo2 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "CQRS", OrderIndex = 2, IsActive = true };

            classId = cls.Id;
            clo1Id = clo1.Id;
            clo2Id = clo2.Id;

            db.Users.AddRange(ownerLecturer, colleagueLecturer, student);
            db.Courses.Add(course);
            db.Classes.Add(cls);
            db.CourseClos.AddRange(clo1, clo2);
            db.PracticeQuestions.Add(BuildApprovedQuestion(course.Id, clo1.Id, "easy", "CLO1 Easy Only"));
            await db.SaveChangesAsync();
        }

        // 1. Anonymous -> 401
        var anonRes = await client.GetAsync($"/api/v1/lecturer/in-class-shifts/pool-stats?classId={classId}");
        anonRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Student gọi lecturer pool-stats -> 403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(student));
        var studentPoolRes = await client.GetAsync($"/api/v1/lecturer/in-class-shifts/pool-stats?classId={classId}");
        studentPoolRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Giảng viên lớp khác gọi pool-stats của lớp đồng nghiệp -> 403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(colleagueLecturer));
        var colleaguePoolRes = await client.GetAsync($"/api/v1/lecturer/in-class-shifts/pool-stats?classId={classId}");
        colleaguePoolRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Giảng viên chủ lớp gọi pool-stats -> 200 OK
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(ownerLecturer));
        var ownerPoolRes = await client.GetAsync($"/api/v1/lecturer/in-class-shifts/pool-stats?classId={classId}");
        ownerPoolRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var statsDto = await ownerPoolRes.Content.ReadFromJsonAsync<InClassShiftPoolStatsResponse>();
        statsDto.Should().NotBeNull();
        statsDto!.CourseBankCounts.Total.Should().Be(1);
        statsDto.Clos.Should().HaveCount(2);

        // 5. Giảng viên chủ lớp tạo ca với 2 CLO nhưng chỉ 1 câu/SV -> 422 ProblemDetails
        var badCreatePayload = new
        {
            classId,
            shiftName = "Ca lỗi số CLO > số câu",
            startTime = DateTime.UtcNow.AddMinutes(5),
            endTime = DateTime.UtcNow.AddMinutes(50),
            questionPoolMode = "SELECTED_CLOS",
            totalQuestionsPerStudent = 1,
            selectedDifficulties = new[] { "easy" },
            selectedCloIds = new[] { clo1Id, clo2Id },
            examDurationMinutes = 15
        };
        var unprocessableRes = await client.PostAsJsonAsync("/api/v1/lecturer/in-class-shifts", badCreatePayload);
        unprocessableRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await unprocessableRes.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Số CLO đã chọn (2) lớn hơn số câu mỗi sinh viên (1)");
    }

    [Fact(DisplayName = "MF-05 RC-3 PostgreSQL Integration: Đồng bộ cột jsonb selected_difficulties/selected_clo_ids & phân bổ câu đồng thời trên PostgreSQL")]
    [Trait("Category", "Integration")]
    public async Task PostgreSql_ReconcilesRc3ColumnsAndAllocatesSelectedClosConcurrently()
    {
        var connStr = InClassShiftConcurrencyIntegrationTests.GetPostgresConnectionString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(connStr)
            .Options;

        Guid? createdSemesterId = null;
        Guid lecturerId = Guid.NewGuid();
        Guid student1Id = Guid.NewGuid();
        Guid student2Id = Guid.NewGuid();
        Guid courseId = Guid.NewGuid();
        Guid classId = Guid.NewGuid();
        Guid rubricId = Guid.NewGuid();
        Guid clo1Id = Guid.NewGuid();
        Guid clo2Id = Guid.NewGuid();
        Guid? shiftId = null;
        var questionIds = new List<Guid>();

        using (var reconcileCtx = new OralExamDbContext(options))
        {
            await PostgresSchemaReconciler.ReconcilePostMigrationAsync(reconcileCtx);
        }

        try
        {
            using (var setupCtx = new OralExamDbContext(options))
            {
                var existingSem = await setupCtx.Semesters.AsNoTracking().FirstOrDefaultAsync();
                Guid semesterId;
                if (existingSem is null)
                {
                    var sem = new Semester
                    {
                        Id = Guid.NewGuid(),
                        Code = $"RC3_{Guid.NewGuid():N}"[..9].ToUpperInvariant(),
                        Name = "FA26 RC3 Integration",
                        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
                        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)),
                        IsActive = true
                    };
                    setupCtx.Semesters.Add(sem);
                    await setupCtx.SaveChangesAsync();
                    semesterId = sem.Id;
                    createdSemesterId = sem.Id;
                }
                else
                {
                    semesterId = existingSem.Id;
                }

                setupCtx.Users.AddRange(
                    new User { Id = lecturerId, Email = $"gv_rc3_{lecturerId:N}@fpt.edu.vn", FullName = "GV PG RC3", Role = UserRole.Lecturer, IsActive = true },
                    new User { Id = student1Id, Email = $"sv1_rc3_{student1Id:N}@fpt.edu.vn", FullName = "SV1 PG RC3", Role = UserRole.Student, IsActive = true },
                    new User { Id = student2Id, Email = $"sv2_rc3_{student2Id:N}@fpt.edu.vn", FullName = "SV2 PG RC3", Role = UserRole.Student, IsActive = true }
                );
                setupCtx.Courses.Add(new Course
                {
                    Id = courseId,
                    Code = $"RC3{Guid.NewGuid():N}"[..9].ToUpperInvariant(),
                    Name = "RC3 Course PG",
                    SemesterId = semesterId,
                    Credits = 3,
                    IsActive = true
                });
                setupCtx.Classes.Add(new Class
                {
                    Id = classId,
                    Code = $"CL{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    Name = "RC3 Class PG",
                    JoinCode = $"JC{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    CourseId = courseId,
                    SemesterId = semesterId,
                    LecturerId = lecturerId,
                    IsActive = true
                });
                setupCtx.ClassEnrollments.AddRange(
                    new ClassEnrollment { Id = Guid.NewGuid(), ClassId = classId, StudentId = student1Id, Status = "enrolled" },
                    new ClassEnrollment { Id = Guid.NewGuid(), ClassId = classId, StudentId = student2Id, Status = "enrolled" }
                );
                setupCtx.CourseClos.AddRange(
                    new CourseClo { Id = clo1Id, CourseId = courseId, CloCode = "CLO1", Description = "Architecture", OrderIndex = 1, IsActive = true },
                    new CourseClo { Id = clo2Id, CourseId = courseId, CloCode = "CLO2", Description = "Persistence", OrderIndex = 2, IsActive = true }
                );
                setupCtx.Rubrics.Add(new Rubric { Id = rubricId, CourseId = courseId, Name = "RC3 Rubric", TotalMaxScore = 10.00m, IsActive = true });

                var seededQuestions = new[]
                {
                    new PracticeQuestion { Id = Guid.NewGuid(), CourseId = courseId, CloId = clo1Id, RubricId = rubricId, Title = "PG CLO1 Easy", Content = "Câu 1", SampleAnswer = "Đáp án mẫu đủ năm mươi ký tự cho bài kiểm thử tích hợp PostgreSQL.", Difficulty = "easy", ApprovalStatus = ApprovalStatus.Approved, IsActive = true },
                    new PracticeQuestion { Id = Guid.NewGuid(), CourseId = courseId, CloId = clo1Id, RubricId = rubricId, Title = "PG CLO1 Medium", Content = "Câu 2", SampleAnswer = "Đáp án mẫu đủ năm mươi ký tự cho bài kiểm thử tích hợp PostgreSQL.", Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true },
                    new PracticeQuestion { Id = Guid.NewGuid(), CourseId = courseId, CloId = clo2Id, RubricId = rubricId, Title = "PG CLO2 Easy", Content = "Câu 3", SampleAnswer = "Đáp án mẫu đủ năm mươi ký tự cho bài kiểm thử tích hợp PostgreSQL.", Difficulty = "easy", ApprovalStatus = ApprovalStatus.Approved, IsActive = true },
                    new PracticeQuestion { Id = Guid.NewGuid(), CourseId = courseId, CloId = clo2Id, RubricId = rubricId, Title = "PG CLO2 Medium", Content = "Câu 4", SampleAnswer = "Đáp án mẫu đủ năm mươi ký tự cho bài kiểm thử tích hợp PostgreSQL.", Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true }
                };
                questionIds.AddRange(seededQuestions.Select(q => q.Id));
                setupCtx.PracticeQuestions.AddRange(seededQuestions);
                await setupCtx.SaveChangesAsync();

                var createRes = await new CreateInClassShiftCommandHandler(setupCtx, new CreateInClassShiftCommandValidator())
                    .Handle(new CreateInClassShiftCommand(
                        lecturerId,
                        classId,
                        "PG Selected CLOs Shift",
                        DateTime.UtcNow.AddMinutes(1),
                        DateTime.UtcNow.AddMinutes(40),
                        QuestionPoolMode: "SELECTED_CLOS",
                        TotalQuestionsPerStudent: 2,
                        ExamDurationMinutes: 20,
                        SelectedDifficulties: new[] { "easy", "medium" },
                        SelectedCloIds: new[] { clo1Id, clo2Id }), CancellationToken.None);

                createRes.IsSuccess.Should().BeTrue(because: createRes.Error);
                shiftId = createRes.Value.Id;

                var shiftEntity = await setupCtx.InClassExamShifts.FirstAsync(s => s.Id == shiftId.Value);
                shiftEntity.Status = "IN_PROGRESS";
                await setupCtx.SaveChangesAsync();

                var otp = TotpPasswordHelper.GenerateCode(shiftEntity.TotpSecretKey);

                using var ctx1 = new OralExamDbContext(options);
                using var ctx2 = new OralExamDbContext(options);
                var qMock = new Mock<IGradingQueueChannel>().Object;
                var h1 = new EnterInClassShiftCommandHandler(ctx1, new EnterInClassShiftCommandValidator(), qMock);
                var h2 = new EnterInClassShiftCommandHandler(ctx2, new EnterInClassShiftCommandValidator(), qMock);

                var barrier = new Barrier(2);
                var t1 = Task.Run(async () =>
                {
                    barrier.SignalAndWait(5000);
                    return await h1.Handle(new EnterInClassShiftCommand(student1Id, shiftId.Value, otp), CancellationToken.None);
                });
                var t2 = Task.Run(async () =>
                {
                    barrier.SignalAndWait(5000);
                    return await h2.Handle(new EnterInClassShiftCommand(student2Id, shiftId.Value, otp), CancellationToken.None);
                });

                var joinResults = await Task.WhenAll(t1, t2);
                joinResults[0].IsSuccess.Should().BeTrue(because: joinResults[0].Error);
                joinResults[1].IsSuccess.Should().BeTrue(because: joinResults[1].Error);

                // Do advisory lock tuần tự hóa 2 sinh viên trên PostgreSQL, 4 câu khác nhau được chia đều 2 + 2 không trùng
                var allAssigned = joinResults[0].Value.AssignedQuestionIds!
                    .Concat(joinResults[1].Value.AssignedQuestionIds!)
                    .ToList();
                allAssigned.Should().HaveCount(4);
                allAssigned.Distinct().Should().HaveCount(4, "pg_advisory_xact_lock tuần tự hóa Anti-Collision không để 2 SV bốc trùng câu khi kho có đủ 4 câu");
            }
        }
        finally
        {
            using var cleanupCtx = new OralExamDbContext(options);
            if (shiftId.HasValue)
            {
                var parts = await cleanupCtx.InClassShiftParticipants.Where(p => p.ShiftId == shiftId.Value).ToListAsync();
                if (parts.Count > 0) cleanupCtx.InClassShiftParticipants.RemoveRange(parts);
                var shift = await cleanupCtx.InClassExamShifts.FindAsync(shiftId.Value);
                if (shift is not null) cleanupCtx.InClassExamShifts.Remove(shift);
                await cleanupCtx.SaveChangesAsync();
            }

            var qs = await cleanupCtx.PracticeQuestions.Where(q => questionIds.Contains(q.Id)).ToListAsync();
            if (qs.Count > 0) cleanupCtx.PracticeQuestions.RemoveRange(qs);
            var clos = await cleanupCtx.CourseClos.Where(c => c.Id == clo1Id || c.Id == clo2Id).ToListAsync();
            if (clos.Count > 0) cleanupCtx.CourseClos.RemoveRange(clos);
            var rub = await cleanupCtx.Rubrics.FindAsync(rubricId);
            if (rub is not null) cleanupCtx.Rubrics.Remove(rub);
            var enrs = await cleanupCtx.ClassEnrollments.Where(e => e.ClassId == classId).ToListAsync();
            if (enrs.Count > 0) cleanupCtx.ClassEnrollments.RemoveRange(enrs);
            var clsEntity = await cleanupCtx.Classes.FindAsync(classId);
            if (clsEntity is not null) cleanupCtx.Classes.Remove(clsEntity);
            var crs = await cleanupCtx.Courses.FindAsync(courseId);
            if (crs is not null) cleanupCtx.Courses.Remove(crs);
            var users = await cleanupCtx.Users.Where(u => u.Id == lecturerId || u.Id == student1Id || u.Id == student2Id).ToListAsync();
            if (users.Count > 0) cleanupCtx.Users.RemoveRange(users);
            await cleanupCtx.SaveChangesAsync();

            if (createdSemesterId.HasValue)
            {
                var semId = createdSemesterId.Value;
                var stillReferenced = await cleanupCtx.Classes.AnyAsync(c => c.SemesterId == semId)
                    || await cleanupCtx.Courses.AnyAsync(c => c.SemesterId == semId);
                if (!stillReferenced)
                {
                    var sem = await cleanupCtx.Semesters.FindAsync(semId);
                    if (sem is not null)
                    {
                        cleanupCtx.Semesters.Remove(sem);
                        await cleanupCtx.SaveChangesAsync();
                    }
                }
            }
        }
    }

    private static string IssueJwt(User user)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(JwtSigningKey);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("role", user.Role),
                new Claim("auth_version", user.AuthVersion.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
