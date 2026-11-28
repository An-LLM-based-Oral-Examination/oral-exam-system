using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Middlewares;
using OralExamination.Application.Features.Appeals;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Appeals.Queries.GetAppealAllocationProposal;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;
using OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.ConfigureOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Commands.DistributeShiftTickets;
using OralExamination.Application.Features.OfficialExams.Commands.ImportExamRoster;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Commands.UnlockExamTicketDevice;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShiftStatus;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftExcel;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamEnrollmentEstimate;
using OralExamination.Application.Features.Proctor.Commands.RecordProctorViolation;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Reporting;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class M3OfficialExamReadinessRegressionTests
{
    private const string LaptopA = "DEV-LAPTOP-M3-ALPHA-0001";
    private const string LaptopB = "DEV-LAPTOP-M3-BETA-0002";

    private static OralExamDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser(string role, string emailPrefix, string? studentCode = null, string? fullName = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = $"{emailPrefix}.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = fullName ?? $"{role}-{emailPrefix}",
            StudentCode = studentCode,
            Role = role,
            IsActive = true
        };

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "Test"));

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "OralExamination.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // =========================================================================
    // ITEM 18: PAdES .pfx Production 503 + Deploy Pre-Flight
    // =========================================================================

    [Fact(DisplayName = "M3-18-01: PadesPdfDigitalSigner Production thiếu cert, thiếu file .pfx, không có Private Key hoặc hết hạn ném PdfSigningCertificateException (: InvalidOperationException)")]
    public void Item18_PadesPdfDigitalSigner_ThrowsTypedException_InFailClosedScenarios()
    {
        typeof(PdfSigningCertificateException).Should().BeDerivedFrom<InvalidOperationException>();

        // 1. Missing config in Production
        var missingConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "true"
            })
            .Build();
        using var missingSigner = new PadesPdfDigitalSigner(missingConfig);
        Action actMissing = () => _ = missingSigner.GetActiveCertificateThumbprint();
        actMissing.Should().Throw<PdfSigningCertificateException>().WithMessage("*FAIL CLOSED*");

        // 2. Missing CertificatePath file in Production wraps FileNotFoundException
        var missingFileConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "true",
                ["PdfSigning:CertificatePath"] = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.pfx")
            })
            .Build();
        using var missingFileSigner = new PadesPdfDigitalSigner(missingFileConfig);
        Action actMissingFile = () => _ = missingFileSigner.GetActiveCertificateThumbprint();
        actMissingFile.Should().Throw<PdfSigningCertificateException>()
            .WithMessage("*FAIL CLOSED*")
            .Which.InnerException.Should().BeOfType<FileNotFoundException>();

        // 3. Expired certificate fails closed with PdfSigningCertificateException
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Expired Test Signer", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var expiredCert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1));
        var expiredB64 = Convert.ToBase64String(expiredCert.Export(X509ContentType.Pfx, "pwd"));
        var expiredConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "true",
                ["PdfSigning:CertificateBase64"] = expiredB64,
                ["PdfSigning:CertificatePassword"] = "pwd"
            })
            .Build();
        using var expiredSigner = new PadesPdfDigitalSigner(expiredConfig);
        Action actExpired = () => _ = expiredSigner.GetActiveCertificateThumbprint();
        actExpired.Should().Throw<PdfSigningCertificateException>().WithMessage("*hết hạn*");

        // 4. RequireCertificate = false in Production keeps ephemeral cert behavior working
        var pilotConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "false"
            })
            .Build();
        using var pilotSigner = new PadesPdfDigitalSigner(pilotConfig);
        pilotSigner.GetActiveCertificateThumbprint().Should().HaveLength(64);
    }

    [Fact(DisplayName = "M3-18-02: GlobalExceptionMiddleware ánh xạ PdfSigningCertificateException sang RFC 7807 503 PDF_SIGNING_CERT_MISSING (che đường dẫn ở Production)")]
    public async Task Item18_GlobalExceptionMiddleware_MapsPdfSigningCertificateException_To503()
    {
        var prodMiddleware = new GlobalExceptionMiddleware(
            _ => throw new PdfSigningCertificateException("[FAIL CLOSED] Không tìm thấy chứng thư tại '/etc/oralexam/certs/secret.pfx'."),
            NullLogger<GlobalExceptionMiddleware>.Instance,
            new FakeHostEnvironment(Environments.Production));

        var prodContext = new DefaultHttpContext();
        prodContext.Request.Path = "/api/v1/official-exams/shifts/1/export-pdf";
        prodContext.Response.Body = new MemoryStream();

        await prodMiddleware.InvokeAsync(prodContext);

        prodContext.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        prodContext.Response.ContentType.Should().Contain("application/problem+json");
        prodContext.Response.Body.Position = 0;
        var prodJson = JsonDocument.Parse(await new StreamReader(prodContext.Response.Body).ReadToEndAsync()).RootElement;
        prodJson.GetProperty("status").GetInt32().Should().Be(503);
        prodJson.GetProperty("errorCode").GetString().Should().Be("PDF_SIGNING_CERT_MISSING");
        prodJson.GetProperty("detail").GetString().Should().NotContain("/etc/oralexam/certs/secret.pfx");
    }

    // =========================================================================
    // ITEM 19: AssignExamTicket & Sibling ExamSet SessionId + StructureId Filter
    // =========================================================================

    [Fact(DisplayName = "M3-19-01: AssignExamTicket & CheckIn chỉ chọn bộ đề HANDED_OVER khớp đúng CourseId, SessionId và StructureId (không dùng ||)")]
    public async Task Item19_AssignAndCheckIn_FilterExamSetBySessionIdAndStructureId()
    {
        await using var db = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo19");
        var student1 = CreateUser(UserRole.Student, "sv19a", "SE190001");
        var student2 = CreateUser(UserRole.Student, "sv19b", "SE190002");
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", SemesterId = semester.Id, IsActive = true };
        var targetStructureId = Guid.NewGuid();
        var otherStructureId = Guid.NewGuid();
        var sessionFe = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            ExamAttemptType = OfficialExamAttemptType.FinalExam,
            ExamStructureId = targetStructureId,
            Title = "FE PRN231",
            ExamDate = new DateOnly(2026, 11, 25),
            Status = "scheduled"
        };
        var sessionRe = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            ExamAttemptType = OfficialExamAttemptType.RetakeExam,
            ExamStructureId = targetStructureId,
            Title = "RE PRN231",
            ExamDate = new DateOnly(2026, 12, 5),
            Status = "scheduled"
        };
        var shiftFe = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = sessionFe.Id,
            Session = sessionFe,
            ShiftName = "Ca FE",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ExamShiftWindow.InProgress,
            MaxSeats = 24
        };

        // Wrong structure, same course -> MUST NOT be selected
        var wrongStructureSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = otherStructureId,
            SessionId = sessionFe.Id,
            SetCode = "SET-WRONG-STRUCT",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        // Wrong session (belongs to RE), matching structure -> MUST NOT be selected for FE
        var wrongSessionSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = targetStructureId,
            SessionId = sessionRe.Id,
            SetCode = "SET-RE-ONLY",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        // Matching course + structure + session (or SessionId == null) -> MUST be selected
        var validSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = targetStructureId,
            SessionId = sessionFe.Id,
            SetCode = "SET-VALID-FE",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };

        db.Users.AddRange(officer, student1, student2);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.OfficialExamSessions.AddRange(sessionFe, sessionRe);
        db.RealExamSessionShifts.Add(shiftFe);
        db.ExamSets.AddRange(wrongStructureSet, wrongSessionSet, validSet);
        await db.SaveChangesAsync();

        var assignHandler = new AssignExamTicketCommandHandler(db, new AssignExamTicketCommandValidator());
        var firstAssign = await assignHandler.Handle(
            new AssignExamTicketCommand(officer.Id, shiftFe.Id, student1.Id, 1, null, student1.StudentCode),
            CancellationToken.None);
        firstAssign.IsSuccess.Should().BeTrue();
        firstAssign.Value.ExamSetId.Should().Be(validSet.Id);

        // Second assign has only wrongStructureSet and wrongSessionSet left -> MUST fail UNPROCESSABLE!
        var secondAssign = await assignHandler.Handle(
            new AssignExamTicketCommand(officer.Id, shiftFe.Id, student2.Id, 2, null, student2.StudentCode),
            CancellationToken.None);
        secondAssign.IsSuccess.Should().BeFalse();
        secondAssign.Error.Should().StartWith("UNPROCESSABLE:");

        // CheckIn on an un-set ticket loads Shift.Session via ThenInclude and resolves only matching Structure/Session
        var sharedSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = targetStructureId,
            SessionId = null,
            SetCode = "SET-SHARED-STRUCT",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        var unSetTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftFe.Id,
            StudentId = student2.Id,
            SeatNumber = 2,
            ExamSetId = null,
            Status = ExamTicketStatus.Scheduled
        };
        db.ExamSets.Add(sharedSet);
        db.StudentExamTickets.Add(unSetTicket);
        await db.SaveChangesAsync();

        var checkInHandler = new CheckInExamTicketCommandHandler(db, new CheckInExamTicketCommandValidator());
        var checkInResult = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student2.Id, unSetTicket.Id, "10.0.0.2", DateTime.UtcNow, LaptopA),
            CancellationToken.None);
        checkInResult.IsSuccess.Should().BeTrue(checkInResult.Error);
        checkInResult.Value.ExamSetId.Should().Be(sharedSet.Id);
    }

    [Fact(DisplayName = "M3-19-02: AutoAllocate, ImportExamRoster, DistributeShiftTickets và EnrollmentEstimate đều lọc theo StructureId khi Session có ExamStructureId")]
    public async Task Item19_SiblingExamSetQueries_EnforceStructureIdWhenConfigured()
    {
        await using var db = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo19s");
        var student = CreateUser(UserRole.Student, "sv19s", "SE190099");
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", SemesterId = semester.Id, IsActive = true };
        var targetStructureId = Guid.NewGuid();
        var otherStructureId = Guid.NewGuid();
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            ExamAttemptType = OfficialExamAttemptType.FinalExam,
            ExamStructureId = targetStructureId,
            Title = "SWD392 FE",
            ExamDate = new DateOnly(2026, 11, 25),
            Status = "scheduled"
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 201",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            Status = ExamShiftWindow.Scheduled,
            MaxSeats = 24
        };
        db.Users.AddRange(officer, student);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.OfficialExamCandidates.Add(new OfficialExamCandidate { SessionId = session.Id, StudentId = student.Id, ImportedBy = officer.Id });
        // Only a set from another structure exists -> all 4 handlers must report 0 matching sets!
        db.ExamSets.Add(new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = otherStructureId,
            SetCode = "SET-OTHER-STRUCT",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var estimateRes = await new GetOfficialExamEnrollmentEstimateQueryHandler(db)
            .Handle(new GetOfficialExamEnrollmentEstimateQuery(officer.Id, session.Id), CancellationToken.None);
        estimateRes.IsSuccess.Should().BeTrue();
        estimateRes.Value.HandedOverExamSetCount.Should().Be(0);

        var autoAllocRes = await new AutoAllocateSessionStudentsCommandHandler(db)
            .Handle(new AutoAllocateSessionStudentsCommand(officer.Id, session.Id, AssignHandedOverExamSets: true), CancellationToken.None);
        autoAllocRes.IsSuccess.Should().BeFalse();
        autoAllocRes.Error.Should().StartWith("UNPROCESSABLE:");

        var previewRosterRes = await new PreviewExamRosterImportQueryHandler(db)
            .Handle(new PreviewExamRosterImportQuery(officer.Id, shift.Id, [new BulkExamTicketImportRow(2, student.StudentCode!, 1, "")]), CancellationToken.None);
        previewRosterRes.IsSuccess.Should().BeTrue();
        previewRosterRes.Value.AvailableHandedOverSetCount.Should().Be(0);
        previewRosterRes.Value.CanCommit.Should().BeFalse();

        // Add ticket without ExamSetId and verify DistributeShiftTickets also ignores otherStructureId
        db.StudentExamTickets.Add(new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Scheduled
        });
        await db.SaveChangesAsync();

        var distRes = await new DistributeShiftTicketsCommandHandler(db)
            .Handle(new DistributeShiftTicketsCommand(officer.Id, officer.Role, shift.Id), CancellationToken.None);
        distRes.IsSuccess.Should().BeFalse();
        distRes.Error.Should().StartWith("UNPROCESSABLE:");
    }

    // =========================================================================
    // ITEM 20: UnlockExamTicketDevice & SubmitOfficialExam Locked Handling
    // =========================================================================

    [Fact(DisplayName = "M3-20-01: UnlockExamTicketDevice cho phép mở khóa vé bị WINDOW_BLUR Locked (IsLocked=false, chưa publish/submit) và khôi phục Status=IN_PROGRESS để thi tiếp")]
    public async Task Item20_UnlockDevice_AllowsAntiCheatLockedTicket_AndBlocksPublishedOrManuallyLocked()
    {
        await using var db = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo20");
        var student = CreateUser(UserRole.Student, "sv20", "SE200001");
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWP391", Name = "SWP391", SemesterId = semester.Id, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, SemesterId = semester.Id, Title = "SWP391 FE", ExamDate = new DateOnly(2026, 11, 25), Status = "in_progress" };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Session = session,
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true,
            MaxSeats = 24
        };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, SetCode = "SET-20", Status = ExamSetStatus.HandedOver, IsActive = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Locked, // Auto-locked by WINDOW_BLUR > 3
            IsLocked = false,
            DeviceId = LaptopA,
            DeviceLockedAt = DateTime.UtcNow.AddMinutes(-5),
            DeviceSessionTokenHash = "hash-old"
        };

        db.Users.AddRange(officer, student);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.ExamSets.Add(examSet);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var unlockHandler = new UnlockExamTicketDeviceCommandHandler(db);
        var unlockRes = await unlockHandler.Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket.Id, "Giám thị xác nhận lỗi kỹ thuật, cho phép đổi máy thi tiếp."),
            CancellationToken.None);

        unlockRes.IsSuccess.Should().BeTrue(unlockRes.Error);
        var updatedTicket = await db.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket.Id);
        updatedTicket.Status.Should().Be(ExamTicketStatus.InProgress);
        updatedTicket.DeviceId.Should().BeNull();
        updatedTicket.DeviceSessionTokenHash.Should().BeNull();
        updatedTicket.DeviceUnlockCount.Should().Be(1);

        var audit = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .FirstAsync(a => a.Action == "OFFICIAL_TICKET_DEVICE_UNLOCKED" && a.EntityId == ticket.Id);
        audit.NewValues.Should().Contain("LOCKED").And.Contain("IN_PROGRESS");

        // Student can re-check-in on replacement laptop
        var reCheckIn = await new CheckInExamTicketCommandHandler(db, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.5", DateTime.UtcNow, LaptopB), CancellationToken.None);
        reCheckIn.IsSuccess.Should().BeTrue(reCheckIn.Error);
        reCheckIn.Value.DeviceId.Should().Be(LaptopB);

        // Manually sealed / One-Way locked ticket (IsLocked = true) MUST remain blocked
        var tracked = await db.StudentExamTickets.SingleAsync(t => t.Id == ticket.Id);
        tracked.Status = ExamTicketStatus.Locked;
        tracked.IsLocked = true;
        await db.SaveChangesAsync();

        var blockedManualLock = await unlockHandler.Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket.Id, "Không được phép mở khi đã niêm phong."),
            CancellationToken.None);
        blockedManualLock.IsSuccess.Should().BeFalse();
        blockedManualLock.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "M3-20-02: SubmitOfficialExam trả OfficialExamErrors.TicketLocked (409 ticket-locked) cho vé Locked chưa nộp, và giữ idempotent Success khi đã có submissions")]
    public async Task Item20_SubmitOfficialExam_DistinguishesLockedTicketFromAlreadySubmitted()
    {
        await using var db = CreateDbContext();
        var student = CreateUser(UserRole.Student, "sv20s", "SE200002");
        var rawToken = OfficialExamDeviceGuard.GenerateSessionToken();
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Locked,
            IsLocked = false,
            DeviceId = LaptopA,
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(rawToken)
        };
        db.Users.Add(student);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var submitHandler = new SubmitOfficialExamCommandHandler(db);
        var lockedRes = await submitHandler.Handle(
            new SubmitOfficialExamCommand(student.Id, ticket.Id, null, null, LaptopA, rawToken),
            CancellationToken.None);
        lockedRes.IsSuccess.Should().BeFalse();
        lockedRes.Error.Should().Be(OfficialExamErrors.TicketLocked);

        // Controller maps TICKET_LOCKED -> 409 Conflict with dedicated error type URI
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SubmitOfficialExamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.TicketLocked));
        var controller = new OfficialExamSubmissionsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(student.Id, UserRole.Student) }
            }
        };
        var httpResult = await controller.Submit(ticket.Id, new SubmitOfficialExamRequest(null), CancellationToken.None);
        var problemObj = httpResult.Should().BeOfType<ObjectResult>().Subject;
        problemObj.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        problemObj.Value.Should().BeOfType<ProblemDetails>().Which.Type.Should().Be("https://oralexam.fpt.edu.vn/errors/ticket-locked");

        // Idempotent success preserved when SubmittedAt + Submissions exist even if Status == Locked
        db.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Đã nộp trước đó",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        ticket.SubmittedAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var idempotentRes = await submitHandler.Handle(
            new SubmitOfficialExamCommand(student.Id, ticket.Id, null, null, LaptopA, rawToken),
            CancellationToken.None);
        idempotentRes.IsSuccess.Should().BeTrue();
        idempotentRes.Value.IsKioskLocked.Should().BeTrue();
    }

    // =========================================================================
    // ITEM 21: OfficialExamReportQuery & ExamShiftGradeReportFactory
    // =========================================================================

    [Fact(DisplayName = "M3-21-01: Export Excel/PDF từ chối khi vé Locked chưa công bố (GradesPublishedAt=null) và ưu tiên Session.Semester.Code trên Course.Semester.Code")]
    public async Task Item21_ReportQueryRequiresGradesPublishedAt_AndFactoryPrioritizesSessionSemester()
    {
        await using var db = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo21");
        var student = CreateUser(UserRole.Student, "sv21", "SE210001");
        var courseSemester = new Semester { Id = Guid.NewGuid(), Code = "SP26", Name = "Spring 2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 4, 30), IsActive = true };
        var sessionSemester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", SemesterId = courseSemester.Id, Semester = courseSemester, IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Course = course,
            SemesterId = sessionSemester.Id,
            Semester = sessionSemester,
            Title = "SWD392 FA26",
            ExamDate = new DateOnly(2026, 11, 25),
            Status = "published"
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Session = session,
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "completed"
        };
        var lockedUnpublishedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            Student = student,
            SeatNumber = 1,
            Status = ExamTicketStatus.Locked,
            IsLocked = true,
            GradesPublishedAt = null // Manually locked pre-publish
        };

        db.Users.AddRange(officer, student);
        db.Semesters.AddRange(courseSemester, sessionSemester);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(lockedUnpublishedTicket);
        await db.SaveChangesAsync();

        var exporter = new ExamGradeReportExporter();
        var excelBeforePublish = await new ExportShiftExcelQueryHandler(db, exporter)
            .Handle(new ExportShiftExcelQuery(shift.Id, officer.Id), CancellationToken.None);
        var pdfBeforePublish = await new ExportShiftPdfQueryHandler(db, exporter)
            .Handle(new ExportShiftPdfQuery(shift.Id, officer.Id), CancellationToken.None);

        excelBeforePublish.IsSuccess.Should().BeFalse();
        pdfBeforePublish.IsSuccess.Should().BeFalse();

        // Factory prioritizes Session.Semester.Code ("FA26") over Course.Semester.Code ("SP26")
        var report = ExamShiftGradeReportFactory.Create(shift);
        report.SemesterCode.Should().Be("FA26");

        // Fallback to Course.Semester.Code when Session.Semester is null
        session.Semester = null!;
        var fallbackReport = ExamShiftGradeReportFactory.Create(shift);
        fallbackReport.SemesterCode.Should().Be("SP26");
    }

    // =========================================================================
    // ITEM 22: Shift/Session DTOs & sessions/tickets CRUD
    // =========================================================================

    [Fact(DisplayName = "M3-22-01: UpdateExamShiftStatus, BatchOpenExamShifts và ConfigureOfficialExamFollowUp trả về đầy đủ DTO fields")]
    public async Task Item22_ShiftAndSessionMutationHandlers_PopulateAllTrailingDtoFields()
    {
        await using var db = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo22");
        var deptHead = CreateUser(UserRole.DepartmentHead, "tbm22");
        var proctor = CreateUser(UserRole.Proctor, "pr22", fullName: "Giám thị 22");
        var examiner = CreateUser(UserRole.Lecturer, "gv22", fullName: "Giám khảo 22");
        var student1 = CreateUser(UserRole.Student, "sv22a", "SE220001");
        var student2 = CreateUser(UserRole.Student, "sv22b", "SE220002");
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Cross-Platform .NET", SemesterId = semester.Id, DepartmentHeadUserId = deptHead.Id, IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            ExamAttemptType = OfficialExamAttemptType.RetakeExam,
            Title = "RE PRN231",
            ExamDate = new DateOnly(2026, 11, 28),
            Status = "scheduled"
        };

        db.Users.AddRange(officer, deptHead, proctor, examiner, student1, student2);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(session);
        db.OfficialExamCandidates.AddRange(
            new OfficialExamCandidate { SessionId = session.Id, StudentId = student1.Id, ImportedBy = officer.Id },
            new OfficialExamCandidate { SessionId = session.Id, StudentId = student2.Id, ImportedBy = officer.Id });
        await db.SaveChangesAsync();

        // 1. BatchOpenExamShifts with MaxSeats = 30 on RE session
        var batchShiftsRes = await new BatchOpenExamShiftsCommandHandler(db)
            .Handle(new BatchOpenExamShiftsCommand(
                officer.Id,
                session.Id,
                "Ca Chiều",
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(1).AddHours(2),
                [new BatchOpenExamShiftRoomDto { RoomLab = "Lab 305", MaxSeats = 30, ProctorUserId = proctor.Id, ExaminerUserId = examiner.Id }]),
                CancellationToken.None);

        batchShiftsRes.IsSuccess.Should().BeTrue(batchShiftsRes.Error);
        var openedShift = batchShiftsRes.Value.Shifts.Single();
        openedShift.ExamAttemptType.Should().Be(OfficialExamAttemptType.RetakeExam);
        openedShift.RemainingSeats.Should().Be(30);
        openedShift.AssignedTicketsCount.Should().Be(0);
        openedShift.IsExamPublished.Should().BeFalse();
        openedShift.IsGradesPublished.Should().BeFalse();

        // Assign 1 ticket (for student1) with ExamSetId so UpdateExamShiftStatus can transition to in_progress
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, SetCode = "SET-22", Status = ExamSetStatus.HandedOver, IsActive = true };
        db.ExamSets.Add(examSet);
        db.StudentExamTickets.Add(new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = openedShift.Id,
            StudentId = student1.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Scheduled
        });
        await db.SaveChangesAsync();

        // 2. ConfigureOfficialExamFollowUp preserves CourseCode, CourseName, ExamAttemptType, CandidateCount, AllocatedCandidateCount
        var followUpRes = await new ConfigureOfficialExamFollowUpCommandHandler(db, new ConfigureOfficialExamFollowUpCommandValidator())
            .Handle(new ConfigureOfficialExamFollowUpCommand(deptHead.Id, session.Id, true, 3), CancellationToken.None);
        followUpRes.IsSuccess.Should().BeTrue(followUpRes.Error);
        followUpRes.Value.ExamAttemptType.Should().Be(OfficialExamAttemptType.RetakeExam);
        followUpRes.Value.CourseCode.Should().Be("PRN231");
        followUpRes.Value.CourseName.Should().Be("Cross-Platform .NET");
        followUpRes.Value.CandidateCount.Should().Be(2);
        followUpRes.Value.AllocatedCandidateCount.Should().Be(1);

        // 3. UpdateExamShiftStatus to in_progress returns IsExamPublished=true, ExamAttemptType=RE, AssignedTicketsCount=1, RemainingSeats=29
        var statusRes = await new UpdateExamShiftStatusCommandHandler(db)
            .Handle(new UpdateExamShiftStatusCommand(officer.Id, officer.Role, openedShift.Id, ExamShiftWindow.InProgress), CancellationToken.None);
        statusRes.IsSuccess.Should().BeTrue(statusRes.Error);
        statusRes.Value.IsExamPublished.Should().BeTrue();
        statusRes.Value.ExamAttemptType.Should().Be(OfficialExamAttemptType.RetakeExam);
        statusRes.Value.AssignedTicketsCount.Should().Be(1);
        statusRes.Value.RemainingSeats.Should().Be(29);
    }

    [Fact(DisplayName = "M3-22-02: PUT/DELETE /sessions/{id} và DELETE /tickets/{ticketId} thực thi đúng RBAC, Conflict/Locked guards và AuditLog")]
    public async Task Item22_SessionAndTicketCrud_EnforceAllGuardsAndAuditLogs()
    {
        await using var db = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo22c");
        var lecturer = CreateUser(UserRole.Lecturer, "gv22c");
        var student = CreateUser(UserRole.Student, "sv22c", "SE220003");
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWP391", Name = "SWP391", SemesterId = semester.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct A", TotalQuestions = 3, DurationMinutes = 20, IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            Title = "Initial Session",
            ExamDate = new DateOnly(2026, 12, 1),
            HasFollowUp = true,
            MaxFollowUpQuestions = 4,
            ExamInputMode = ExamInputMode.VoiceOnly,
            TranscriptBufferSeconds = 60,
            Status = "scheduled"
        };

        db.Users.AddRange(officer, lecturer, student);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.OfficialExamSessions.Add(session);
        await db.SaveChangesAsync();

        // Non-EO forbidden on PUT session
        var nonEoUpdate = await new UpdateOfficialExamSessionCommandHandler(db)
            .Handle(new UpdateOfficialExamSessionCommand(lecturer.Id, lecturer.Role, session.Id, structure.Id, "Hack", new DateOnly(2026, 12, 2), ExamInputMode.VoiceWithTranscriptEdit, 90), CancellationToken.None);
        nonEoUpdate.IsSuccess.Should().BeFalse();
        nonEoUpdate.Error.Should().StartWith("FORBIDDEN:");

        // EO PUT session succeeds, preserves HasFollowUp/MaxFollowUpQuestions, writes AuditLog
        var updateRes = await new UpdateOfficialExamSessionCommandHandler(db)
            .Handle(new UpdateOfficialExamSessionCommand(officer.Id, officer.Role, session.Id, structure.Id, "Updated Session", new DateOnly(2026, 12, 3), ExamInputMode.VoiceWithTranscriptEdit, 120), CancellationToken.None);
        updateRes.IsSuccess.Should().BeTrue(updateRes.Error);
        updateRes.Value.Title.Should().Be("Updated Session");
        updateRes.Value.ExamStructureId.Should().Be(structure.Id);
        updateRes.Value.ExamInputMode.Should().Be(ExamInputMode.VoiceWithTranscriptEdit);
        updateRes.Value.TranscriptBufferSeconds.Should().Be(120);
        updateRes.Value.HasFollowUp.Should().BeTrue();
        updateRes.Value.MaxFollowUpQuestions.Should().Be(4);

        // Add shift + ticket -> PUT/DELETE session must fail with 409 CONFLICT
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddDays(2),
            EndTime = DateTime.UtcNow.AddDays(2).AddHours(2),
            Status = "scheduled",
            IsExamPublished = false,
            MaxSeats = 24
        };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, SetCode = "SET-22C", Status = ExamSetStatus.HandedOver, IsActive = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Scheduled
        };
        db.RealExamSessionShifts.Add(shift);
        db.ExamSets.Add(examSet);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var blockedSessionUpdate = await new UpdateOfficialExamSessionCommandHandler(db)
            .Handle(new UpdateOfficialExamSessionCommand(officer.Id, officer.Role, session.Id, null, "Blocked", new DateOnly(2026, 12, 4), ExamInputMode.VoiceOnly, 60), CancellationToken.None);
        blockedSessionUpdate.IsSuccess.Should().BeFalse();
        blockedSessionUpdate.Error.Should().StartWith("CONFLICT:");

        var blockedSessionDelete = await new DeleteOfficialExamSessionCommandHandler(db)
            .Handle(new DeleteOfficialExamSessionCommand(officer.Id, officer.Role, session.Id), CancellationToken.None);
        blockedSessionDelete.IsSuccess.Should().BeFalse();
        blockedSessionDelete.Error.Should().StartWith("CONFLICT:");

        // Ticket locked -> 403 LOCKED:
        ticket.IsLocked = true;
        await db.SaveChangesAsync();
        var deleteLockedTicket = await new DeleteExamTicketCommandHandler(db)
            .Handle(new DeleteExamTicketCommand(officer.Id, officer.Role, ticket.Id), CancellationToken.None);
        deleteLockedTicket.IsSuccess.Should().BeFalse();
        deleteLockedTicket.Error.Should().StartWith("LOCKED:");

        // Ticket with device bound -> 409 CONFLICT:
        ticket.IsLocked = false;
        ticket.DeviceId = LaptopA;
        await db.SaveChangesAsync();
        var deleteBoundTicket = await new DeleteExamTicketCommandHandler(db)
            .Handle(new DeleteExamTicketCommand(officer.Id, officer.Role, ticket.Id), CancellationToken.None);
        deleteBoundTicket.IsSuccess.Should().BeFalse();
        deleteBoundTicket.Error.Should().StartWith("CONFLICT:");

        // Unused Scheduled ticket -> DELETE succeeds and preserves ExamSet
        ticket.DeviceId = null;
        await db.SaveChangesAsync();
        var deleteTicketOk = await new DeleteExamTicketCommandHandler(db)
            .Handle(new DeleteExamTicketCommand(officer.Id, officer.Role, ticket.Id), CancellationToken.None);
        deleteTicketOk.IsSuccess.Should().BeTrue(deleteTicketOk.Error);
        (await db.StudentExamTickets.AnyAsync(t => t.Id == ticket.Id)).Should().BeFalse();
        (await db.ExamSets.AnyAsync(es => es.Id == examSet.Id)).Should().BeTrue();

        // Remove shift, then DELETE session succeeds and writes AuditLog
        db.RealExamSessionShifts.Remove(shift);
        await db.SaveChangesAsync();
        var deleteSessionOk = await new DeleteOfficialExamSessionCommandHandler(db)
            .Handle(new DeleteOfficialExamSessionCommand(officer.Id, officer.Role, session.Id), CancellationToken.None);
        deleteSessionOk.IsSuccess.Should().BeTrue(deleteSessionOk.Error);
        (await db.OfficialExamSessions.AnyAsync(s => s.Id == session.Id)).Should().BeFalse();
        (await db.AuditLogs.AnyAsync(a => a.Action == "OFFICIAL_EXAM_SESSION_DELETED" && a.EntityId == session.Id)).Should().BeTrue();
    }

    // =========================================================================
    // ITEM 23: Proctor Violation Whitelist & Case Normalization
    // =========================================================================

    [Fact(DisplayName = "M3-23-01: RecordProctorViolation từ chối EventType/Severity ngoài whitelist và Details > 2000 ký tự, đồng thời hỗ trợ 4 hằng số FE Proctor & chuẩn hóa hoa/thường")]
    public async Task Item23_ProctorViolation_EnforcesWhitelistAndNormalization_WhilePreservingFeConstants()
    {
        await using var db = CreateDbContext();
        var proctor = CreateUser(UserRole.Proctor, "pr23");
        var student = CreateUser(UserRole.Student, "sv23", "SE230001");
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            ProctorUserId = proctor.Id,
            Status = ExamShiftWindow.InProgress
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

        var validator = new RecordProctorViolationCommandValidator();
        var handler = new RecordProctorViolationCommandHandler(db);

        // 1. Unknown EventType rejected by both Validator and Handler
        var invalidEventCmd = new RecordProctorViolationCommand(proctor.Id, shift.Id, ticket.Id, student.Id, "UNKNOWN_CUSTOM_EVENT", "high", "details");
        validator.Validate(invalidEventCmd).IsValid.Should().BeFalse();
        var invalidEventRes = await handler.Handle(invalidEventCmd, CancellationToken.None);
        invalidEventRes.IsSuccess.Should().BeFalse();
        invalidEventRes.Error.Should().StartWith("VALIDATION:");

        // 2. Invalid Severity rejected before DB (prevents PostgreSQL 23514)
        var invalidSevCmd = new RecordProctorViolationCommand(proctor.Id, shift.Id, ticket.Id, student.Id, AntiCheatEventType.TabSwitch, "EXTREME", "details");
        validator.Validate(invalidSevCmd).IsValid.Should().BeFalse();
        var invalidSevRes = await handler.Handle(invalidSevCmd, CancellationToken.None);
        invalidSevRes.IsSuccess.Should().BeFalse();
        invalidSevRes.Error.Should().StartWith("VALIDATION:");

        // 3. Details > 2000 chars rejected
        var longDetailsCmd = new RecordProctorViolationCommand(proctor.Id, shift.Id, ticket.Id, student.Id, AntiCheatEventType.TabSwitch, "high", new string('a', 2001));
        validator.Validate(longDetailsCmd).IsValid.Should().BeFalse();
        var longDetailsRes = await handler.Handle(longDetailsCmd, CancellationToken.None);
        longDetailsRes.IsSuccess.Should().BeFalse();
        longDetailsRes.Error.Should().StartWith("VALIDATION:");

        // 4. All 4 FE Proctor dropdown constants + uppercase severity ("HIGH") succeed and normalize cleanly
        var feEventTypes = new[]
        {
            AntiCheatEventType.ProctorReportedViolation,
            AntiCheatEventType.TabSwitchOrBlur,
            AntiCheatEventType.UnauthorizedDevice,
            AntiCheatEventType.IdentityMismatch
        };
        foreach (var ev in feEventTypes)
        {
            var cmd = new RecordProctorViolationCommand(proctor.Id, shift.Id, ticket.Id, student.Id, ev.ToLowerInvariant(), "HIGH", "Giám thị lập biên bản.");
            validator.Validate(cmd).IsValid.Should().BeTrue();
            var res = await handler.Handle(cmd, CancellationToken.None);
            res.IsSuccess.Should().BeTrue(res.Error);
            res.Value.EventType.Should().Be(ev);
            res.Value.Severity.Should().Be(AntiCheatSeverity.High);
        }

        // Kiosk AntiCheatEventType.All remains strict (12 events)
        AntiCheatEventType.All.Should().HaveCount(12);
        AntiCheatEventType.All.Should().NotContain(AntiCheatEventType.ProctorReportedViolation);
    }

    // =========================================================================
    // ITEM 24: Appeals Allocation Proposal Course Filter & Quota
    // =========================================================================

    [Fact(DisplayName = "M3-24-01: GetAppealAllocationProposal lọc đúng giảng viên theo môn (CourseLecturers + Classes), đa môn độc lập, và tính ExistingActiveCount/RemainingQuota")]
    public async Task Item24_GetAppealAllocationProposal_FiltersByCourseAndPopulatesRemainingQuota()
    {
        await using var db = CreateDbContext();
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo24");
        var studentA = CreateUser(UserRole.Student, "sv24a", "SE240001");
        var studentB = CreateUser(UserRole.Student, "sv24b", "SE240002");

        var examinerA = CreateUser(UserRole.Lecturer, "gv_examiner_a", fullName: "Giám khảo A");
        var courseLecturerA = CreateUser(UserRole.Lecturer, "gv_course_a", fullName: "GV Môn A (CourseLecturer)");
        var classLecturerB = CreateUser(UserRole.Lecturer, "gv_class_b", fullName: "GV Môn B (Class.LecturerId)");
        var outOfCourseLecturer = CreateUser(UserRole.Lecturer, "gv_unrelated", fullName: "GV Ngoài Môn");

        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", IsActive = true };

        var sessionA = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseA.Id, Title = "FE PRN231", ExamDate = new DateOnly(2026, 11, 20), Status = "published" };
        var sessionB = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseB.Id, Title = "FE SWD392", ExamDate = new DateOnly(2026, 11, 21), Status = "published" };

        var shiftA = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = sessionA.Id, ShiftName = "Ca A", RoomLab = "Lab 1", ExaminerUserId = examinerA.Id, Status = "completed" };
        var shiftB = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = sessionB.Id, ShiftName = "Ca B", RoomLab = "Lab 2", ExaminerUserId = Guid.NewGuid(), Status = "completed" };

        var ticketA = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shiftA.Id, StudentId = studentA.Id, SeatNumber = 1, Status = ExamTicketStatus.Published, GradesPublishedAt = DateTime.UtcNow, IsLocked = true };
        var ticketB = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shiftB.Id, StudentId = studentB.Id, SeatNumber = 1, Status = ExamTicketStatus.Published, GradesPublishedAt = DateTime.UtcNow, IsLocked = true };

        var appealA = new AppealRequest { Id = Guid.NewGuid(), TicketId = ticketA.Id, StudentId = studentA.Id, SessionId = sessionA.Id, Reason = "Phúc khảo A", Status = AppealStatus.SubmittedToExamOffice, CreatedAt = DateTime.UtcNow.AddMinutes(-2) };
        var appealB = new AppealRequest { Id = Guid.NewGuid(), TicketId = ticketB.Id, StudentId = studentB.Id, SessionId = sessionB.Id, Reason = "Phúc khảo B", Status = AppealStatus.SubmittedToExamOffice, CreatedAt = DateTime.UtcNow.AddMinutes(-1) };

        // Existing active appeals for courseLecturerA (2 active: AssignedToLecturer + RegradedByLecturer; 1 closed Approved ignored)
        db.AppealRequests.AddRange(
            new AppealRequest { Id = Guid.NewGuid(), TicketId = Guid.NewGuid(), StudentId = Guid.NewGuid(), SessionId = Guid.NewGuid(), Reason = "Active 1", Status = AppealStatus.AssignedToLecturer, AssignedTo = courseLecturerA.Id },
            new AppealRequest { Id = Guid.NewGuid(), TicketId = Guid.NewGuid(), StudentId = Guid.NewGuid(), SessionId = Guid.NewGuid(), Reason = "Active 2", Status = AppealStatus.RegradedByLecturer, AssignedTo = courseLecturerA.Id },
            new AppealRequest { Id = Guid.NewGuid(), TicketId = Guid.NewGuid(), StudentId = Guid.NewGuid(), SessionId = Guid.NewGuid(), Reason = "Closed", Status = AppealStatus.Approved, AssignedTo = courseLecturerA.Id });

        db.Users.AddRange(officer, studentA, studentB, examinerA, courseLecturerA, classLecturerB, outOfCourseLecturer);
        db.Courses.AddRange(courseA, courseB);
        db.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = courseA.Id, LecturerUserId = examinerA.Id, AssignedByUserId = officer.Id, IsActive = true },
            new CourseLecturer { CourseId = courseA.Id, LecturerUserId = courseLecturerA.Id, AssignedByUserId = officer.Id, IsActive = true });
        db.Classes.Add(new Class { Id = Guid.NewGuid(), CourseId = courseB.Id, Code = "SE1801", Name = "SE1801", LecturerId = classLecturerB.Id, IsActive = true });
        db.OfficialExamSessions.AddRange(sessionA, sessionB);
        db.RealExamSessionShifts.AddRange(shiftA, shiftB);
        db.StudentExamTickets.AddRange(ticketA, ticketB);
        db.AppealRequests.AddRange(appealA, appealB);
        await db.SaveChangesAsync();

        var handler = new GetAppealAllocationProposalQueryHandler(db);
        var proposalRes = await handler.Handle(
            new GetAppealAllocationProposalQuery(SessionId: null, officer.Id, officer.Role),
            CancellationToken.None);

        proposalRes.IsSuccess.Should().BeTrue(proposalRes.Error);
        var dto = proposalRes.Value;
        dto.TotalAppeals.Should().Be(2);
        dto.TotalLecturers.Should().Be(3);

        // Out-of-course lecturer MUST NOT appear in LecturerSummaries or Items
        dto.LecturerSummaries.Should().NotContain(s => s.LecturerId == outOfCourseLecturer.Id);

        // Multi-course routing: appealA -> courseLecturerA, appealB -> classLecturerB
        dto.Items.Single(i => i.AppealId == appealA.Id).ProposedLecturerId.Should().Be(courseLecturerA.Id);
        dto.Items.Single(i => i.AppealId == appealB.Id).ProposedLecturerId.Should().Be(classLecturerB.Id);

        // Quota fields accurately calculated
        const int maxAppealsPerLecturer = 24;
        var summaryA = dto.LecturerSummaries.Single(s => s.LecturerId == courseLecturerA.Id);
        summaryA.ExistingActiveCount.Should().Be(2);
        summaryA.RemainingQuota.Should().Be(maxAppealsPerLecturer - 2);
        summaryA.AssignedCount.Should().Be(1);

        var summaryB = dto.LecturerSummaries.Single(s => s.LecturerId == classLecturerB.Id);
        summaryB.ExistingActiveCount.Should().Be(0);
        summaryB.RemainingQuota.Should().Be(maxAppealsPerLecturer);
        summaryB.AssignedCount.Should().Be(1);
    }

    [Fact(DisplayName = "M3-22-03: OfficialExamsController UpdateSession/DeleteSession/DeleteTicket trả đúng 200/204/403/409")]
    public async Task Item22_OfficialExamsController_CrudActions_ReturnExpectedStatusCodes()
    {
        var senderMock = new Mock<ISender>();
        var actorId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var controller = new OfficialExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(actorId, UserRole.ExaminationOfficer) }
            }
        };

        var updatedSession = new OfficialExamSessionResponse(
            sessionId,
            Guid.NewGuid(),
            null,
            "Updated Title",
            new DateOnly(2026, 12, 1),
            false,
            2,
            ExamInputMode.VoiceOnly,
            60,
            "scheduled");

        senderMock.Setup(s => s.Send(It.IsAny<UpdateOfficialExamSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<OfficialExamSessionResponse>.Success(updatedSession));
        var putRes = await controller.UpdateSession(sessionId, new UpdateOfficialExamSessionRequest
        {
            Title = "Updated Title",
            ExamDate = new DateOnly(2026, 12, 1),
            ExamInputMode = ExamInputMode.VoiceOnly,
            TranscriptBufferSeconds = 60
        }, CancellationToken.None);
        putRes.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(updatedSession);

        senderMock.Setup(s => s.Send(It.IsAny<DeleteOfficialExamSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result.Success());
        (await controller.DeleteSession(sessionId, CancellationToken.None)).Should().BeOfType<NoContentResult>();

        senderMock.Setup(s => s.Send(It.IsAny<DeleteExamTicketCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result.Failure("LOCKED: Vé đã khóa."));
        var lockedDel = await controller.DeleteTicket(ticketId, CancellationToken.None);
        lockedDel.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        senderMock.Setup(s => s.Send(It.IsAny<DeleteExamTicketCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result.Success());
        (await controller.DeleteTicket(ticketId, CancellationToken.None)).Should().BeOfType<NoContentResult>();
    }
}
