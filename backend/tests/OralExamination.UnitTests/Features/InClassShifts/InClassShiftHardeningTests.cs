using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OralExamination.API.Hubs;
using OralExamination.API.Workers;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;
using OralExamination.Application.Features.InClassShifts.Commands.DeleteInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.UpdateInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Application.Features.InClassShifts.Queries.GetNextInClassFollowUp;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;

namespace OralExamination.UnitTests.Features.InClassShifts;

public sealed class InClassShiftHardeningTests
{
    private const string JwtSigningKey = "SuperSecretKeyForInClassShiftHardeningMustBeAtLeast32BytesLong!!";
    private const string JwtIssuer = "OralExamInClassHardeningIssuer";
    private const string JwtAudience = "OralExamInClassHardeningAudience";

    private static OralExamDbContext CreateInMemoryContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static PracticeQuestion BuildApprovedQuestion(Guid courseId, Guid? cloId, Guid rubricId, string difficulty, string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            CloId = cloId,
            RubricId = rubricId,
            Title = title,
            Content = $"Nội dung câu hỏi: {title}",
            SampleAnswer = "Đáp án mẫu chuẩn học thuật có độ dài lớn hơn năm mươi ký tự để kiểm chứng MF-05.",
            Difficulty = difficulty,
            BloomLevel = BloomLevel.Apply,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

    [Fact(DisplayName = "MF-05 RBAC/BOLA: Sửa/xóa ca thi, SignalR Hub & DLQ chỉ cho phép đúng Giảng viên trực tiếp phụ trách lớp (Class.LecturerId)")]
    public async Task RbacAndBola_EnforcesStrictClassLecturerOwnershipAcrossMutationsHubAndDlq()
    {
        using var db = CreateInMemoryContext();
        var oldShiftCreator = new User { Id = Guid.NewGuid(), Email = "old-gv@fpt.edu.vn", FullName = "GV Cũ", Role = UserRole.Lecturer, IsActive = true };
        var currentClassLecturer = new User { Id = Guid.NewGuid(), Email = "current-gv@fpt.edu.vn", FullName = "GV Chủ Lớp Hiện Tại", Role = UserRole.Lecturer, IsActive = true };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@fpt.edu.vn", FullName = "Admin", Role = UserRole.Admin, IsActive = true };
        var headUser = new User { Id = Guid.NewGuid(), Email = "head@fpt.edu.vn", FullName = "Trưởng BM", Role = UserRole.DepartmentHead, IsActive = true };
        var enrolledStudent = new User { Id = Guid.NewGuid(), Email = "sv@fpt.edu.vn", FullName = "Sinh Viên", Role = UserRole.Student, IsActive = true };
        var outsiderStudent = new User { Id = Guid.NewGuid(), Email = "outsider@fpt.edu.vn", FullName = "SV Lớp Khác", Role = UserRole.Student, IsActive = true };

        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", DepartmentHeadUserId = headUser.Id, IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "PRN231 - SE1801", CourseId = course.Id, LecturerId = currentClassLecturer.Id, IsActive = true };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            LecturerId = oldShiftCreator.Id, // Ca cũ tạo trước khi đổi GV chủ lớp
            ShiftName = "Ca Kiểm Tra Giữa Kỳ",
            TotpSecretKey = "SECRET123456",
            Status = "SCHEDULED",
            StartTime = DateTime.UtcNow.AddHours(2),
            EndTime = DateTime.UtcNow.AddHours(3)
        };
        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = enrolledStudent.Id,
            QuestionId = Guid.NewGuid(),
            Transcript = "Câu trả lời của sinh viên",
            Status = "SUBMITTED"
        };
        var dlqItem = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            CorrelationId = submission.Id,
            Status = "pending",
            PayloadJson = JsonSerializer.Serialize(new GradingTask
            {
                AnswerId = submission.Id,
                InClassSubmissionId = submission.Id,
                SessionId = shift.Id,
                StudentId = enrolledStudent.Id,
                QuestionId = submission.QuestionId
            }),
            ErrorMessage = "Timeout"
        };

        db.Users.AddRange(oldShiftCreator, currentClassLecturer, adminUser, headUser, enrolledStudent, outsiderStudent);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = enrolledStudent.Id, Status = "enrolled" });
        db.InClassExamShifts.Add(shift);
        db.InClassExamSubmissions.Add(submission);
        db.DeadLetterQueues.Add(dlqItem);
        await db.SaveChangesAsync();

        // 1. DeleteInClassShift: Chặn Admin, Trưởng BM và GV cũ; chỉ cho phép currentClassLecturer
        var deleteHandler = new DeleteInClassShiftCommandHandler(db);
        (await deleteHandler.Handle(new DeleteInClassShiftCommand(adminUser.Id, shift.Id), CancellationToken.None))
            .Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);
        (await deleteHandler.Handle(new DeleteInClassShiftCommand(headUser.Id, shift.Id), CancellationToken.None))
            .Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);
        (await deleteHandler.Handle(new DeleteInClassShiftCommand(oldShiftCreator.Id, shift.Id), CancellationToken.None))
            .Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);

        // 2. DLQ InClassGrading: mọi giảng viên đều bị chặn, chỉ Admin quản lý DLQ.
        var oldGvDlq = await new GetDlqTasksQueryHandler(db).Handle(new GetDlqTasksQuery(ActorUserId: oldShiftCreator.Id), CancellationToken.None);
        oldGvDlq.IsSuccess.Should().BeFalse();

        var currentGvDlq = await new GetDlqTasksQueryHandler(db).Handle(new GetDlqTasksQuery(ActorUserId: currentClassLecturer.Id), CancellationToken.None);
        currentGvDlq.IsSuccess.Should().BeFalse();
        var adminDlq = await new GetDlqTasksQueryHandler(db).Handle(new GetDlqTasksQuery(ActorUserId: adminUser.Id), CancellationToken.None);
        adminDlq.IsSuccess.Should().BeTrue();
        adminDlq.Value.Should().ContainSingle(x => x.Id == dlqItem.Id);

        var queueMock = new Mock<IGradingQueueChannel>();
        var deniedReplay = await new ReplayDlqTaskCommandHandler(db, queueMock.Object)
            .Handle(new ReplayDlqTaskCommand(dlqItem.Id, oldShiftCreator.Id, UserRole.Lecturer), CancellationToken.None);
        deniedReplay.IsSuccess.Should().BeFalse();

        // 3. InClassShiftHub: Chặn Admin, Trưởng BM, GV cũ và SV ngoài lớp; mở đúng nhóm cho GV chủ lớp và SV trong lớp
        var addedGroups = new List<(string ConnectionId, string GroupName)>();
        var groupsMock = new Mock<IGroupManager>();
        groupsMock
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((conn, grp, _) => addedGroups.Add((conn, grp)))
            .Returns(Task.CompletedTask);

        static InClassShiftHub CreateHubForUser(OralExamDbContext ctx, IGroupManager groups, User actor, string connId)
        {
            var hub = new InClassShiftHub(ctx) { Groups = groups };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                new Claim(ClaimTypes.Role, actor.Role)
            }, "TestAuth"));
            var callerContext = new Mock<HubCallerContext>();
            callerContext.SetupGet(c => c.User).Returns(claims);
            callerContext.SetupGet(c => c.ConnectionId).Returns(connId);
            callerContext.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
            hub.Context = callerContext.Object;
            return hub;
        }

        await FluentActions.Invoking(() => CreateHubForUser(db, groupsMock.Object, adminUser, "c-admin").JoinShift(shift.Id.ToString()))
            .Should().ThrowAsync<HubException>();
        await FluentActions.Invoking(() => CreateHubForUser(db, groupsMock.Object, headUser, "c-head").JoinShift(shift.Id.ToString()))
            .Should().ThrowAsync<HubException>();
        await FluentActions.Invoking(() => CreateHubForUser(db, groupsMock.Object, oldShiftCreator, "c-old-gv").JoinShift(shift.Id.ToString()))
            .Should().ThrowAsync<HubException>();
        await FluentActions.Invoking(() => CreateHubForUser(db, groupsMock.Object, outsiderStudent, "c-outsider").JoinShift(shift.Id.ToString()))
            .Should().ThrowAsync<HubException>();

        await CreateHubForUser(db, groupsMock.Object, currentClassLecturer, "c-owner").JoinShift(shift.Id.ToString());
        addedGroups.Should().Contain(("c-owner", InClassShiftHub.StaffGroupName(shift.Id)));

        await CreateHubForUser(db, groupsMock.Object, enrolledStudent, "c-student").JoinShift(shift.Id.ToString());
        addedGroups.Should().Contain(("c-student", InClassShiftHub.StudentGroupName(shift.Id, enrolledStudent.Id)));
        addedGroups.Should().NotContain(("c-student", InClassShiftHub.StaffGroupName(shift.Id)));
    }

    [Fact(DisplayName = "MF-05 UpdateShift RC-3 & HTTP Status: Cập nhật SELECTED_CLOS thành công, trả 422 khi thiếu câu/CLO và chuẩn hóa 403/404")]
    public async Task UpdateShift_SupportsSelectedClosRc3_AndControllerMaps403And404And422()
    {
        var dbName = Guid.NewGuid().ToString();
        var ownerLecturer = new User { Id = Guid.NewGuid(), Email = "owner@fpt.edu.vn", FullName = "Owner", Role = UserRole.Lecturer, IsActive = true, AuthVersion = 1 };
        var otherLecturer = new User { Id = Guid.NewGuid(), Email = "other@fpt.edu.vn", FullName = "Other", Role = UserRole.Lecturer, IsActive = true, AuthVersion = 1 };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin-http@fpt.edu.vn", FullName = "Admin", Role = UserRole.Admin, IsActive = true, AuthVersion = 1 };
        var headUser = new User { Id = Guid.NewGuid(), Email = "head-http@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true, AuthVersion = 1 };
        var unenrolledStudent = new User { Id = Guid.NewGuid(), Email = "outsider@fpt.edu.vn", FullName = "Outsider", Role = UserRole.Student, IsActive = true, AuthVersion = 1 };

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

        Guid shiftId;
        Guid clo1Id;
        Guid clo2Id;

        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var rubricId = Guid.NewGuid();
            var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", IsActive = true };
            var cls = new Class { Id = Guid.NewGuid(), Code = "SE1811", Name = "SWD392 - SE1811", CourseId = course.Id, LecturerId = ownerLecturer.Id, IsActive = true };
            var clo1 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "Clean Arch", OrderIndex = 1, IsActive = true };
            var clo2 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "DDD", OrderIndex = 2, IsActive = true };
            var shift = new InClassExamShift
            {
                Id = Guid.NewGuid(),
                ClassId = cls.Id,
                LecturerId = ownerLecturer.Id,
                ShiftName = "Ca Ban Đầu",
                TotpSecretKey = "KEEP_SECRET_KEY",
                Status = "SCHEDULED",
                StartTime = DateTime.UtcNow.AddHours(2),
                EndTime = DateTime.UtcNow.AddHours(3)
            };

            shiftId = shift.Id;
            clo1Id = clo1.Id;
            clo2Id = clo2.Id;

            db.Users.AddRange(ownerLecturer, otherLecturer, adminUser, headUser, unenrolledStudent);
            db.Courses.Add(course);
            db.Classes.Add(cls);
            db.CourseClos.AddRange(clo1, clo2);
            db.Rubrics.Add(new Rubric { Id = rubricId, CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true });
            db.PracticeQuestions.AddRange(
                BuildApprovedQuestion(course.Id, clo1.Id, rubricId, "easy", "CLO1 Easy"),
                BuildApprovedQuestion(course.Id, clo2.Id, rubricId, "medium", "CLO2 Medium")
            );
            db.InClassExamShifts.Add(shift);
            await db.SaveChangesAsync();
        }

        // 1. GV lớp khác gọi GET /submissions hoặc GET /current-password -> 403 Forbidden (thay vì 400); Admin & Trưởng BM gọi DELETE ca thật -> 403 Forbidden
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(otherLecturer));
        var otherSubRes = await client.GetAsync($"/api/v1/lecturer/in-class-shifts/{shiftId}/submissions");
        otherSubRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var otherOtpRes = await client.GetAsync($"/api/v1/lecturer/in-class-shifts/{shiftId}/active-password");
        otherOtpRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(adminUser));
        (await client.DeleteAsync($"/api/v1/in-class-shifts/{shiftId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(headUser));
        (await client.DeleteAsync($"/api/v1/in-class-shifts/{shiftId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. SV không ghi danh gọi GET /my-submissions -> 403 Forbidden; ca không tồn tại -> 404 NotFound
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(unenrolledStudent));
        var outsiderResultsRes = await client.GetAsync($"/api/v1/student/in-class-shifts/{shiftId}/my-submissions");
        outsiderResultsRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var missingShiftRes = await client.GetAsync($"/api/v1/student/in-class-shifts/{Guid.NewGuid()}/my-submissions");
        missingShiftRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. More selected CLOs than questions is allowed: the CLOs define the pool.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(ownerLecturer));
        var fewerQuestionsUpdate = new
        {
            shiftName = "Ca RC-3 Lỗi",
            startTime = DateTime.UtcNow.AddHours(3),
            endTime = DateTime.UtcNow.AddHours(4),
            questionPoolMode = "SELECTED_CLOS",
            totalQuestionsPerStudent = 1,
            selectedDifficulties = new[] { "easy", "medium" },
            selectedCloIds = new[] { clo1Id, clo2Id },
            examDurationMinutes = 20
        };
        var fewerQuestionsRes = await client.PutAsJsonAsync($"/api/v1/lecturer/in-class-shifts/{shiftId}", fewerQuestionsUpdate);
        fewerQuestionsRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var verifyScope = server.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var updatedShift = await verifyDb.InClassExamShifts.AsNoTracking().FirstAsync(s => s.Id == shiftId);
            updatedShift.TotalQuestionsPerStudent.Should().Be(1);
            InClassShiftPoolConfig.ParseGuidList(updatedShift.SelectedCloIdsJson)
                .Should().Equal(clo1Id, clo2Id);
        }

        // Requesting more questions than the selected pool contains must still return 422.
        var invalidPoolUpdate = new
        {
            shiftName = "Ca RC-3 Thiếu Câu",
            startTime = DateTime.UtcNow.AddHours(3),
            endTime = DateTime.UtcNow.AddHours(4),
            questionPoolMode = "SELECTED_CLOS",
            totalQuestionsPerStudent = 3,
            selectedDifficulties = new[] { "easy", "medium" },
            selectedCloIds = new[] { clo1Id, clo2Id },
            examDurationMinutes = 20
        };
        var unprocessableUpdateRes = await client.PutAsJsonAsync($"/api/v1/lecturer/in-class-shifts/{shiftId}", invalidPoolUpdate);
        unprocessableUpdateRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 4. GV chủ lớp PUT UpdateShift hợp lệ với SELECTED_CLOS (2 câu/SV, 2 CLO) -> 204 NoContent & bảo toàn TotpSecretKey
        var validPoolUpdate = new
        {
            shiftName = "Ca RC-3 Đã Cập Nhật",
            startTime = DateTime.UtcNow.AddHours(3),
            endTime = DateTime.UtcNow.AddHours(4),
            questionPoolMode = "SELECTED_CLOS",
            totalQuestionsPerStudent = 2,
            selectedDifficulties = new[] { "easy", "medium" },
            selectedCloIds = new[] { clo1Id, clo2Id },
            hasFollowUp = true,
            maxFollowUpQuestions = 2,
            examDurationMinutes = 25
        };
        var okUpdateRes = await client.PutAsJsonAsync($"/api/v1/lecturer/in-class-shifts/{shiftId}", validPoolUpdate);
        okUpdateRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var verifyScope = server.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var updatedShift = await verifyDb.InClassExamShifts.AsNoTracking().FirstAsync(s => s.Id == shiftId);
            updatedShift.ShiftName.Should().Be("Ca RC-3 Đã Cập Nhật");
            updatedShift.QuestionPoolMode.Should().Be("SELECTED_CLOS");
            updatedShift.TotpSecretKey.Should().Be("KEEP_SECRET_KEY");
            InClassShiftPoolConfig.ParseStoredDifficulties(updatedShift.SelectedDifficultiesJson)
                .Should().Equal("easy", "medium");
            InClassShiftPoolConfig.ParseGuidList(updatedShift.SelectedCloIdsJson)
                .Should().Equal(clo1Id, clo2Id);
        }
    }

    [Fact(DisplayName = "MF-05 Audit & Follow-up Race: Bắt buộc lý do hậu kiểm, không ghi đè FollowUpPrompt đã sinh, ẩn điểm SV trên SignalR và chuyển Participant sang GRADED")]
    public async Task FollowUpRace_ParticipantGradedTransition_RedactedSignalR_AndMandatoryAuditReason()
    {
        var dbName = Guid.NewGuid().ToString();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-grade@fpt.edu.vn", FullName = "GV Chấm", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-grade@fpt.edu.vn", FullName = "SV Thi", StudentCode = "SE189999", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1820", Name = "PRN231 - SE1820", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric Chuẩn", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Kiến thức cốt lõi", Description = "Giải thích đúng bản chất", MaxScore = 10m, Weight = 1.0m, OrderIndex = 1 };
        var question = BuildApprovedQuestion(course.Id, null, rubric.Id, "medium", "Giải thích Dependency Injection");

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Follow-up Race",
            TotpSecretKey = "RACE_SECRET",
            Status = "IN_PROGRESS",
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            TotalQuestionsPerStudent = 1,
            ExamDurationMinutes = 20,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30)
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { question.Id }),
            Status = "SUBMITTED",
            JoinedAt = DateTime.UtcNow.AddMinutes(-3)
        };
        var mainSubmission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Dependency Injection giúp giảm phụ thuộc cứng giữa các lớp dịch vụ.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());

        var aiMock = new Mock<IAiGradingService>();
        aiMock
            .Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 7.5m,
                Feedback = "Trình bày đúng khái niệm nhưng cần nêu thêm vòng đời Scoped/Singleton.",
                ConfidenceScore = 0.92m,
                FollowUpPrompt = "Câu hỏi phụ MỚI từ Worker không được phép ghi đè câu đã phát cho sinh viên!"
            });
        services.AddSingleton(aiMock.Object);

        using var provider = services.BuildServiceProvider();
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.AddRange(lecturer, student);
            db.Courses.Add(course);
            db.Classes.Add(cls);
            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(criterion);
            db.PracticeQuestions.Add(question);
            db.InClassExamShifts.Add(shift);
            db.InClassShiftParticipants.Add(participant);
            db.InClassExamSubmissions.Add(mainSubmission);
            await db.SaveChangesAsync();
        }

        // 1. Sinh câu hỏi phụ trước khi GradingQueueWorker chấm xong (mô phỏng SV vừa bấm nộp là gọi next-followup ngay)
        const string canonicalAskedPrompt = "Hãy phân biệt Transient, Scoped và Singleton trong ASP.NET Core DI?";
        using (var followUpScope = provider.CreateScope())
        {
            var db = followUpScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var promptAiMock = new Mock<IAiGradingService>();
            promptAiMock
                .Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AiGradingResult { FollowUpPrompt = canonicalAskedPrompt });

            var nextFuRes = await new GetNextInClassFollowUpQueryHandler(db, promptAiMock.Object)
                .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, mainSubmission.Id), CancellationToken.None);

            nextFuRes.IsSuccess.Should().BeTrue();
            nextFuRes.Value.HasMoreFollowUp.Should().BeTrue();
            nextFuRes.Value.FollowUpPrompt.Should().Be(canonicalAskedPrompt);
        }

        // 2. GradingQueueWorker chạy chấm câu chính -> KHÔNG ghi đè FollowUpPrompt, chuyển participant.Status -> GRADED, và che điểm trên StudentGroup
        object? staffPayload = null;
        object? studentPayload = null;

        var staffClientMock = new Mock<IInClassShiftClient>();
        staffClientMock
            .Setup(c => c.InClassSubmissionGraded(It.IsAny<object>()))
            .Callback<object>(p => staffPayload = p)
            .Returns(Task.CompletedTask);

        var studentClientMock = new Mock<IInClassShiftClient>();
        studentClientMock
            .Setup(c => c.InClassSubmissionGraded(It.IsAny<object>()))
            .Callback<object>(p => studentPayload = p)
            .Returns(Task.CompletedTask);

        var hubClientsMock = new Mock<IHubClients<IInClassShiftClient>>();
        hubClientsMock.Setup(c => c.Group(InClassShiftHub.StaffGroupName(shift.Id))).Returns(staffClientMock.Object);
        hubClientsMock.Setup(c => c.Group(InClassShiftHub.StudentGroupName(shift.Id, student.Id))).Returns(studentClientMock.Object);

        var inClassHubMock = new Mock<IHubContext<InClassShiftHub, IInClassShiftClient>>();
        inClassHubMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

        var practiceHubMock = new Mock<IHubContext<PracticeHub, IPracticeClient>>();

        var singleTaskChannel = new Mock<IGradingQueueChannel>();
        singleTaskChannel
            .Setup(c => c.ReadAllAsync(It.IsAny<CancellationToken>()))
            .Returns(YieldSingleTaskAsync(new GradingTask
            {
                AnswerId = mainSubmission.Id,
                InClassSubmissionId = mainSubmission.Id,
                SessionId = shift.Id,
                StudentId = student.Id,
                QuestionId = question.Id,
                AnswerText = mainSubmission.Transcript
            }));

        var worker = new GradingQueueWorker(
            singleTaskChannel.Object,
            provider,
            practiceHubMock.Object,
            NullLogger<GradingQueueWorker>.Instance,
            configuration: null,
            inClassHubContext: inClassHubMock.Object);

        await worker.StartAsync(CancellationToken.None);
        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask;
        }
        await worker.StopAsync(CancellationToken.None);

        using (var checkScope = provider.CreateScope())
        {
            var db = checkScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var gradedSub = await db.InClassExamSubmissions.AsNoTracking().FirstAsync(s => s.Id == mainSubmission.Id);
            gradedSub.Status.Should().Be("AI_GRADED");
            gradedSub.AiScore.Should().Be(7.5m);
            gradedSub.FollowUpPrompt.Should().Be(canonicalAskedPrompt, "Worker không được phép ghi đè câu hỏi phụ đã hiển thị cho sinh viên");

            var updatedParticipant = await db.InClassShiftParticipants.AsNoTracking().FirstAsync(p => p.Id == participant.Id);
            updatedParticipant.Status.Should().Be("GRADED", "Đã nộp đủ câu chính và chấm xong toàn bộ lượt nộp thì participant phải chuyển sang GRADED");
        }

        staffPayload.Should().NotBeNull();
        var staffJson = JsonSerializer.Serialize(staffPayload);
        staffJson.Should().Contain("7.5", "Payload SignalR gửi cho Giảng viên phải có điểm AI thời gian thực");
        studentPayload.Should().NotBeNull();
        var studentJson = JsonSerializer.Serialize(studentPayload);
        studentJson.Should().Contain("AI_GRADED");
        studentJson.Should().NotContain("7.5", "Payload SignalR gửi cho sinh viên trước khi chốt sổ tuyệt đối không được lộ điểm AI");
        studentJson.Should().NotContain("AiScore");
        studentJson.Should().NotContain("aiScore");

        // 3. Khi ca thi đã khóa / đóng quá hạn -> GetNextInClassFollowUp trả HasMoreFollowUp = false cho lượt chưa sinh câu phụ
        using (var closedCheckScope = provider.CreateScope())
        {
            var db = closedCheckScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var followUpSub = new InClassExamSubmission
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = student.Id,
                QuestionId = question.Id,
                IsFollowUp = true,
                ParentSubmissionId = mainSubmission.Id,
                FollowUpIndex = 1,
                Transcript = "Transient tạo mới mỗi lần inject, Scoped theo HTTP request, Singleton suốt vòng đời app.",
                Status = "AI_GRADED",
                AiScore = 8.5m,
                FinalScore = 8.5m
            };
            var trackedShift = await db.InClassExamShifts.FirstAsync(s => s.Id == shift.Id);
            trackedShift.Status = "CLOSED";
            trackedShift.EndTime = DateTime.UtcNow.AddMinutes(-10); // Quá cửa sổ grace
            db.InClassExamSubmissions.Add(followUpSub);
            await db.SaveChangesAsync();

            var closedFuRes = await new GetNextInClassFollowUpQueryHandler(db, aiMock.Object)
                .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, followUpSub.Id), CancellationToken.None);
            closedFuRes.IsSuccess.Should().BeTrue();
            closedFuRes.Value.HasMoreFollowUp.Should().BeFalse();

            // Kể cả bản ghi cha đã được Worker điền sẵn FollowUpPrompt trước đó cũng phải trả HasMoreFollowUp = false khi ca đã đóng quá hạn
            var prePopulatedClosedRes = await new GetNextInClassFollowUpQueryHandler(db, aiMock.Object)
                .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, mainSubmission.Id), CancellationToken.None);
            prePopulatedClosedRes.IsSuccess.Should().BeTrue();
            prePopulatedClosedRes.Value.HasMoreFollowUp.Should().BeFalse();
            prePopulatedClosedRes.Value.FollowUpPrompt.Should().BeNull();

            // 4. Hậu kiểm điểm bắt buộc có lý do (LecturerNotes không được rỗng)
            var adjustHandler = new AdjustInClassSubmissionScoreCommandHandler(db, new AdjustInClassSubmissionScoreCommandValidator());
            var emptyReasonRes = await adjustHandler.Handle(
                new AdjustInClassSubmissionScoreCommand(lecturer.Id, followUpSub.Id, 9.0m, "   "),
                CancellationToken.None);
            emptyReasonRes.IsSuccess.Should().BeFalse();
            emptyReasonRes.Error.Should().Be(InClassShiftErrors.EmptyAuditReason);

            var validReasonRes = await adjustHandler.Handle(
                new AdjustInClassSubmissionScoreCommand(lecturer.Id, followUpSub.Id, 9.0m, "  Sinh viên phân tích rất rõ ví dụ thực tế  "),
                CancellationToken.None);
            validReasonRes.IsSuccess.Should().BeTrue();
            validReasonRes.Value.Status.Should().Be("AUDITED");
            validReasonRes.Value.LecturerNotes.Should().Be("Sinh viên phân tích rất rõ ví dụ thực tế");
        }
    }

    [Fact(DisplayName = "MF-05 Dynamic Follow-up Only: Không bao giờ sinh câu hỏi phụ khuôn mẫu có sẵn khi AI trả rỗng, boilerplate hoặc SV từ chối trả lời")]
    public async Task InClassFollowUp_NeverFallsBackToCannedPrompt_WhenAiReturnsNullBoilerplateOrRefusal()
    {
        using var db = CreateInMemoryContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-fu@fpt.edu.vn", FullName = "GV FU", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-fu@fpt.edu.vn", FullName = "SV FU", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1821", Name = "PRN231 - SE1821", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Hiểu bản chất", Description = "Giải thích đúng", MaxScore = 10m, Weight = 1m, OrderIndex = 1 };
        var question = BuildApprovedQuestion(course.Id, null, rubric.Id, "medium", "Giải thích Middleware Pipeline");
        question.FollowUpPrompt = "Câu hỏi phụ tĩnh viết sẵn trong DB không được phép dùng trong MF-05";

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Dynamic Follow-up",
            TotpSecretKey = "DYN_SECRET",
            Status = "IN_PROGRESS",
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            TotalQuestionsPerStudent = 1,
            ExamDurationMinutes = 20,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30)
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { question.Id }),
            Status = "IN_PROGRESS",
            JoinedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var normalSub = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Middleware tạo thành chuỗi đường ống xử lý HTTP request và response theo thứ tự đăng ký.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddSeconds(-10)
        };
        var refusalSub = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Em không biết câu này ạ.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddSeconds(-5)
        };

        db.Users.AddRange(lecturer, student);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.Rubrics.Add(rubric);
        db.RubricCriteria.Add(criterion);
        db.PracticeQuestions.Add(question);
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.Add(participant);
        db.InClassExamSubmissions.AddRange(normalSub, refusalSub);
        await db.SaveChangesAsync();

        // 1. AI trả FollowUpPrompt = null -> phải trả HasMoreFollowUp = false, không sinh câu cứng
        var nullAi = new Mock<IAiGradingService>();
        nullAi.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult { Score = 9.0m, FollowUpPrompt = null });

        var nullRes = await new GetNextInClassFollowUpQueryHandler(db, nullAi.Object)
            .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, normalSub.Id), CancellationToken.None);
        nullRes.IsSuccess.Should().BeTrue();
        nullRes.Value.HasMoreFollowUp.Should().BeFalse();
        nullRes.Value.FollowUpPrompt.Should().BeNull();

        // 2. AI trả boilerplate chung chung -> FollowUpPromptBuilder phải chặn, trả HasMoreFollowUp = false
        var boilerplateAi = new Mock<IAiGradingService>();
        boilerplateAi.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 6.0m,
                FollowUpPrompt = "Hãy phân tích sâu hơn cơ chế hoạt động và đưa ra một ví dụ thực tế cho câu trả lời của bạn."
            });

        var boilerplateRes = await new GetNextInClassFollowUpQueryHandler(db, boilerplateAi.Object)
            .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, normalSub.Id), CancellationToken.None);
        boilerplateRes.IsSuccess.Should().BeTrue();
        boilerplateRes.Value.HasMoreFollowUp.Should().BeFalse();
        boilerplateRes.Value.FollowUpPrompt.Should().BeNull();

        // 3. SV trả lời từ chối ("Em không biết") -> dù AI lỡ sinh câu phụ vẫn phải chặn trả HasMoreFollowUp = false
        var validAi = new Mock<IAiGradingService>();
        validAi.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 5.0m,
                FollowUpPrompt = "Vì sao UseCors phải đặt trước UseStaticFiles trong ASP.NET Core?"
            });

        var refusalRes = await new GetNextInClassFollowUpQueryHandler(db, validAi.Object)
            .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, refusalSub.Id), CancellationToken.None);
        refusalRes.IsSuccess.Should().BeTrue();
        refusalRes.Value.HasMoreFollowUp.Should().BeFalse();
        refusalRes.Value.FollowUpPrompt.Should().BeNull();

        // 4. Khi SV trả lời có nội dung thật và AI sinh câu động đúng ngữ cảnh -> trả HasMoreFollowUp = true với đúng câu AI sinh
        var dynamicRes = await new GetNextInClassFollowUpQueryHandler(db, validAi.Object)
            .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, normalSub.Id), CancellationToken.None);
        dynamicRes.IsSuccess.Should().BeTrue();
        dynamicRes.Value.HasMoreFollowUp.Should().BeTrue();
        dynamicRes.Value.FollowUpPrompt.Should().Be("Vì sao UseCors phải đặt trước UseStaticFiles trong ASP.NET Core?");
    }

    [Fact(DisplayName = "MF-05 DLQ Replay Worker: Bảo vệ bài AUDITED & ca đã khóa, giữ nguyên IsFollowUp + ParentAnswerId khi cứu bài treo")]
    public async Task OfficialExamDlqReplayWorker_ProtectsAuditedAndLockedShifts_AndPreservesFollowUpMetadata()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        using var provider = services.BuildServiceProvider();

        var openShiftId = Guid.NewGuid();
        var lockedShiftId = Guid.NewGuid();
        var mainSubId = Guid.NewGuid();
        var stuckFollowUpSubId = Guid.NewGuid();
        var auditedSubId = Guid.NewGuid();
        var lockedShiftStuckSubId = Guid.NewGuid();
        var dlqAuditedId = Guid.NewGuid();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.InClassExamShifts.AddRange(
                new InClassExamShift
                {
                    Id = openShiftId,
                    ClassId = Guid.NewGuid(),
                    LecturerId = Guid.NewGuid(),
                    ShiftName = "Ca Đang Chấm",
                    TotpSecretKey = "OPEN",
                    Status = "CLOSED",
                    IsLocked = false
                },
                new InClassExamShift
                {
                    Id = lockedShiftId,
                    ClassId = Guid.NewGuid(),
                    LecturerId = Guid.NewGuid(),
                    ShiftName = "Ca Đã Chốt Sổ",
                    TotpSecretKey = "LOCKED",
                    Status = "FINALIZED",
                    IsLocked = true
                });

            db.InClassExamSubmissions.AddRange(
                new InClassExamSubmission
                {
                    Id = mainSubId,
                    ShiftId = openShiftId,
                    StudentId = Guid.NewGuid(),
                    QuestionId = Guid.NewGuid(),
                    Transcript = "Câu trả lời chính",
                    Status = "AI_GRADED",
                    AiScore = 6.0m,
                    FinalScore = 6.0m,
                    SubmittedAt = DateTime.UtcNow.AddMinutes(-6)
                },
                new InClassExamSubmission
                {
                    Id = stuckFollowUpSubId,
                    ShiftId = openShiftId,
                    StudentId = Guid.NewGuid(),
                    QuestionId = Guid.NewGuid(),
                    Transcript = "Câu trả lời phụ bị treo quá 2 phút",
                    IsFollowUp = true,
                    ParentSubmissionId = mainSubId,
                    FollowUpIndex = 1,
                    Status = "SUBMITTED",
                    AiScore = null,
                    SubmittedAt = DateTime.UtcNow.AddMinutes(-16)
                },
                new InClassExamSubmission
                {
                    Id = auditedSubId,
                    ShiftId = openShiftId,
                    StudentId = Guid.NewGuid(),
                    QuestionId = Guid.NewGuid(),
                    Transcript = "Bài đã được GV hậu kiểm",
                    Status = "AUDITED",
                    FinalScore = 8.5m,
                    LecturerNotes = "GV đã chấm tay",
                    SubmittedAt = DateTime.UtcNow.AddMinutes(-10)
                },
                new InClassExamSubmission
                {
                    Id = lockedShiftStuckSubId,
                    ShiftId = lockedShiftId,
                    StudentId = Guid.NewGuid(),
                    QuestionId = Guid.NewGuid(),
                    Transcript = "Bài thuộc ca đã khóa một chiều",
                    Status = "SUBMITTED",
                    AiScore = null,
                    SubmittedAt = DateTime.UtcNow.AddMinutes(-10)
                });

            db.DeadLetterQueues.Add(new DlqEntity
            {
                Id = dlqAuditedId,
                TaskType = "InClassGrading",
                CorrelationId = auditedSubId,
                Status = "pending",
                NextRetryAt = DateTime.UtcNow.AddMinutes(-1),
                ErrorMessage = "Transient LLM failure",
                PayloadJson = JsonSerializer.Serialize(new GradingTask
                {
                    AnswerId = auditedSubId,
                    InClassSubmissionId = auditedSubId,
                    SessionId = openShiftId
                })
            });

            await db.SaveChangesAsync();
        }

        var enqueuedTasks = new List<GradingTask>();
        var channelMock = new Mock<IGradingQueueChannel>();
        channelMock
            .Setup(c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Callback<GradingTask, CancellationToken>((task, _) => enqueuedTasks.Add(task))
            .Returns(ValueTask.CompletedTask);

        var replayWorker = new OfficialExamDlqReplayWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            channelMock.Object);

        await replayWorker.ReplayDueAsync(CancellationToken.None);

        // Chỉ cứu đúng bài follow-up bị treo trên ca chưa khóa, giữ nguyên IsFollowUp = true & ParentAnswerId = mainSubId
        enqueuedTasks.Should().ContainSingle();
        enqueuedTasks[0].InClassSubmissionId.Should().Be(stuckFollowUpSubId);
        enqueuedTasks[0].IsFollowUp.Should().BeTrue();
        enqueuedTasks[0].ParentAnswerId.Should().Be(mainSubId);

        using (var assertScope = provider.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var auditedSub = await db.InClassExamSubmissions.AsNoTracking().FirstAsync(s => s.Id == auditedSubId);
            auditedSub.Status.Should().Be("AUDITED");
            auditedSub.FinalScore.Should().Be(8.5m);

            var resolvedDlq = await db.DeadLetterQueues.AsNoTracking().FirstAsync(d => d.Id == dlqAuditedId);
            resolvedDlq.Status.Should().Be("resolved");
        }
    }

    [Fact(DisplayName = "M3-Item8 MF-05: Replay/Retry khi đã có AiEvaluation cập nhật tại chỗ (idempotent) và thay thế Details sạch sẽ")]
    public async Task GradingQueueWorker_Mf05DlqReplay_UpdatesExistingAiEvaluationIdempotently()
    {
        var dbName = Guid.NewGuid().ToString();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-idem@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-idem@fpt.edu.vn", FullName = "SV", StudentCode = "SE180001", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1821", Name = "PRN231 - SE1821", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Tiêu chí 1", Description = "Mô tả", MaxScore = 10m, Weight = 1.0m, OrderIndex = 1 };
        var question = BuildApprovedQuestion(course.Id, null, rubric.Id, "medium", "Giải thích Middleware");

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Idempotent",
            TotpSecretKey = "SECRET_IDEM",
            Status = "IN_PROGRESS",
            TotalQuestionsPerStudent = 1,
            ExamDurationMinutes = 20,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30)
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { question.Id }),
            Status = "SUBMITTED",
            JoinedAt = DateTime.UtcNow.AddMinutes(-3)
        };
        var mainSubmission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Middleware xử lý pipeline HTTP request và response.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        var existingEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "in_class",
            StudentId = student.Id,
            InClassSubmissionId = mainSubmission.Id,
            TotalScore = 5.0m,
            ConfidenceScore = 0.70m,
            Feedback = "Old in-class feedback",
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-5),
            Details = new List<AiEvaluationDetail>
            {
                new AiEvaluationDetail
                {
                    Id = Guid.NewGuid(),
                    CriterionId = criterion.Id,
                    Score = 5.0m,
                    Comment = "Old in-class detail"
                }
            }
        };

        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.0m,
                ConfidenceScore = 0.94m,
                Feedback = "Replayed feedback",
                CotTrace = "Replayed trace",
                Details = new List<GradingDetailDto>
                {
                    new GradingDetailDto { CriterionId = criterion.Id.ToString(), Score = 8.0m, Comment = "Replayed detail" }
                }
            });
        services.AddSingleton(aiMock.Object);

        using var provider = services.BuildServiceProvider();
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.AddRange(lecturer, student);
            db.Courses.Add(course);
            db.Classes.Add(cls);
            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(criterion);
            db.PracticeQuestions.Add(question);
            db.InClassExamShifts.Add(shift);
            db.InClassShiftParticipants.Add(participant);
            db.InClassExamSubmissions.Add(mainSubmission);
            db.AiEvaluations.Add(existingEval);
            await db.SaveChangesAsync();
        }

        var inClassHubMock = new Mock<IHubContext<InClassShiftHub, IInClassShiftClient>>();
        var hubClientsMock = new Mock<IHubClients<IInClassShiftClient>>();
        hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(new Mock<IInClassShiftClient>().Object);
        inClassHubMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

        var singleTaskChannel = new Mock<IGradingQueueChannel>();
        singleTaskChannel.Setup(c => c.ReadAllAsync(It.IsAny<CancellationToken>()))
            .Returns(YieldSingleTaskAsync(new GradingTask
            {
                AnswerId = mainSubmission.Id,
                InClassSubmissionId = mainSubmission.Id,
                SessionId = shift.Id,
                StudentId = student.Id,
                QuestionId = question.Id,
                AnswerText = mainSubmission.Transcript
            }));

        var worker = new GradingQueueWorker(
            singleTaskChannel.Object,
            provider,
            new Mock<IHubContext<PracticeHub, IPracticeClient>>().Object,
            NullLogger<GradingQueueWorker>.Instance,
            configuration: null,
            inClassHubContext: inClassHubMock.Object);

        await worker.StartAsync(CancellationToken.None);
        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask;
        }
        await worker.StopAsync(CancellationToken.None);

        using var checkScope = provider.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var evals = await checkDb.AiEvaluations
            .AsNoTracking()
            .Where(e => e.InClassSubmissionId == mainSubmission.Id)
            .ToListAsync();
        evals.Should().ContainSingle();
        evals[0].Id.Should().Be(existingEval.Id);
        evals[0].TotalScore.Should().Be(8.0m);
        evals[0].Feedback.Should().Be("Replayed feedback");

        var details = await checkDb.AiEvaluationDetails
            .AsNoTracking()
            .Where(d => d.EvaluationId == existingEval.Id)
            .ToListAsync();
        details.Should().ContainSingle();
        details[0].Score.Should().Be(8.0m);
        details[0].Comment.Should().Be("Replayed detail");

        var gradedSub = await checkDb.InClassExamSubmissions.AsNoTracking().FirstAsync(s => s.Id == mainSubmission.Id);
        gradedSub.Status.Should().Be("AI_GRADED");
    }

    [Fact(DisplayName = "M3-Item8 MF-05: Khi Polly hết retry -> ghi DLQ InClassGrading và phát ReceiveGradingError cho Staff + Student")]
    public async Task GradingQueueWorker_Mf05PollyExhausted_PersistsDlqAndDispatchesReceiveGradingErrorToStaffAndStudent()
    {
        var dbName = Guid.NewGuid().ToString();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-err@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-err@fpt.edu.vn", FullName = "SV", StudentCode = "SE180002", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1822", Name = "PRN231 - SE1822", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Tiêu chí 1", Description = "Mô tả", MaxScore = 10m, Weight = 1.0m, OrderIndex = 1 };
        var question = BuildApprovedQuestion(course.Id, null, rubric.Id, "easy", "Giải thích REST");

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Error SignalR",
            TotpSecretKey = "SECRET_ERR",
            Status = "IN_PROGRESS",
            TotalQuestionsPerStudent = 1,
            ExamDurationMinutes = 20,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30)
        };
        var mainSubmission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "REST là kiến trúc phi trạng thái dùng HTTP.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("Gemini 503"));
        services.AddSingleton(aiMock.Object);

        using var provider = services.BuildServiceProvider();
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.AddRange(lecturer, student);
            db.Courses.Add(course);
            db.Classes.Add(cls);
            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(criterion);
            db.PracticeQuestions.Add(question);
            db.InClassExamShifts.Add(shift);
            db.InClassExamSubmissions.Add(mainSubmission);
            await db.SaveChangesAsync();
        }

        var staffClientMock = new Mock<IInClassShiftClient>();
        staffClientMock.Setup(c => c.ReceiveGradingError(It.IsAny<Guid>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var studentClientMock = new Mock<IInClassShiftClient>();
        studentClientMock.Setup(c => c.ReceiveGradingError(It.IsAny<Guid>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var hubClientsMock = new Mock<IHubClients<IInClassShiftClient>>();
        hubClientsMock.Setup(c => c.Group(InClassShiftHub.StaffGroupName(shift.Id))).Returns(staffClientMock.Object);
        hubClientsMock.Setup(c => c.Group(InClassShiftHub.StudentGroupName(shift.Id, student.Id))).Returns(studentClientMock.Object);

        var inClassHubMock = new Mock<IHubContext<InClassShiftHub, IInClassShiftClient>>();
        inClassHubMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

        var fastRetryConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GradingQueue:RetryDelayMilliseconds"] = "1" })
            .Build();

        var singleTaskChannel = new Mock<IGradingQueueChannel>();
        singleTaskChannel.Setup(c => c.ReadAllAsync(It.IsAny<CancellationToken>()))
            .Returns(YieldSingleTaskAsync(new GradingTask
            {
                AnswerId = mainSubmission.Id,
                InClassSubmissionId = mainSubmission.Id,
                SessionId = shift.Id,
                StudentId = student.Id,
                QuestionId = question.Id,
                AnswerText = mainSubmission.Transcript
            }));

        var worker = new GradingQueueWorker(
            singleTaskChannel.Object,
            provider,
            new Mock<IHubContext<PracticeHub, IPracticeClient>>().Object,
            NullLogger<GradingQueueWorker>.Instance,
            configuration: fastRetryConfig,
            inClassHubContext: inClassHubMock.Object);

        await worker.StartAsync(CancellationToken.None);
        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask;
        }
        await worker.StopAsync(CancellationToken.None);

        aiMock.Verify(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(4));

        using var checkScope = provider.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var dlq = await checkDb.DeadLetterQueues.AsNoTracking()
            .SingleAsync(d => d.TaskType == "InClassGrading" && d.CorrelationId == mainSubmission.Id);
        dlq.Status.Should().Be("pending");
        dlq.FlowType.Should().Be("IN_CLASS_EXAM");
        dlq.ManagedByRole.Should().Be("lecturer");

        staffClientMock.Verify(c => c.ReceiveGradingError(mainSubmission.Id, It.IsAny<string>()), Times.Once);
        studentClientMock.Verify(c => c.ReceiveGradingError(mainSubmission.Id, It.IsAny<string>()), Times.Once);
    }

    private static async IAsyncEnumerable<GradingTask> YieldSingleTaskAsync(
        GradingTask task,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        yield return task;
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
