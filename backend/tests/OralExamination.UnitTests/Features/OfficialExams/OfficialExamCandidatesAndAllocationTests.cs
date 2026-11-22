using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OfficeOpenXml;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;
using OralExamination.Application.Features.OfficialExams.Commands.BatchCreateOfficialExamSessions;
using OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;
using OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Commands.ImportSessionCandidates;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamEnrollmentEstimate;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;
using OralExamination.Application.Features.OfficialExams.Queries.GetSemesterEligibleExamCourses;
using OralExamination.Application.Features.OfficialExams.Queries.GetSessionCandidates;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamCandidatesAndAllocationTests : IDisposable
{
    private const string SigningKey = "Sprint1OfficialExamCandidatesAllocationSigningKeyMin32Bytes!!";
    private const string Issuer = "Sprint1CandidatesIssuer";
    private const string Audience = "Sprint1CandidatesAudience";

    private sealed class SeededRandomGenerator(int seed) : IOfficialExamRandomGenerator
    {
        private readonly Random _rng = new(seed);
        public int NextInt32(int toExclusive) => _rng.Next(toExclusive);
    }

    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly User _officer;
    private readonly User _owningDeptHead;
    private readonly User _otherDeptHead;
    private readonly User _lecturer;
    private readonly User _proctor;
    private readonly User _admin;
    private readonly Semester _semester;
    private readonly Course _course;
    private readonly ExamStructure _structure;
    private readonly Class _activeClass;
    private readonly List<User> _enrolledStudents = new();
    private readonly List<User> _retakeOnlyStudents = new();
    private readonly User _inactiveStudent;

    public OfficialExamCandidatesAndAllocationTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = SigningKey,
            ["Authentication:JwtIssuer"] = Issuer,
            ["Authentication:JwtAudience"] = Audience,
            ["Authentication:AccessTokenMinutes"] = "60"
        }).Build();

        var dbName = Guid.NewGuid().ToString();
        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
                services.AddSingleton<ICandidateRosterFileParser, CandidateRosterFileParser>();
                services.AddSingleton<IExamRosterFileParser, ExamRosterFileParser>();
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

        _officer = CreateUser(UserRole.ExaminationOfficer, "eo.s1@fpt.edu.vn");
        _owningDeptHead = CreateUser(UserRole.DepartmentHead, "dh.own.s1@fpt.edu.vn");
        _otherDeptHead = CreateUser(UserRole.DepartmentHead, "dh.other.s1@fpt.edu.vn");
        _lecturer = CreateUser(UserRole.Lecturer, "lec.s1@fpt.edu.vn");
        _proctor = CreateUser(UserRole.Proctor, "proctor.s1@fpt.edu.vn");
        _admin = CreateUser(UserRole.Admin, "admin.s1@fpt.edu.vn");

        _semester = new Semester
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
            Name = "Cross-Platform Backend",
            SemesterId = _semester.Id,
            AcademicProgramId = program.Id,
            DepartmentHeadUserId = _owningDeptHead.Id,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            IsActive = true
        };

        _structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            CreatedBy = _owningDeptHead.Id,
            Name = "PRN231 Official Structure",
            IsActive = true
        };

        _activeClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801",
            Name = "SE1801 PRN231",
            JoinCode = "SE1801S1",
            CourseId = _course.Id,
            SemesterId = _semester.Id,
            IsActive = true
        };

        for (var i = 1; i <= 60; i++)
        {
            var student = CreateUser(UserRole.Student, $"sv{i:D3}@fpt.edu.vn", $"SE18{i:D4}");
            _enrolledStudents.Add(student);
            db.ClassEnrollments.Add(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = _activeClass.Id,
                StudentId = student.Id,
                Status = "enrolled",
                JoinMethod = "assigned"
            });
        }

        for (var i = 1; i <= 5; i++)
        {
            _retakeOnlyStudents.Add(CreateUser(UserRole.Student, $"re{i:D3}@fpt.edu.vn", $"SE1790{i:D2}"));
        }

        _inactiveStudent = CreateUser(UserRole.Student, "inactive.s1@fpt.edu.vn", "SE189999", isActive: false);

        db.Users.AddRange(_officer, _owningDeptHead, _otherDeptHead, _lecturer, _proctor, _admin, _inactiveStudent);
        db.Users.AddRange(_enrolledStudents);
        db.Users.AddRange(_retakeOnlyStudents);
        db.Semesters.Add(_semester);
        db.AcademicPrograms.Add(program);
        db.Courses.Add(_course);
        db.ExamStructures.Add(_structure);
        db.Classes.Add(_activeClass);
        db.SaveChanges();
    }

    private static User CreateUser(string role, string email, string? studentCode = null, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        FullName = $"User {studentCode ?? role}",
        StudentCode = studentCode,
        Role = role,
        IsActive = isActive,
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

    private async Task<HttpResponseMessage> SendJsonAsync(string method, string route, User? actor, object? body = null)
    {
        using var req = new HttpRequestMessage(new HttpMethod(method), route);
        if (body is not null)
        {
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        if (actor is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(actor));
        }

        return await _client.SendAsync(req);
    }

    private async Task<HttpResponseMessage> SendFileUploadAsync(
        string route,
        User? actor,
        byte[] fileBytes,
        string fileName,
        string contentType,
        string? mode = null)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(fileContent, "file", fileName);
        if (!string.IsNullOrWhiteSpace(mode))
        {
            form.Add(new StringContent(mode), "mode");
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, route) { Content = form };
        if (actor is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(actor));
        }

        return await _client.SendAsync(req);
    }

    private static byte[] BuildXlsxBytes(string header, IEnumerable<string> studentCodes)
    {
        ExcelPackage.License.SetNonCommercialOrganization("FPT University Oral Examination Project");
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Sheet1");
        ws.Cells[1, 1].Value = header;
        var row = 2;
        foreach (var code in studentCodes)
        {
            ws.Cells[row++, 1].Value = code;
        }
        return pkg.GetAsByteArray();
    }

    [Fact(DisplayName = "S1-FERE-01: Khởi tạo độc lập đợt FE và RE trên cùng (Semester, Course), chặn trùng 409, hỗ trợ skipExisting và lọc GetOfficialExamSessions")]
    public async Task FeAndReSessions_AreIndependentPerCourseSemester_AndEnforceUniqueness()
    {
        // 1. Tạo đợt FE bằng batch
        var feRes = await SendJsonAsync("POST", $"/api/v1/official-exams/semesters/{_semester.Id}/sessions/batch", _officer, new
        {
            examDate = "2026-12-10",
            examAttemptType = "FE",
            examInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            transcriptBufferSeconds = 60,
            skipExisting = false,
            courses = new[] { new { courseId = _course.Id } }
        });
        feRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Tạo trùng FE với skipExisting = false -> 409 Conflict
        var dupFeRes = await SendJsonAsync("POST", $"/api/v1/official-exams/semesters/{_semester.Id}/sessions/batch", _officer, new
        {
            examDate = "2026-12-11",
            examAttemptType = "FE",
            examInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            transcriptBufferSeconds = 60,
            skipExisting = false,
            courses = new[] { new { courseId = _course.Id } }
        });
        dupFeRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 3. Tạo đợt RE trên cùng (Semester, Course) -> 201 Created độc lập với FE!
        var reRes = await SendJsonAsync("POST", $"/api/v1/official-exams/semesters/{_semester.Id}/sessions/batch", _officer, new
        {
            examDate = "2026-12-20",
            examAttemptType = "RE",
            examInputMode = ExamInputMode.VoiceOnly,
            transcriptBufferSeconds = 60,
            skipExisting = false,
            courses = new[] { new { courseId = _course.Id } }
        });
        reRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. CreateOfficialExamSession đơn lẻ trùng (Semester, Course, RE) -> 409 Conflict
        var dupSingleRe = await SendJsonAsync("POST", "/api/v1/official-exams/sessions", _officer, new
        {
            courseId = _course.Id,
            examStructureId = _structure.Id,
            title = "Duplicate RE",
            examDate = "2026-12-21",
            hasFollowUp = false,
            maxFollowUpQuestions = 2,
            examInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            transcriptBufferSeconds = 60,
            examAttemptType = "RE"
        });
        dupSingleRe.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 5. GET eligible-courses trả về cả feSession lẫn reSession
        var eligibleRes = await SendJsonAsync("GET", $"/api/v1/official-exams/semesters/{_semester.Id}/eligible-courses", _officer);
        eligibleRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await eligibleRes.Content.ReadAsStringAsync()))
        {
            var courseObj = doc.RootElement.EnumerateArray().Single(e => e.GetProperty("courseId").GetGuid() == _course.Id);
            courseObj.GetProperty("feSession").GetProperty("examAttemptType").GetString().Should().Be("FE");
            courseObj.GetProperty("reSession").GetProperty("examAttemptType").GetString().Should().Be("RE");
        }

        // 6. GET sessions lọc theo semesterId & examAttemptType=RE chỉ trả đúng 1 đợt RE
        var reOnlyRes = await SendJsonAsync("GET", $"/api/v1/official-exams/sessions?semesterId={_semester.Id}&examAttemptType=RE", _officer);
        reOnlyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await reOnlyRes.Content.ReadAsStringAsync()))
        {
            var arr = doc.RootElement.EnumerateArray().ToList();
            arr.Should().ContainSingle();
            arr[0].GetProperty("examAttemptType").GetString().Should().Be("RE");
            arr[0].GetProperty("courseCode").GetString().Should().Be("PRN231");
        }

        // 7. Invalid examAttemptType -> 400 BadRequest
        var badAttemptRes = await SendJsonAsync("GET", "/api/v1/official-exams/sessions?examAttemptType=PE", _officer);
        badAttemptRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "S1-TEMPLATE-01: Tải template Excel (.xlsx với ZIP magic bytes) & CSV thành công; chặn format sai (400) và non-EO (403)")]
    public async Task CandidateTemplateDownload_ReturnsValidXlsxAndCsv_AndEnforcesRbac()
    {
        var xlsxRes = await SendJsonAsync("GET", "/api/v1/official-exams/candidates/import/template?format=xlsx", _officer);
        xlsxRes.StatusCode.Should().Be(HttpStatusCode.OK);
        xlsxRes.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var xlsxBytes = await xlsxRes.Content.ReadAsByteArrayAsync();
        xlsxBytes.Take(4).Should().Equal(0x50, 0x4B, 0x03, 0x04);

        var csvRes = await SendJsonAsync("GET", "/api/v1/official-exams/candidates/import/template?format=csv", _officer);
        csvRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var csvText = await csvRes.Content.ReadAsStringAsync();
        csvText.Should().Contain("studentCode").And.Contain("SE170001");

        var badFormatRes = await SendJsonAsync("GET", "/api/v1/official-exams/candidates/import/template?format=pdf", _officer);
        badFormatRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        foreach (var blocked in new[] { _lecturer, _proctor, _owningDeptHead, _admin, _enrolledStudents[0] })
        {
            var forbiddenRes = await SendJsonAsync("GET", "/api/v1/official-exams/candidates/import/template?format=csv", blocked);
            forbiddenRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact(DisplayName = "S1-PARSER-GUARD-01: Chặn tệp > 2MB (413), giả mạo Magic Bytes .xlsx hoặc đuôi sai (415), vượt quá 2.000 dòng (422) và thiếu header MSSV (400)")]
    public async Task CandidateRosterImport_Enforces413_415_422_400_Guards()
    {
        var sessionId = await CreateSessionViaApiAsync(OfficialExamAttemptType.FinalExam);
        var previewRoute = $"/api/v1/official-exams/sessions/{sessionId}/candidates/import/preview";

        // 1. > 2MB -> 413 Payload Too Large
        var oversized = new byte[OfficialExamRules.MaxCandidateRosterBytes + 1024];
        Array.Fill(oversized, (byte)'A');
        var res413 = await SendFileUploadAsync(previewRoute, _officer, oversized, "big.csv", "text/csv");
        res413.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        // 2. Fake .xlsx (plain text renamed to .xlsx -> wrong Magic Bytes) -> 415 Unsupported Media Type
        var fakeXlsx = Encoding.UTF8.GetBytes("studentCode\nSE180001\n");
        var res415FakeXlsx = await SendFileUploadAsync(
            previewRoute,
            _officer,
            fakeXlsx,
            "spoofed.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        res415FakeXlsx.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        // 3. Unsupported extension .txt -> 415 Unsupported Media Type
        var res415Ext = await SendFileUploadAsync(previewRoute, _officer, fakeXlsx, "roster.txt", "text/plain");
        res415Ext.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        // 4. Binary EXE header disguised as .csv -> 415 Unsupported Media Type
        var fakeExeCsv = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 };
        var res415BinCsv = await SendFileUploadAsync(previewRoute, _officer, fakeExeCsv, "malware.csv", "text/csv");
        res415BinCsv.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        // 5. > 2,000 rows -> 422 Unprocessable Entity
        var sb = new StringBuilder("MSSV\n");
        for (var i = 1; i <= 2001; i++)
        {
            sb.Append("SE").Append(i.ToString("D6")).Append('\n');
        }
        var res422Rows = await SendFileUploadAsync(previewRoute, _officer, Encoding.UTF8.GetBytes(sb.ToString()), "too-many.csv", "text/csv");
        res422Rows.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 6. Missing MSSV/studentCode header -> 400 Bad Request
        var badHeaderCsv = Encoding.UTF8.GetBytes("email,fullName\nsv001@fpt.edu.vn,Test\n");
        var res400Header = await SendFileUploadAsync(previewRoute, _officer, badHeaderCsv, "no-mssv.csv", "text/csv");
        res400Header.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "S1-IMPORT-REPLACE-APPEND-01: Preview/Commit kiểm tra lỗi 422 atomic, REPLACE trước khi chia phòng, chặn REPLACE 409 sau khi đã cấp vé, APPEND idempotent và DELETE candidate (204/409)")]
    public async Task CandidateImport_SupportsReplaceAndAppend_BlocksReplaceAndDeleteWhenTicketsExist()
    {
        var sessionId = await CreateSessionViaApiAsync(OfficialExamAttemptType.RetakeExam);
        var previewRoute = $"/api/v1/official-exams/sessions/{sessionId}/candidates/import/preview";
        var commitRoute = $"/api/v1/official-exams/sessions/{sessionId}/candidates/import/commit";

        // 1. Preview có dòng lỗi (MSSV lặp trong file + MSSV không tồn tại + SV inactive) -> canCommit = false, Commit trả 422 & 0 ghi
        var invalidCsv = Encoding.UTF8.GetBytes(
            $"MSSV\n{_enrolledStudents[0].StudentCode}\n{_enrolledStudents[0].StudentCode}\nSE999999\n{_inactiveStudent.StudentCode}\n{_retakeOnlyStudents[0].StudentCode}\n");

        var badPreviewRes = await SendFileUploadAsync(previewRoute, _officer, invalidCsv, "invalid.csv", "text/csv", "REPLACE");
        badPreviewRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await badPreviewRes.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("totalRows").GetInt32().Should().Be(5);
            doc.RootElement.GetProperty("validCount").GetInt32().Should().Be(2);
            doc.RootElement.GetProperty("invalidCount").GetInt32().Should().Be(3);
            doc.RootElement.GetProperty("duplicateCount").GetInt32().Should().Be(1);
            doc.RootElement.GetProperty("canCommit").GetBoolean().Should().BeFalse();

            // Retake-only student (not enrolled in current semester) is STILL valid with isEnrolledInSemester == false
            var retakeRow = doc.RootElement.GetProperty("rows").EnumerateArray()
                .Single(r => r.GetProperty("studentCode").GetString() == _retakeOnlyStudents[0].StudentCode);
            retakeRow.GetProperty("isValid").GetBoolean().Should().BeTrue();
            retakeRow.GetProperty("isEnrolledInSemester").GetBoolean().Should().BeFalse();
        }

        var badCommitRes = await SendFileUploadAsync(commitRoute, _officer, invalidCsv, "invalid.csv", "text/csv", "REPLACE");
        badCommitRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 2. Import hợp lệ bằng Excel .xlsx (chỉ có cột MSSV) -> REPLACE 50 SV
        var first50Codes = _enrolledStudents.Take(50).Select(s => s.StudentCode!).ToList();
        var xlsx50 = BuildXlsxBytes("MSSV", first50Codes);

        var preview50Res = await SendFileUploadAsync(previewRoute, _officer, xlsx50, "roster50.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "REPLACE");
        preview50Res.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await preview50Res.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("canCommit").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("projectedTotalCandidates").GetInt32().Should().Be(50);
            doc.RootElement.GetProperty("maxStudentsPerRoom").GetInt32().Should().Be(24);
            doc.RootElement.GetProperty("recommendedTotalRooms").GetInt32().Should().Be(3);
        }

        var commit50Res = await SendFileUploadAsync(commitRoute, _officer, xlsx50, "roster50.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "REPLACE");
        commit50Res.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await commit50Res.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("importedCount").GetInt32().Should().Be(50);
            doc.RootElement.GetProperty("totalCandidateCount").GetInt32().Should().Be(50);
            doc.RootElement.GetProperty("recommendedTotalRooms").GetInt32().Should().Be(3);
        }

        // 3. Xóa 1 thí sinh trước khi phân bổ -> 204 No Content, sau đó thêm lại bằng REPLACE
        var delBeforeRes = await SendJsonAsync("DELETE", $"/api/v1/official-exams/sessions/{sessionId}/candidates/{_enrolledStudents[49].Id}", _officer);
        delBeforeRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reCommit50Res = await SendFileUploadAsync(commitRoute, _officer, xlsx50, "roster50.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "REPLACE");
        reCommit50Res.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Mở 3 phòng (72 ghế) và auto-allocate 50 SV -> cân bằng [17, 17, 16]
        var openShiftsRes = await SendJsonAsync("POST", $"/api/v1/official-exams/sessions/{sessionId}/shifts/batch", _officer, new
        {
            shiftGroupName = "Ca 1",
            startTime = "2026-12-20T01:00:00Z",
            endTime = "2026-12-20T03:00:00Z",
            rooms = new[]
            {
                new { roomLab = "LAB-101", maxSeats = 24 },
                new { roomLab = "LAB-102", maxSeats = 24 },
                new { roomLab = "LAB-103", maxSeats = 24 }
            }
        });
        openShiftsRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var alloc1Res = await SendJsonAsync("POST", $"/api/v1/official-exams/sessions/{sessionId}/auto-allocate-students", _officer, new
        {
            assignHandedOverExamSets = false,
            dryRun = false
        });
        alloc1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await alloc1Res.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("newlyAllocatedCount").GetInt32().Should().Be(50);
            var counts = doc.RootElement.GetProperty("shiftGroups")[0].GetProperty("rooms").EnumerateArray()
                .Select(r => r.GetProperty("totalAfter").GetInt32()).ToList();
            counts.Should().Equal(17, 17, 16);
        }

        // 5. Sau khi đã có vé thi -> REPLACE bị chặn 409 Conflict (cả preview và commit); DELETE thí sinh đã có vé bị chặn 409
        var replaceBlockedPreview = await SendFileUploadAsync(previewRoute, _officer, xlsx50, "roster50.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "REPLACE");
        replaceBlockedPreview.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var replaceBlockedCommit = await SendFileUploadAsync(commitRoute, _officer, xlsx50, "roster50.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "REPLACE");
        replaceBlockedCommit.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var delAfterTicketRes = await SendJsonAsync("DELETE", $"/api/v1/official-exams/sessions/{sessionId}/candidates/{_enrolledStudents[0].Id}", _officer);
        delAfterTicketRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 6. APPEND 10 SV mới + 2 SV đã có trong đợt (idempotent) -> chỉ chèn 10 SV mới, tổng 60 SV
        var appendCodes = _enrolledStudents.Skip(48).Take(12).Select(s => s.StudentCode!).ToList(); // 2 old + 10 new
        var appendCsv = Encoding.UTF8.GetBytes("student_code\n" + string.Join("\n", appendCodes) + "\n");

        var appendPreview = await SendFileUploadAsync(previewRoute, _officer, appendCsv, "append.csv", "text/csv", "APPEND");
        appendPreview.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await appendPreview.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("canCommit").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("duplicateCount").GetInt32().Should().Be(2);
            doc.RootElement.GetProperty("existingCandidateCount").GetInt32().Should().Be(50);
            doc.RootElement.GetProperty("projectedTotalCandidates").GetInt32().Should().Be(60);
        }

        var appendCommit = await SendFileUploadAsync(commitRoute, _officer, appendCsv, "append.csv", "text/csv", "APPEND");
        appendCommit.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await appendCommit.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("importedCount").GetInt32().Should().Be(10);
            doc.RootElement.GetProperty("totalCandidateCount").GetInt32().Should().Be(60);
        }

        // Retry identical APPEND -> idempotent 0 newly imported, total stays 60
        var appendRetry = await SendFileUploadAsync(commitRoute, _officer, appendCsv, "append.csv", "text/csv", "APPEND");
        appendRetry.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await appendRetry.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("importedCount").GetInt32().Should().Be(0);
            doc.RootElement.GetProperty("totalCandidateCount").GetInt32().Should().Be(60);
        }

        // 7. Auto-allocate lần 2 sau APPEND -> giữ nguyên 50 vé cũ, xếp 10 SV mới cân bằng thành [20, 20, 20]
        var alloc2Res = await SendJsonAsync("POST", $"/api/v1/official-exams/sessions/{sessionId}/auto-allocate-students", _officer, new
        {
            assignHandedOverExamSets = false,
            dryRun = false
        });
        alloc2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await alloc2Res.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("totalEligibleStudents").GetInt32().Should().Be(60);
            doc.RootElement.GetProperty("previouslyAllocatedCount").GetInt32().Should().Be(50);
            doc.RootElement.GetProperty("newlyAllocatedCount").GetInt32().Should().Be(10);
            var counts = doc.RootElement.GetProperty("shiftGroups")[0].GetProperty("rooms").EnumerateArray()
                .Select(r => r.GetProperty("totalAfter").GetInt32()).ToList();
            counts.Should().Equal(20, 20, 20);
        }

        // 8. GET candidates có search, pagination, trạng thái phân bổ & BOLA DH đúng môn 200 / DH môn khác 403
        var candidatesRes = await SendJsonAsync("GET", $"/api/v1/official-exams/sessions/{sessionId}/candidates?search=SE180001&allocatedOnly=true&pageNumber=1&pageSize=10", _owningDeptHead);
        candidatesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await candidatesRes.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
            doc.RootElement.GetProperty("allocatedCount").GetInt32().Should().Be(60);
            doc.RootElement.GetProperty("unallocatedCount").GetInt32().Should().Be(0);
            var firstItem = doc.RootElement.GetProperty("items")[0];
            firstItem.GetProperty("studentCode").GetString().Should().Be("SE180001");
            firstItem.GetProperty("isAllocated").GetBoolean().Should().BeTrue();
            firstItem.GetProperty("seatNumber").GetInt32().Should().BeGreaterThanOrEqualTo(1);
            firstItem.GetProperty("roomLab").GetString().Should().StartWith("LAB-");
        }

        var otherDhCandidatesRes = await SendJsonAsync("GET", $"/api/v1/official-exams/sessions/{sessionId}/candidates", _otherDeptHead);
        otherDhCandidatesRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "S1-CANDIDATE-SOURCE-01: Estimate & AutoAllocate đọc 100% từ official_exam_candidates (0 candidate -> 422 dù có 60 SV enrolled), và chặn 422 khi < 24 bộ đề HANDED_OVER (loại trừ IsSampleSet)")]
    public async Task EstimateAndAllocation_ReadStrictlyFromCandidates_AndFailClosed422WhenZeroCandidatesOrInsufficientHandedOverSets()
    {
        var sessionId = await CreateSessionViaApiAsync(OfficialExamAttemptType.FinalExam);

        // 1. Chưa import DSSV (dù có 60 SV enrolled trong lớp) -> Estimate báo 0 thí sinh dự thi, semesterEnrolledCount = 60
        var est0Res = await SendJsonAsync("GET", $"/api/v1/official-exams/sessions/{sessionId}/enrollment-estimate", _officer);
        est0Res.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await est0Res.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("eligibleStudentCount").GetInt32().Should().Be(0);
            doc.RootElement.GetProperty("importedCandidateCount").GetInt32().Should().Be(0);
            doc.RootElement.GetProperty("hasImportedCandidates").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("candidateSource").GetString().Should().Be("IMPORTED_CANDIDATES");
            doc.RootElement.GetProperty("semesterEnrolledCount").GetInt32().Should().Be(60);
            doc.RootElement.GetProperty("recommendedTotalRooms").GetInt32().Should().Be(0);
        }

        // Mở 1 phòng 24 ghế
        var openRes = await SendJsonAsync("POST", $"/api/v1/official-exams/sessions/{sessionId}/shifts/batch", _officer, new
        {
            shiftGroupName = "Ca 1",
            startTime = "2026-12-10T01:00:00Z",
            endTime = "2026-12-10T03:00:00Z",
            rooms = new[] { new { roomLab = "LAB-401", maxSeats = 24 } }
        });
        openRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Auto-allocate với 0 candidates -> 422 Unprocessable Entity
        var alloc0Res = await SendJsonAsync("POST", $"/api/v1/official-exams/sessions/{sessionId}/auto-allocate-students", _officer, new
        {
            assignHandedOverExamSets = false,
            dryRun = false
        });
        alloc0Res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 3. Import đúng 24 thí sinh (có cả 5 SV RE không nằm trong lớp kỳ hiện tại + 19 SV enrolled)
        var codes24 = _retakeOnlyStudents.Select(s => s.StudentCode!)
            .Concat(_enrolledStudents.Take(19).Select(s => s.StudentCode!))
            .ToList();
        var csv24 = Encoding.UTF8.GetBytes("studentCode\n" + string.Join("\n", codes24) + "\n");
        var commit24Res = await SendFileUploadAsync(
            $"/api/v1/official-exams/sessions/{sessionId}/candidates/import/commit",
            _officer,
            csv24,
            "24-students.csv",
            "text/csv",
            "REPLACE");
        commit24Res.StatusCode.Should().Be(HttpStatusCode.OK);

        // Seed 23 bộ đề HANDED_OVER + 2 bộ đề IsSampleSet = true (không được tính!) -> vẫn thiếu 1 bộ cho phòng 24 SV
        using (var scope = _server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            for (var i = 1; i <= 23; i++)
            {
                db.ExamSets.Add(new ExamSet
                {
                    Id = Guid.NewGuid(),
                    CourseId = _course.Id,
                    StructureId = _structure.Id,
                    SetCode = $"HO-SET-{i:D2}",
                    Status = ExamSetStatus.HandedOver,
                    IsSampleSet = false,
                    IsActive = true
                });
            }
            for (var i = 1; i <= 2; i++)
            {
                db.ExamSets.Add(new ExamSet
                {
                    Id = Guid.NewGuid(),
                    CourseId = _course.Id,
                    StructureId = _structure.Id,
                    SetCode = $"SAMPLE-SET-{i:D2}",
                    Status = ExamSetStatus.HandedOver,
                    IsSampleSet = true,
                    IsActive = true
                });
            }
            await db.SaveChangesAsync();
        }

        // Estimate phải báo handedOverExamSetCount == 23 (loại bỏ 2 sample sets)
        var est24Res = await SendJsonAsync("GET", $"/api/v1/official-exams/sessions/{sessionId}/enrollment-estimate", _officer);
        est24Res.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await est24Res.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("eligibleStudentCount").GetInt32().Should().Be(24);
            doc.RootElement.GetProperty("recommendedTotalRooms").GetInt32().Should().Be(1);
            doc.RootElement.GetProperty("handedOverExamSetCount").GetInt32().Should().Be(23);
        }

        // Auto-allocate yêu cầu phát bộ đề HANDED_OVER khi chỉ có 23 < 24 -> 422 Unprocessable Entity
        var insufficientSetsRes = await SendJsonAsync("POST", $"/api/v1/official-exams/sessions/{sessionId}/auto-allocate-students", _officer, new
        {
            assignHandedOverExamSets = true,
            dryRun = false
        });
        insufficientSetsRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Thêm bộ đề thứ 24 -> phân bổ thành công 24 SV, 24 bộ đề không trùng
        using (var scope = _server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.ExamSets.Add(new ExamSet
            {
                Id = Guid.NewGuid(),
                CourseId = _course.Id,
                StructureId = _structure.Id,
                SetCode = "HO-SET-24",
                Status = ExamSetStatus.HandedOver,
                IsSampleSet = false,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var alloc24Res = await SendJsonAsync("POST", $"/api/v1/official-exams/sessions/{sessionId}/auto-allocate-students", _officer, new
        {
            assignHandedOverExamSets = true,
            dryRun = false
        });
        alloc24Res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "S1-RBAC-SHIFTS-01: Khóa RBAC 401/403 trên 4 endpoints candidates (preview/commit/get/delete) và kiểm chứng lọc GetExamShifts theo examinerUserId + assignedTicketsCount")]
    public async Task CandidateEndpoints_EnforceRbac_And_GetShifts_SupportsExaminerFilterAndSeatCounts()
    {
        var sessionId = await CreateSessionViaApiAsync(OfficialExamAttemptType.FinalExam);
        var validCsv = Encoding.UTF8.GetBytes($"MSSV\n{_enrolledStudents[0].StudentCode}\n");

        // 1. Anonymous -> 401 trên tất cả endpoints candidates
        (await SendFileUploadAsync($"/api/v1/official-exams/sessions/{sessionId}/candidates/import/preview", null, validCsv, "a.csv", "text/csv"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendFileUploadAsync($"/api/v1/official-exams/sessions/{sessionId}/candidates/import/commit", null, validCsv, "a.csv", "text/csv"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendJsonAsync("GET", $"/api/v1/official-exams/sessions/{sessionId}/candidates", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendJsonAsync("DELETE", $"/api/v1/official-exams/sessions/{sessionId}/candidates/{_enrolledStudents[0].Id}", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Non-EO roles -> 403 trên preview/commit/delete
        foreach (var blocked in new[] { _owningDeptHead, _lecturer, _proctor, _admin, _enrolledStudents[0] })
        {
            (await SendFileUploadAsync($"/api/v1/official-exams/sessions/{sessionId}/candidates/import/preview", blocked, validCsv, "a.csv", "text/csv"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendFileUploadAsync($"/api/v1/official-exams/sessions/{sessionId}/candidates/import/commit", blocked, validCsv, "a.csv", "text/csv"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendJsonAsync("DELETE", $"/api/v1/official-exams/sessions/{sessionId}/candidates/{_enrolledStudents[0].Id}", blocked))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // 3. Student/Proctor/Lecturer/Admin -> 403 trên GET candidates
        foreach (var blocked in new[] { _lecturer, _proctor, _admin, _enrolledStudents[0] })
        {
            (await SendJsonAsync("GET", $"/api/v1/official-exams/sessions/{sessionId}/candidates", blocked))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // 4. GetShifts hỗ trợ lọc examinerUserId + trả đúng examAttemptType, assignedTicketsCount, remainingSeats
        var openRes = await SendJsonAsync("POST", $"/api/v1/official-exams/sessions/{sessionId}/shifts/batch", _officer, new
        {
            shiftGroupName = "Ca Examiner",
            startTime = "2026-12-10T04:00:00Z",
            endTime = "2026-12-10T06:00:00Z",
            rooms = new object[]
            {
                new { roomLab = "LAB-501", maxSeats = 24, proctorUserId = _proctor.Id, examinerUserId = _lecturer.Id },
                new { roomLab = "LAB-502", maxSeats = 24 }
            }
        });
        openRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var shiftsRes = await SendJsonAsync("GET", $"/api/v1/official-exams/shifts?sessionId={sessionId}&examinerUserId={_lecturer.Id}", _lecturer);
        shiftsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await shiftsRes.Content.ReadAsStringAsync());
        var arr = doc.RootElement.EnumerateArray().ToList();
        arr.Should().ContainSingle();
        arr[0].GetProperty("roomLab").GetString().Should().Be("LAB-501");
        arr[0].GetProperty("examAttemptType").GetString().Should().Be("FE");
        arr[0].GetProperty("assignedTicketsCount").GetInt32().Should().Be(0);
        arr[0].GetProperty("remainingSeats").GetInt32().Should().Be(24);
    }

    private async Task<Guid> CreateSessionViaApiAsync(string attemptType)
    {
        var res = await SendJsonAsync("POST", $"/api/v1/official-exams/semesters/{_semester.Id}/sessions/batch", _officer, new
        {
            examDate = attemptType == OfficialExamAttemptType.RetakeExam ? "2026-12-20" : "2026-12-10",
            examAttemptType = attemptType,
            examInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            transcriptBufferSeconds = 60,
            skipExisting = false,
            courses = new[] { new { courseId = _course.Id } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("sessions")[0].GetProperty("id").GetGuid();
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }
}
