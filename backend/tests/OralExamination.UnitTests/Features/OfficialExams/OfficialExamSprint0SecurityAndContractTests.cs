using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
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
using OralExamination.Application.Features.Appeals.Queries.GetAppealEvidence;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftExcel;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamShifts;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamTicketState;
using OralExamination.Application.Features.Proctor.Commands.RecordProctorViolation;
using OralExamination.Application.Features.Proctor.Queries.GetShiftAttendance;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Reporting;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamSprint0SecurityAndContractTests : IDisposable
{
    private const string SigningKey = "Sprint0OfficialExamSecurityRbacBolaSigningKeyMinimum32Bytes!!";
    private const string Issuer = "Sprint0SecurityIssuer";
    private const string Audience = "Sprint0SecurityAudience";

    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly string _dbName = Guid.NewGuid().ToString();

    private readonly User _student1;
    private readonly User _student2;
    private readonly User _assignedExaminer;
    private readonly User _otherLecturer;
    private readonly User _assignedProctor;
    private readonly User _otherProctor;
    private readonly User _owningDeptHead;
    private readonly User _otherDeptHead;
    private readonly User _officer;
    private readonly User _admin;

    private readonly Course _course;
    private readonly OfficialExamSession _session;
    private readonly RealExamSessionShift _shift;
    private readonly ExamSet _handedOverSet;
    private readonly ExamQuestion _question;
    private readonly StudentExamTicket _ticket1;
    private readonly StudentExamTicket _ticket2;
    private readonly AppealRequest _appeal1;

    public OfficialExamSprint0SecurityAndContractTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = SigningKey,
            ["Authentication:JwtIssuer"] = Issuer,
            ["Authentication:JwtAudience"] = Audience,
            ["Authentication:AccessTokenMinutes"] = "60"
        }).Build();

        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(_dbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
                services.AddScoped<IExamGradeReportExporter, ExamGradeReportExporter>();
                services.AddApplication();
                services.AddOralExamAuthentication(ctx.Configuration);
                services.AddControllers().AddApplicationPart(typeof(OfficialExamsController).Assembly);
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        _server = new TestServer(builder);
        _client = _server.CreateClient();

        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();

        _student1 = CreateUser(UserRole.Student, "sv1@sprint0.test", "SE180101");
        _student2 = CreateUser(UserRole.Student, "sv2@sprint0.test", "SE180102");
        _assignedExaminer = CreateUser(UserRole.Lecturer, "examiner@sprint0.test");
        _otherLecturer = CreateUser(UserRole.Lecturer, "other.lecturer@sprint0.test");
        _assignedProctor = CreateUser(UserRole.Proctor, "proctor@sprint0.test");
        _otherProctor = CreateUser(UserRole.Proctor, "other.proctor@sprint0.test");
        _owningDeptHead = CreateUser(UserRole.DepartmentHead, "dh.owner@sprint0.test");
        _otherDeptHead = CreateUser(UserRole.DepartmentHead, "dh.other@sprint0.test");
        _officer = CreateUser(UserRole.ExaminationOfficer, "eo@sprint0.test");
        _admin = CreateUser(UserRole.Admin, "admin@sprint0.test");

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var program = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "SE",
            Name = "Software Engineering",
            DepartmentHeadUserId = _owningDeptHead.Id,
            IsActive = true
        };
        _course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Backend .NET",
            SemesterId = semester.Id,
            AcademicProgramId = program.Id,
            DepartmentHeadUserId = _owningDeptHead.Id,
            IsActive = true
        };
        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            CreatedBy = _owningDeptHead.Id,
            Name = "Structure Sprint 0",
            TotalQuestions = 1,
            DurationMinutes = 30,
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            Name = "Rubric 10",
            TotalMaxScore = 10m,
            IsActive = true
        };
        _question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            RubricId = rubric.Id,
            Title = "Dependency Injection",
            Content = "Explain DI lifetimes in ASP.NET Core.",
            SampleAnswer = "Scoped, Transient, Singleton lifetimes control service instantiation.",
            Difficulty = "medium",
            BloomLevel = "Understand",
            Source = "manual",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        _handedOverSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            StructureId = structure.Id,
            SetCode = "SET-SPRINT0-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        var draftSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            StructureId = structure.Id,
            SetCode = "SET-SPRINT0-DRAFT",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };
        var setQuestion = new ExamSetQuestion
        {
            Id = Guid.NewGuid(),
            ExamSetId = _handedOverSet.Id,
            ExamQuestionId = _question.Id,
            OrderIndex = 1
        };

        _session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            SemesterId = semester.Id,
            ExamStructureId = structure.Id,
            Title = "Sprint 0 Official Exam",
            ExamAttemptType = OfficialExamAttemptType.FinalExam,
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "in_progress",
            CreatedBy = _officer.Id
        };
        var now = DateTime.UtcNow;
        _shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = _session.Id,
            ShiftName = "Ca Sprint 0",
            RoomLab = "Lab 501",
            MaxSeats = 24,
            StartTime = now.AddMinutes(-20),
            EndTime = now.AddMinutes(40),
            ProctorUserId = _assignedProctor.Id,
            ExaminerUserId = _assignedExaminer.Id,
            IsExamPublished = true,
            PublishedAt = now.AddMinutes(-20),
            Status = "in_progress"
        };

        _ticket1 = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = _shift.Id,
            StudentId = _student1.Id,
            ExamSetId = _handedOverSet.Id,
            SeatNumber = 1,
            IpAddress = "10.24.0.10",
            DeviceId = "dev-byod-student1-123456",
            DeviceLockedAt = now.AddMinutes(-15),
            DeviceUnlockCount = 1,
            Status = ExamTicketStatus.InProgress,
            IsLocked = false
        };
        _ticket2 = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = _shift.Id,
            StudentId = _student2.Id,
            ExamSetId = _handedOverSet.Id,
            SeatNumber = 2,
            IpAddress = "10.24.0.11",
            Status = ExamTicketStatus.Scheduled,
            IsLocked = false
        };
        var sub1 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket1.Id,
            ExamQuestionId = _question.Id,
            AudioR2Url = "https://r2.test/01_SE180101.webm",
            AudioHashSha256 = new string('a', 64),
            TranscriptWhisper = "Student 1 transcript",
            AiScore = 7.5m,
            FinalScore = 7.5m,
            GradingStatus = "ai_graded"
        };
        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket2.Id,
            ExamQuestionId = _question.Id,
            AudioR2Url = "https://r2.test/02_SE180102.webm",
            AudioHashSha256 = new string('b', 64),
            TranscriptWhisper = "Student 2 secret transcript",
            AiScore = 5.0m,
            FinalScore = 5.0m,
            GradingStatus = "ai_graded"
        };
        _appeal1 = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = _ticket1.Id,
            StudentId = _student1.Id,
            SessionId = _session.Id,
            SubmissionId = sub1.Id,
            Reason = "Đề nghị chấm lại câu 1",
            Status = AppealStatus.AssignedToLecturer,
            AssignedTo = _otherLecturer.Id,
            OriginalScore = 7.5m
        };

        db.Users.AddRange(
            _student1, _student2, _assignedExaminer, _otherLecturer,
            _assignedProctor, _otherProctor, _owningDeptHead, _otherDeptHead,
            _officer, _admin);
        db.Semesters.Add(semester);
        db.AcademicPrograms.Add(program);
        db.Courses.Add(_course);
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(_question);
        db.ExamSets.AddRange(_handedOverSet, draftSet);
        db.ExamSetQuestions.Add(setQuestion);
        db.OfficialExamSessions.Add(_session);
        db.RealExamSessionShifts.Add(_shift);
        db.StudentExamTickets.AddRange(_ticket1, _ticket2);
        db.ExamQuestionSubmissions.AddRange(sub1, sub2);
        db.AppealRequests.Add(_appeal1);
        db.SaveChanges();
    }

    private static User CreateUser(string role, string email, string? studentCode = null) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        FullName = $"User {role}",
        StudentCode = studentCode,
        Role = role,
        IsActive = true,
        AuthVersion = 1
    };

    private string CreateJwt(User user)
    {
        var token = new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
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
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256Signature)
        });
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string method,
        string route,
        User? actor,
        object? body = null,
        string? forwardedIp = null,
        string? deviceId = null,
        string? kioskSessionToken = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        else if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        if (actor is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(actor));
        }

        if (!string.IsNullOrWhiteSpace(forwardedIp))
        {
            request.Headers.Add("X-Forwarded-For", forwardedIp);
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            request.Headers.Add("X-Device-Id", deviceId);
        }

        if (!string.IsNullOrWhiteSpace(kioskSessionToken))
        {
            request.Headers.Add("X-Kiosk-Session-Token", kioskSessionToken);
        }

        return await _client.SendAsync(request);
    }

    [Fact(DisplayName = "S0-RBAC-01: OfficialExamsController có class-level [Authorize] không gắn Roles và mọi public action có [Authorize(Roles=...)] tường minh; SubmitExamSetReview cấm examination_officer")]
    public void OfficialExamsController_ClassAuthorize_And_ExplicitActionRoles()
    {
        var classAuth = typeof(OfficialExamsController).GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        classAuth.Should().NotBeNull();
        classAuth!.Roles.Should().BeNull("class-level [Authorize] không được gắn Roles vì ASP.NET Core áp dụng phép AND sẽ chặn student 403");

        var actions = typeof(OfficialExamsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToList();

        actions.Should().NotBeEmpty();
        foreach (var action in actions)
        {
            var actionAuth = action.GetCustomAttribute<AuthorizeAttribute>(inherit: false);
            actionAuth.Should().NotBeNull($"Action {action.Name} bắt buộc phải có [Authorize(Roles=...)] tường minh");
            actionAuth!.Roles.Should().NotBeNullOrWhiteSpace($"Action {action.Name} bắt buộc phải liệt kê Roles tối thiểu");
            actionAuth.Roles!.Split(',', StringSplitOptions.TrimEntries)
                .Should().NotContain(UserRole.Admin, $"Action {action.Name} tuyệt đối không cho phép Admin");
        }

        var submitMethod = typeof(OfficialExamsController).GetMethod(nameof(OfficialExamsController.SubmitExamSetReview))!;
        var submitRoles = submitMethod.GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',', StringSplitOptions.TrimEntries);
        submitRoles.Should().BeEquivalentTo(new[] { UserRole.Lecturer, UserRole.DepartmentHead });
        submitRoles.Should().NotContain(UserRole.ExaminationOfficer);
    }

    [Fact(DisplayName = "S0-RBAC-02: Student qua HTTP pipeline thực tế không bị 403 class-level trên tickets/current, tickets/{id}, check-in, anti-cheat/log nhưng bị 403 trên EO/Proctor/Examiner actions")]
    public async Task StudentHttpPipeline_SucceedsOnStudentRoutes_AndIsForbiddenOnStaffRoutes()
    {
        // 1. GET tickets/current -> 200 OK (not 403!)
        using (var currentResp = await SendAsync("GET", "/api/v1/official-exams/tickets/current", _student1, forwardedIp: "10.24.0.10", deviceId: "dev-byod-student1-123456"))
        {
            currentResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 2. GET tickets/{ticketId} -> 200 OK recovery state
        using (var stateResp = await SendAsync("GET", $"/api/v1/official-exams/tickets/{_ticket1.Id}", _student1, deviceId: "dev-byod-student1-123456"))
        {
            stateResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = JsonDocument.Parse(await stateResp.Content.ReadAsStringAsync()).RootElement;
            json.GetProperty("ticketId").GetGuid().Should().Be(_ticket1.Id);
            json.GetProperty("shiftId").GetGuid().Should().Be(_shift.Id);
            json.GetProperty("status").GetString().Should().Be(ExamTicketStatus.InProgress);
            json.GetProperty("isExamPublished").GetBoolean().Should().BeTrue();
            json.GetProperty("isKioskLocked").GetBoolean().Should().BeFalse();
            json.GetProperty("violationCount").GetInt32().Should().Be(0);
            json.GetProperty("maxViolations").GetInt32().Should().Be(3);
            json.GetProperty("lockThreshold").GetInt32().Should().Be(4);
            json.GetProperty("remainingStrikes").GetInt32().Should().Be(4);
            json.GetProperty("deviceId").GetString().Should().Be("dev-byod-student1-123456");
            json.GetProperty("deviceUnlockCount").GetInt32().Should().Be(1);
            json.TryGetProperty("serverNowUtc", out _).Should().BeTrue();
            json.TryGetProperty("deviceLockedAt", out _).Should().BeTrue();
        }

        // 3. POST tickets/{ticket2}/check-in by student2 -> 200 OK (not 403!)
        using (var checkInResp = await SendAsync("POST", $"/api/v1/official-exams/tickets/{_ticket2.Id}/check-in", _student2, forwardedIp: "10.24.0.11", deviceId: "dev-byod-student2-123456"))
        {
            checkInResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 4. POST anti-cheat/log by student1 for own ticket -> 200 OK (not 403!)
        using (var logResp = await SendAsync("POST", "/api/v1/official-exams/anti-cheat/log", _student1, new
        {
            ticketId = _ticket1.Id,
            shiftId = _shift.Id,
            studentId = _student1.Id,
            eventType = AntiCheatEventType.WindowBlur,
            severity = AntiCheatSeverity.Medium,
            details = "Window blur 1",
            clientEventId = "client-evt-http-001"
        }, deviceId: "dev-byod-student1-123456"))
        {
            logResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 5. Student blocked 403 on EO/Examiner/Proctor routes & other student's ticket state
        var blockedRoutes = new (string Method, string Route)[]
        {
            ("GET", $"/api/v1/official-exams/tickets/{_ticket2.Id}"),
            ("POST", $"/api/v1/official-exams/tickets/{_ticket1.Id}/unlock-device"),
            ("GET", $"/api/v1/official-exams/shifts/{_shift.Id}/audit-evidence"),
            ("POST", $"/api/v1/official-exams/shifts/{_shift.Id}/publish-grades"),
            ("PUT", $"/api/v1/official-exams/tickets/{_ticket1.Id}/override-score"),
            ("POST", $"/api/v1/official-exams/tickets/{_ticket1.Id}/lock"),
            ("GET", $"/api/v1/official-exams/shifts/{_shift.Id}/export-excel"),
            ("GET", $"/api/v1/official-exams/shifts/{_shift.Id}/export-pdf"),
            ("GET", $"/api/v1/appeals/{_appeal1.Id}/evidence")
        };

        foreach (var (method, route) in blockedRoutes)
        {
            using var resp = await SendAsync(method, route, _student1);
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"Student phải bị chặn 403 trên {method} {route}");
        }

        // 6. EO blocked 403 on SubmitExamSetReview
        using (var eoSubmitResp = await SendAsync("POST", $"/api/v1/official-exams/exam-sets/{_handedOverSet.Id}/submit-review", _officer))
        {
            eoSubmitResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact(DisplayName = "S0-BOLA-01: GetAuditEvidence, OverrideScore, LockTicket, ExportExcel/Pdf khóa chặt quyền sở hữu (HTTP 403, KHÔNG trả 404)")]
    public async Task AuditOverrideLockAndExport_EnforceShiftExaminerAndCourseOwnershipBola()
    {
        // Unassigned lecturer & non-owning DH -> 403 on GetAuditEvidence (NOT 404!)
        foreach (var unauthorized in new[] { _otherLecturer, _otherDeptHead })
        {
            using var evResp = await SendAsync("GET", $"/api/v1/official-exams/shifts/{_shift.Id}/audit-evidence", unauthorized);
            evResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            using var ovResp = await SendAsync("PUT", $"/api/v1/official-exams/tickets/{_ticket1.Id}/override-score", unauthorized, new
            {
                auditedScore = 9.0m,
                overrideReason = "Cố tình sửa điểm của ca thi không được phân công"
            });
            ovResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            using var lockResp = await SendAsync("POST", $"/api/v1/official-exams/tickets/{_ticket1.Id}/lock", unauthorized);
            lockResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            using var xlResp = await SendAsync("GET", $"/api/v1/official-exams/shifts/{_shift.Id}/export-excel", unauthorized);
            xlResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            using var pdfResp = await SendAsync("GET", $"/api/v1/official-exams/shifts/{_shift.Id}/export-pdf", unauthorized);
            pdfResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Assigned Examiner, Owning DH, ExaminationOfficer -> 200 OK on GetAuditEvidence
        foreach (var authorized in new[] { _assignedExaminer, _owningDeptHead, _officer })
        {
            using var evResp = await SendAsync("GET", $"/api/v1/official-exams/shifts/{_shift.Id}/audit-evidence", authorized);
            evResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact(DisplayName = "S0-BOLA-02: GET /api/v1/appeals/{appealId}/evidence & alias /lecturer/appeals/{appealId}/evidence chỉ trả đúng 1 vé thi cho GV được phân công hoặc EO")]
    public async Task GetAppealEvidence_ReturnsSingleTicketForAssignedLecturerOrOfficer_AndForbidsOthers()
    {
        // Assigned lecturer (_otherLecturer) -> 200 OK on both canonical and lecturer alias routes
        foreach (var route in new[]
        {
            $"/api/v1/appeals/{_appeal1.Id}/evidence",
            $"/api/v1/lecturer/appeals/{_appeal1.Id}/evidence"
        })
        {
            using var resp = await SendAsync("GET", route, _otherLecturer);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().Contain("SE180101");
            body.Should().NotContain("SE180102", "tuyệt đối không được rò rỉ dữ liệu thí sinh khác trong cùng ca thi");
            body.Should().NotContain("Student 2 secret transcript");
        }

        // Examination Officer -> 200 OK
        using (var eoResp = await SendAsync("GET", $"/api/v1/appeals/{_appeal1.Id}/evidence", _officer))
        {
            eoResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Unassigned lecturer (initial examiner), DH, proctor, student, admin -> 403 Forbidden
        foreach (var forbidden in new[] { _assignedExaminer, _owningDeptHead, _assignedProctor, _student1, _admin })
        {
            using var resp = await SendAsync("GET", $"/api/v1/appeals/{_appeal1.Id}/evidence", forbidden);
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"Role {forbidden.Role} không được phép xem bằng chứng phúc khảo chưa được gán");
        }
    }

    [Fact(DisplayName = "S0-ANTILEAK-01: GetMyWorkstationTicket chỉ trả Questions khi Shift=in_progress && IsExamPublished=true && Ticket=IN_PROGRESS")]
    public async Task GetMyWorkstationTicket_NeverLeaksQuestionsBeforePublishOrCheckInOrAfterSubmit()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OralExamDbContext(options);

        var student = CreateUser(UserRole.Student, "antileak@test.vn", "SE180200");
        var course = new Course { Id = Guid.NewGuid(), Code = "SWP391", Name = "Capstone", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct", IsActive = true };
        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Secret Exam Question",
            Content = "Top secret content",
            Difficulty = "hard",
            BloomLevel = "Analyze",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var set = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SECRET-SET-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            Title = "Anti-Leak Session",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "in_progress"
        };
        var now = DateTime.UtcNow;
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca Anti-Leak",
            RoomLab = "Lab 601",
            StartTime = now.AddMinutes(-10),
            EndTime = now.AddMinutes(50),
            Status = ExamShiftWindow.Scheduled,
            IsExamPublished = false
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = set.Id,
            SeatNumber = 1,
            IpAddress = "10.0.0.1",
            Status = ExamTicketStatus.Scheduled
        };
        db.Users.Add(student);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.ExamQuestions.Add(question);
        db.ExamSets.Add(set);
        db.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = set.Id, ExamQuestionId = question.Id, OrderIndex = 1 });
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var handler = new GetMyWorkstationTicketQueryHandler(db, new GetMyWorkstationTicketQueryValidator());

        // Case 1: Shift scheduled, unpublished, ticket scheduled -> Questions MUST be null
        var r1 = await handler.Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", now), CancellationToken.None);
        r1.IsSuccess.Should().BeTrue();
        r1.Value.Questions.Should().BeNull();
        r1.Value.ExamSetCode.Should().BeNull();
        r1.Value.IsExamPublished.Should().BeFalse();

        // Case 2: Shift in_progress, ticket IN_PROGRESS, but IsExamPublished == false -> Questions MUST be null
        shift.Status = ExamShiftWindow.InProgress;
        ticket.Status = ExamTicketStatus.InProgress;
        await db.SaveChangesAsync();
        var r2 = await handler.Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", now), CancellationToken.None);
        r2.IsSuccess.Should().BeTrue();
        r2.Value.Questions.Should().BeNull();

        // Case 3: Shift in_progress, IsExamPublished == true, but ticket SCHEDULED -> Questions MUST be null
        shift.IsExamPublished = true;
        ticket.Status = ExamTicketStatus.Scheduled;
        await db.SaveChangesAsync();
        var r3 = await handler.Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", now), CancellationToken.None);
        r3.IsSuccess.Should().BeTrue();
        r3.Value.Questions.Should().BeNull();

        // Case 4: ALL 3 true -> Questions exposed!
        ticket.Status = ExamTicketStatus.InProgress;
        await db.SaveChangesAsync();
        var r4 = await handler.Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", now), CancellationToken.None);
        r4.IsSuccess.Should().BeTrue();
        r4.Value.Questions.Should().NotBeNull().And.ContainSingle(q => q.QuestionId == question.Id);
        r4.Value.ExamSetCode.Should().Be("SECRET-SET-01");

        // Case 5: Ticket SUBMITTED -> Questions MUST be null & IsKioskLocked == true
        ticket.Status = ExamTicketStatus.Submitted;
        await db.SaveChangesAsync();
        var r5 = await handler.Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", now), CancellationToken.None);
        r5.IsSuccess.Should().BeTrue();
        r5.Value.Questions.Should().BeNull();
        r5.Value.IsKioskLocked.Should().BeTrue();
    }

    [Fact(DisplayName = "S0-NO-AUTOSET-01: AssignExamTicket thất bại fail-closed 422 UNPROCESSABLE và KHÔNG tự tạo AUTO-SET-* khi thiếu bộ đề HANDED_OVER")]
    public async Task AssignExamTicket_WithoutHandedOverExamSet_FailsClosed422_WithoutCreatingAutoSet()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OralExamDbContext(options);

        var officer = CreateUser(UserRole.ExaminationOfficer, "eo.noautoset@test.vn");
        var student = CreateUser(UserRole.Student, "sv.noautoset@test.vn", "SE180300");
        var course = new Course { Id = Guid.NewGuid(), Code = "PRJ301", Name = "Java Web", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct", IsActive = true };
        var approvedQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Approved Q",
            Content = "Content",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        // Only OFFICIAL (not HANDED_OVER) and sample HANDED_OVER sets exist -> must NOT be picked!
        var officialOnlySet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "OFF-01", Status = ExamSetStatus.Official, IsActive = true };
        var sampleSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "SAMPLE-01", Status = ExamSetStatus.HandedOver, IsSampleSet = true, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = structure.Id, Title = "Session", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", StartTime = DateTime.UtcNow.AddHours(1), EndTime = DateTime.UtcNow.AddHours(2), MaxSeats = 24 };

        db.Users.AddRange(officer, student);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.ExamQuestions.Add(approvedQuestion);
        db.ExamSets.AddRange(officialOnlySet, sampleSet);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        await db.SaveChangesAsync();

        var handler = new AssignExamTicketCommandHandler(db, new AssignExamTicketCommandValidator());
        var result = await handler.Handle(new AssignExamTicketCommand(officer.Id, shift.Id, student.Id, 1, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("UNPROCESSABLE:");
        (await db.ExamSets.AnyAsync(es => es.SetCode.StartsWith("AUTO-SET"))).Should().BeFalse();
        (await db.StudentExamTickets.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "S0-PROCTOR-01: GetShiftAttendance trả đầy đủ deviceId, deviceLockedAt, deviceUnlockCount, violationCount gom nhóm và RecordProctorViolation gán ClientEventId")]
    public async Task ProctorAttendanceAndViolation_ReturnsByodFieldsGroupedViolationCount_AndClientEventId()
    {
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();

        var recordHandler = new RecordProctorViolationCommandHandler(db);
        var rec1 = await recordHandler.Handle(new RecordProctorViolationCommand(
            _assignedProctor.Id, _shift.Id, _ticket1.Id, _student1.Id,
            AntiCheatEventType.WindowBlur, AntiCheatSeverity.Medium, "Blur 1"), CancellationToken.None);
        var rec2 = await recordHandler.Handle(new RecordProctorViolationCommand(
            _assignedProctor.Id, _shift.Id, _ticket1.Id, _student1.Id,
            AntiCheatEventType.TabSwitch, AntiCheatSeverity.High, "Tab switch"), CancellationToken.None);

        rec1.IsSuccess.Should().BeTrue();
        rec1.Value.ClientEventId.Should().Be(rec1.Value.Id.ToString("D"));
        rec2.IsSuccess.Should().BeTrue();
        rec2.Value.ClientEventId.Should().Be(rec2.Value.Id.ToString("D"));

        var attendanceHandler = new GetShiftAttendanceQueryHandler(db);
        var attendance = await attendanceHandler.Handle(new GetShiftAttendanceQuery(_shift.Id, _assignedProctor.Id), CancellationToken.None);

        attendance.IsSuccess.Should().BeTrue();
        var seat1 = attendance.Value.Seats.Single(s => s.SeatNumber == 1);
        seat1.DeviceId.Should().Be("dev-byod-student1-123456");
        seat1.DeviceLockedAt.Should().NotBeNull();
        seat1.DeviceUnlockCount.Should().Be(1);
        seat1.ViolationCount.Should().Be(2);

        var seat2 = attendance.Value.Seats.Single(s => s.SeatNumber == 2);
        seat2.DeviceId.Should().BeNull();
        seat2.ViolationCount.Should().Be(0);
    }

    [Fact(DisplayName = "S0-DB-M1M2-01: EF Core Model khớp 100% M1+M2 (OfficialExamCandidate, FE/RE, BYOD partial unique, Follow-up turn unique, bỏ uq_ticket_question, ShiftMutationGuard BYOD)")]
    public async Task DbContextModel_And_ShiftMutationGuard_EnforceM1AndM2Invariants()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OralExamDbContext(options);

        // 1. OfficialExamCandidate entity & unique index
        var candidateEntity = db.Model.FindEntityType(typeof(OfficialExamCandidate))!;
        candidateEntity.Should().NotBeNull();
        candidateEntity.GetTableName().Should().Be("official_exam_candidates");
        candidateEntity.GetIndexes().Should().Contain(i => i.GetDatabaseName() == "uq_official_exam_candidates_session_student" && i.IsUnique);

        // 2. OfficialExamSession AttemptType & M4 non-null SemesterId + unfiltered unique index
        var sessionEntity = db.Model.FindEntityType(typeof(OfficialExamSession))!;
        sessionEntity.FindProperty(nameof(OfficialExamSession.SemesterId))!.IsNullable.Should().BeFalse("M4 Contract bắt buộc semester_id NOT NULL");
        sessionEntity.FindProperty(nameof(OfficialExamSession.ExamAttemptType))!.GetDefaultValue().Should().Be(OfficialExamAttemptType.FinalExam);
        sessionEntity.FindProperty(nameof(OfficialExamSession.MaxFollowUpQuestions))!.GetDefaultValue().Should().Be(2);
        sessionEntity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "uq_official_exam_sessions_semester_course_attempt"
            && i.IsUnique
            && i.GetFilter() == null);

        // 3. StudentExamTicket BYOD partial unique index & hashed session token column
        var ticketEntity = db.Model.FindEntityType(typeof(StudentExamTicket))!;
        ticketEntity.FindProperty(nameof(StudentExamTicket.DeviceSessionTokenHash))!.GetColumnName().Should().Be("device_session_token_hash");
        ticketEntity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "uq_tickets_shift_device"
            && i.IsUnique
            && i.GetFilter() == "device_id IS NOT NULL");

        // 4. ExamQuestionSubmission follow-up indexes & legacy uq_ticket_question removed
        var subEntity = db.Model.FindEntityType(typeof(ExamQuestionSubmission))!;
        subEntity.GetIndexes().Should().NotContain(i => i.GetDatabaseName() == "uq_ticket_question");
        subEntity.GetIndexes().Should().Contain(i => i.GetDatabaseName() == "uq_exam_submissions_ticket_question_turn" && i.IsUnique);
        subEntity.GetIndexes().Should().Contain(i => i.GetDatabaseName() == "uq_exam_submissions_ticket_parent" && i.IsUnique);

        // 5. RealExamSessionShift certificate index & ExamAntiCheatLog client_event_id unique index
        var shiftEntity = db.Model.FindEntityType(typeof(RealExamSessionShift))!;
        shiftEntity.GetIndexes().Should().Contain(i => i.GetDatabaseName() == "ix_real_exam_shifts_certificate_hash");
        var antiCheatEntity = db.Model.FindEntityType(typeof(ExamAntiCheatLog))!;
        antiCheatEntity.GetIndexes().Should().Contain(i => i.GetDatabaseName() == "uq_exam_anti_cheat_logs_ticket_client_event" && i.IsUnique);

        // 6. ShiftMutationGuard (qua UpdateExamShift) coi DeviceId đã bind là bằng chứng vé đã sử dụng
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo.guard@test.vn");
        var proctor = CreateUser(UserRole.Proctor, "proctor.guard@test.vn");
        var examiner = CreateUser(UserRole.Lecturer, "examiner.guard@test.vn");
        var futureShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Future Shift",
            RoomLab = "Lab 99",
            StartTime = DateTime.UtcNow.AddHours(2),
            EndTime = DateTime.UtcNow.AddHours(4),
            ProctorUserId = proctor.Id,
            ExaminerUserId = examiner.Id,
            Status = "scheduled"
        };
        var boundTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = futureShift.Id,
            StudentId = Guid.NewGuid(),
            SeatNumber = 1,
            Status = ExamTicketStatus.Scheduled,
            DeviceId = "bound-laptop-001"
        };
        db.Users.AddRange(officer, proctor, examiner);
        db.RealExamSessionShifts.Add(futureShift);
        db.StudentExamTickets.Add(boundTicket);
        await db.SaveChangesAsync();

        var updateResult = await new OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift.UpdateExamShiftCommandHandler(db)
            .Handle(new OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift.UpdateExamShiftCommand(
                officer.Id,
                UserRole.ExaminationOfficer,
                futureShift.Id,
                "Future Shift Renamed",
                "Lab 99",
                DateTime.UtcNow.AddHours(3),
                DateTime.UtcNow.AddHours(5),
                proctor.Id,
                examiner.Id), CancellationToken.None);

        updateResult.IsSuccess.Should().BeFalse();
        updateResult.Error.Should().Be(OralExamination.Application.Common.Models.ShiftMutationErrors.HasUsedTickets);
    }

    [Fact(DisplayName = "S0-ANTICHEAT-OPTIONAL-STUDENT-01: Student bỏ trống/null studentId dùng JWT ActorUserId (200 OK), truyền studentId SV khác vẫn 403, Proctor bỏ trống bị 400")]
    public async Task OfficialExams_LogAntiCheat_OptionalStudentId_ResolvesFromJwtForStudent_AndBlocksSpoofing()
    {
        // 1. Student omits studentId (null) -> resolved from JWT ActorUserId -> 200 OK
        var studentNullRes = await SendAsync("POST", "/api/v1/official-exams/anti-cheat/log", _student1, new
        {
            ticketId = _ticket1.Id,
            shiftId = _shift.Id,
            studentId = (Guid?)null,
            eventType = AntiCheatEventType.WindowBlur,
            severity = AntiCheatSeverity.Medium,
            details = "Kiosk blur without studentId",
            clientEventId = $"kiosk-null-student-{Guid.NewGuid():N}"
        });
        studentNullRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await studentNullRes.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("studentId").GetGuid().Should().Be(_student1.Id);
            doc.RootElement.GetProperty("ticketId").GetGuid().Should().Be(_ticket1.Id);
        }

        // 2. Student sends Guid.Empty -> also resolved from JWT ActorUserId -> 200 OK
        var studentEmptyRes = await SendAsync("POST", "/api/v1/official-exams/anti-cheat/log", _student1, new
        {
            ticketId = _ticket1.Id,
            shiftId = _shift.Id,
            studentId = Guid.Empty,
            eventType = AntiCheatEventType.TabSwitch,
            severity = AntiCheatSeverity.Low,
            details = "Kiosk tab switch with empty studentId",
            clientEventId = $"kiosk-empty-student-{Guid.NewGuid():N}"
        });
        studentEmptyRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Student spoofs another student's ID -> MUST remain 403 Forbidden
        var studentSpoofRes = await SendAsync("POST", "/api/v1/official-exams/anti-cheat/log", _student1, new
        {
            ticketId = _ticket2.Id,
            shiftId = _shift.Id,
            studentId = _student2.Id,
            eventType = AntiCheatEventType.WindowBlur,
            severity = AntiCheatSeverity.High,
            details = "Spoof attempt"
        });
        studentSpoofRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Proctor omits studentId -> validator rejects with 400 BadRequest
        var proctorNullRes = await SendAsync("POST", "/api/v1/official-exams/anti-cheat/log", _assignedProctor, new
        {
            ticketId = _ticket1.Id,
            shiftId = _shift.Id,
            studentId = (Guid?)null,
            eventType = AntiCheatEventType.WindowBlur,
            severity = AntiCheatSeverity.Medium,
            details = "Proctor missing studentId"
        });
        proctorNullRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "S0-DTO-F5-SYNC-01: GetOfficialExamSessions, GetExamShifts và GetMyWorkstationTicket ánh xạ đủ ExamAttemptType, IsExamPublished, IsGradesPublished, GradesPublishedAt, CertificateHash")]
    public async Task OfficialExamReadHandlers_MapF5SafeSprint0DtoFields()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OralExamDbContext(options);

        var student = CreateUser(UserRole.Student, "sv.f5sync@test.vn", "SE180401");
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", SemesterId = semester.Id, IsActive = true };
        var sessionRe = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            Title = "SWD392 Retake Exam",
            ExamAttemptType = OfficialExamAttemptType.RetakeExam,
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "in_progress"
        };

        var now = DateTime.UtcNow;
        var certHash = new string('a', 64);
        var publishedAt1 = now.AddMinutes(-10);
        var publishedAt2 = now.AddMinutes(-5);

        var emptyShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = sessionRe.Id,
            ShiftName = "Ca chưa công bố",
            RoomLab = "Lab 101",
            StartTime = now.AddHours(-2),
            EndTime = now.AddHours(-1),
            Status = "scheduled",
            IsExamPublished = false
        };

        var publishedShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = sessionRe.Id,
            ShiftName = "Ca đã công bố",
            RoomLab = "Lab 102",
            StartTime = now.AddMinutes(-30),
            EndTime = now.AddMinutes(30),
            Status = "in_progress",
            IsExamPublished = true,
            CertificateHash = certHash
        };

        var publishedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = publishedShift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            IpAddress = "10.0.0.5",
            Status = ExamTicketStatus.Published,
            IsLocked = true,
            GradesPublishedAt = publishedAt2
        };
        var earlierTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = publishedShift.Id,
            StudentId = Guid.NewGuid(),
            SeatNumber = 2,
            IpAddress = "10.0.0.6",
            Status = ExamTicketStatus.Published,
            IsLocked = true,
            GradesPublishedAt = publishedAt1
        };

        db.Users.Add(student);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(sessionRe);
        db.RealExamSessionShifts.AddRange(emptyShift, publishedShift);
        db.StudentExamTickets.AddRange(publishedTicket, earlierTicket);
        await db.SaveChangesAsync();

        // 1. GetOfficialExamSessions maps ExamAttemptType = "RE"
        var sessionsRes = await new GetOfficialExamSessionsQueryHandler(db)
            .Handle(new GetOfficialExamSessionsQuery(course.Id, null), CancellationToken.None);
        sessionsRes.IsSuccess.Should().BeTrue();
        sessionsRes.Value.Should().ContainSingle(s => s.Id == sessionRe.Id && s.ExamAttemptType == OfficialExamAttemptType.RetakeExam);

        // 2. GetExamShifts maps IsExamPublished, IsGradesPublished, GradesPublishedAt (max), CertificateHash
        var shiftsRes = await new GetExamShiftsQueryHandler(db)
            .Handle(new GetExamShiftsQuery(sessionRe.Id, null, null), CancellationToken.None);
        shiftsRes.IsSuccess.Should().BeTrue();

        var emptyDto = shiftsRes.Value.Single(s => s.Id == emptyShift.Id);
        emptyDto.IsExamPublished.Should().BeFalse();
        emptyDto.IsGradesPublished.Should().BeFalse();
        emptyDto.GradesPublishedAt.Should().BeNull();
        emptyDto.CertificateHash.Should().BeNull();

        var publishedDto = shiftsRes.Value.Single(s => s.Id == publishedShift.Id);
        publishedDto.IsExamPublished.Should().BeTrue();
        publishedDto.IsGradesPublished.Should().BeTrue();
        publishedDto.GradesPublishedAt.Should().Be(publishedAt2);
        publishedDto.CertificateHash.Should().Be(certHash);

        // 3. GetMyWorkstationTicket maps ExamAttemptType from Session
        var ticketRes = await new GetMyWorkstationTicketQueryHandler(db, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.5", now), CancellationToken.None);
        ticketRes.IsSuccess.Should().BeTrue();
        ticketRes.Value.ExamAttemptType.Should().Be(OfficialExamAttemptType.RetakeExam);
    }

    [Fact(DisplayName = "S0-PROCTOR-IDEMPOTENCY-01: RecordProctorViolation lặp lại cùng (ticketId, clientEventId) trả về idempotent, không tạo duplicate log/audit hay broadcast lần 2")]
    public async Task RecordProctorViolation_DuplicateClientEventId_IsIdempotent_AndNotifiesOnce()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OralExamDbContext(options);

        var proctor = CreateUser(UserRole.Proctor, "proctor.idem@test.vn");
        var student = CreateUser(UserRole.Student, "sv.idem@test.vn", "SE180501");
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Idempotency",
            RoomLab = "Lab 202",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(50),
            ProctorUserId = proctor.Id,
            Status = "in_progress"
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress
        };
        db.Users.AddRange(proctor, student);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IAntiCheatNotifier>();
        var handler = new RecordProctorViolationCommandHandler(db, notifierMock.Object);

        const string eventKey = "proctor-retry-evt-001";
        var cmd = new RecordProctorViolationCommand(
            proctor.Id,
            shift.Id,
            ticket.Id,
            student.Id,
            AntiCheatEventType.WindowBlur,
            AntiCheatSeverity.Medium,
            "Thí sinh chuyển cửa sổ",
            eventKey);

        var first = await handler.Handle(cmd, CancellationToken.None);
        var retry = await handler.Handle(cmd, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        retry.IsSuccess.Should().BeTrue();
        retry.Value.Id.Should().Be(first.Value.Id);
        retry.Value.ClientEventId.Should().Be(eventKey);
        retry.Value.ViolationCount.Should().Be(1);
        (await db.ExamAntiCheatLogs.CountAsync(l => l.TicketId == ticket.Id)).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == "PROCTOR_VIOLATION_RECORDED")).Should().Be(1);
        notifierMock.Verify(
            n => n.NotifyAntiCheatEventAsync(It.IsAny<ExamAntiCheatLogDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }
}
