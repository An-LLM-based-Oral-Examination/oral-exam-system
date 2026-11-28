using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Moq;
using OralExamination.API.Configuration;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;
using OralExamination.Application.Features.OfficialExams.Queries.VerifyGradeCertificate;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using OralExamination.Infrastructure.Reporting;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class PadesPdfSigningAndCertificateLookupTests
{
    private static OralExamDbContext CreateDbContext(bool withLockInterceptor = false)
    {
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (withLockInterceptor)
        {
            builder.AddInterceptors(new OneWayLockInterceptor());
        }
        return new OralExamDbContext(builder.Options);
    }

    private static async Task<(User Officer, User Examiner, User Proctor, User Student, RealExamSessionShift Shift, StudentExamTicket Ticket, ExamQuestionSubmission Sub, LecturerAudit Audit)> SeedCompletedShiftAsync(
        OralExamDbContext context)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Kiến trúc Phần mềm", Credits = 3, SemesterId = semester.Id, Semester = semester, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.pki@fpt.edu.vn", FullName = "Cán bộ Khảo thí", Role = UserRole.ExaminationOfficer, IsActive = true };
        var examiner = new User { Id = Guid.NewGuid(), Email = "examiner.pki@fpt.edu.vn", FullName = "TS. Giám khảo Độc lập", Role = UserRole.Lecturer, IsActive = true };
        var proctor = new User { Id = Guid.NewGuid(), Email = "proctor.pki@fpt.edu.vn", FullName = "ThS. Giám thị Phòng Lab", Role = UserRole.Proctor, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.pki@fpt.edu.vn", StudentCode = "SE179001", FullName = "Nguyễn Hoàng Việt", Role = UserRole.Student, IsActive = true };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Course = course,
            SemesterId = semester.Id,
            Semester = semester,
            ExamAttemptType = OfficialExamAttemptType.FinalExam,
            Title = "Kỳ thi Vấn đáp Cuối kỳ SWD392",
            ExamDate = new DateOnly(2026, 11, 20),
            Status = "scheduled",
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Session = session,
            ShiftName = "Ca 1 - Phòng 301",
            RoomLab = "Lab 301",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddMinutes(-15),
            ProctorUserId = proctor.Id,
            ProctorUser = proctor,
            ExaminerUserId = examiner.Id,
            ExaminerUser = examiner,
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            Shift = shift,
            StudentId = student.Id,
            Student = student,
            SeatNumber = 1,
            Status = ExamTicketStatus.Audited,
            IsLocked = false
        };
        var sub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Trình bày Clean Architecture và CQRS.",
            AiScore = 8.0m,
            FinalScore = 8.5m,
            GradingStatus = "audited"
        };
        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = examiner.Id,
            OriginalAiScore = 8.0m,
            AuditedScore = 8.5m,
            OverrideReason = "Giải thích rõ nguyên lý phụ thuộc và kiểm thử tách lớp.",
            IsLocked = false
        };

        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Users.AddRange(officer, examiner, proctor, student);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(sub);
        context.LecturerAudits.Add(audit);
        await context.SaveChangesAsync();

        return (officer, examiner, proctor, student, shift, ticket, sub, audit);
    }

    [Fact]
    public async Task PadesPdfSigner_ProducesCryptographicallyValidCadesSignature_AndDetectsSingleByteTamper()
    {
        using var context = CreateDbContext();
        var (officer, _, _, _, shift, _, _, _) = await SeedCompletedShiftAsync(context);
        using var signer = new PadesPdfDigitalSigner();

        var publishResult = await new PublishGradesCommandHandler(context, signer)
            .Handle(new PublishGradesCommand(shift.Id, officer.Id), CancellationToken.None);
        publishResult.IsSuccess.Should().BeTrue();

        var exporter = new ExamGradeReportExporter(signer);
        var exportResult = await new ExportShiftPdfQueryHandler(context, exporter, signer)
            .Handle(new ExportShiftPdfQuery(shift.Id, officer.Id), CancellationToken.None);

        exportResult.IsSuccess.Should().BeTrue();
        exportResult.Value.CertificateThumbprint.Should().Be(signer.GetActiveCertificateThumbprint());
        exportResult.Value.CertificateThumbprint.Should().HaveLength(64);

        var pdfBytes = exportResult.Value.FileContents;
        Encoding.ASCII.GetString(pdfBytes, 0, 5).Should().Be("%PDF-");

        var pdfText = Encoding.Latin1.GetString(pdfBytes);
        pdfText.Should().Contain("/SubFilter /ETSI.CAdES.detached");
        pdfText.Should().Contain("/ByteRange");
        pdfText.Should().Contain("/SigFlags 3");

        // Cryptographic verification via SignedCms.CheckSignature(true)
        var verified = PadesPdfDigitalSigner.VerifyEmbeddedSignature(pdfBytes);
        verified.IsValid.Should().BeTrue($"PAdES signature must validate cleanly, error={verified.Error}");
        verified.CertificateThumbprint.Should().Be(signer.GetActiveCertificateThumbprint());
        verified.SignerName.Should().Contain("FPTU Oral Examination Board");

        // Flip 1 byte in the signed PDF body (well before /Contents) -> SignedCms MUST reject!
        var tamperedPdf = (byte[])pdfBytes.Clone();
        var tamperOffset = Math.Min(64, tamperedPdf.Length / 4);
        tamperedPdf[tamperOffset] ^= 0xFF;

        var tamperedOutcome = PadesPdfDigitalSigner.VerifyEmbeddedSignature(tamperedPdf);
        tamperedOutcome.IsValid.Should().BeFalse("flipping a single byte in the signed PDF must invalidate the CMS signature");
    }

    [Fact]
    public void PadesPdfSigner_ProductionWithoutCertificate_FailsClosed_AndBase64PfxLoadsCleanly()
    {
        var prodMissingConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "true"
            })
            .Build();

        using var prodMissingSigner = new PadesPdfDigitalSigner(prodMissingConfig);
        Action prodAct = () => _ = prodMissingSigner.GetActiveCertificateThumbprint();
        prodAct.Should().Throw<InvalidOperationException>().WithMessage("*FAIL CLOSED*");

        // Explicit Base64 PFX with password succeeds even with RequireCertificate = true
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=FPTU Prod PKI Test Signer, O=FPT University, C=VN",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        var expectedThumbprint = Convert.ToHexString(cert.GetCertHash(HashAlgorithmName.SHA256)).ToLowerInvariant();
        var pfxPassword = "Pki-Test-Password-2026!";
        var pfxBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Pfx, pfxPassword));

        var prodValidConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "true",
                ["PdfSigning:CertificateBase64"] = pfxBase64,
                ["PdfSigning:CertificatePassword"] = pfxPassword
            })
            .Build();

        using var prodSigner = new PadesPdfDigitalSigner(prodValidConfig);
        prodSigner.GetActiveCertificateThumbprint().Should().Be(expectedThumbprint);
        prodSigner.GetSignerDisplayName().Should().Be("FPTU Prod PKI Test Signer");
    }

    [Fact]
    public async Task PublishGrades_AndApprovedAppeal_PersistAndUpdateHashThumbprintSignedAt_AndO1VerifyDetectsDbTamper()
    {
        await using var context = CreateDbContext(withLockInterceptor: true);
        var (officer, _, _, student, shift, ticket, sub, _) = await SeedCompletedShiftAsync(context);
        var appealLecturer = new User { Id = Guid.NewGuid(), Email = "regrader.pki@fpt.edu.vn", FullName = "GV Phúc khảo", Role = UserRole.Lecturer, IsActive = true };
        context.Users.Add(appealLecturer);
        await context.SaveChangesAsync();

        using var signer = new PadesPdfDigitalSigner();

        // 1. PublishGrades persists CertificateHash, CertificateThumbprint, CertificateSignedAt
        var publish = await new PublishGradesCommandHandler(context, signer)
            .Handle(new PublishGradesCommand(shift.Id, officer.Id), CancellationToken.None);
        publish.IsSuccess.Should().BeTrue();

        var publishedShift = await context.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
        publishedShift.CertificateHash.Should().HaveLength(64);
        publishedShift.CertificateThumbprint.Should().Be(signer.GetActiveCertificateThumbprint());
        publishedShift.CertificateSignedAt.Should().NotBeNull();

        // 2. O(1) VerifyGradeCertificateQueryHandler matches indexed hash & returns Thumbprint/SignedAt/IsTampered=false
        var verifyHandler = new VerifyGradeCertificateQueryHandler(context);
        var validRes = await verifyHandler.Handle(new VerifyGradeCertificateQuery(publishedShift.CertificateHash!), CancellationToken.None);
        validRes.IsSuccess.Should().BeTrue();
        validRes.Value.Status.Should().Be("VALID");
        validRes.Value.IsTampered.Should().BeFalse();
        validRes.Value.CertificateThumbprint.Should().Be(signer.GetActiveCertificateThumbprint());
        validRes.Value.CertificateSignedAt.Should().Be(publishedShift.CertificateSignedAt);
        validRes.Value.StudentScores.Should().ContainSingle(s => s.StudentCode == "SE179001" && s.FinalScore == 8.5m);

        // 3. Approved appeal recomputes CertificateHash + CertificateThumbprint + CertificateSignedAt atomically
        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = shift.SessionId,
            SubmissionId = sub.Id,
            Reason = "Phúc khảo nâng điểm câu hỏi 1",
            Status = AppealStatus.RegradedByLecturer,
            AssignedTo = appealLecturer.Id,
            OriginalScore = 8.5m,
            ProposedScore = 9.5m
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var appealRes = await new ReviewAppealDecisionCommandHandler(context, signer)
            .Handle(new ReviewAppealDecisionCommand(appeal.Id, officer.Id, AppealStatus.Approved, 9.5m, "Chấp nhận điểm chấm lại 9.50"), CancellationToken.None);
        appealRes.IsSuccess.Should().BeTrue();

        var afterAppealShift = await context.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
        afterAppealShift.CertificateHash.Should().HaveLength(64).And.NotBe(publishedShift.CertificateHash);
        afterAppealShift.CertificateThumbprint.Should().Be(signer.GetActiveCertificateThumbprint());

        var newVerify = await verifyHandler.Handle(new VerifyGradeCertificateQuery(afterAppealShift.CertificateHash!), CancellationToken.None);
        newVerify.IsSuccess.Should().BeTrue();
        newVerify.Value.StudentScores.Single().FinalScore.Should().Be(9.5m);

        // Old pre-appeal hash is now invalid
        var oldVerify = await verifyHandler.Handle(new VerifyGradeCertificateQuery(publishedShift.CertificateHash!), CancellationToken.None);
        oldVerify.IsSuccess.Should().BeFalse();

        // 4. Direct DB tamper (bypassing lock interceptor on separate context or raw SQL simulation) -> live re-hash catches TAMPERED!
        await using var tamperDb = new OralExamDbContext(new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var (eo2, _, _, _, shift2, _, _, audit2) = await SeedCompletedShiftAsync(tamperDb);
        var pub2 = await new PublishGradesCommandHandler(tamperDb, signer)
            .Handle(new PublishGradesCommand(shift2.Id, eo2.Id), CancellationToken.None);
        pub2.IsSuccess.Should().BeTrue();
        var originalHash = (await tamperDb.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift2.Id)).CertificateHash!;

        // Simulate raw SQL tampering of score while leaving shift.CertificateHash untouched
        audit2.AuditedScore = 10.0m;
        await tamperDb.SaveChangesAsync();

        var tamperVerify = await new VerifyGradeCertificateQueryHandler(tamperDb)
            .Handle(new VerifyGradeCertificateQuery(originalHash), CancellationToken.None);
        tamperVerify.IsSuccess.Should().BeFalse();
        tamperVerify.Error.Should().Contain("TAMPERED");
    }

    [Fact]
    public async Task ExportShiftPdf_EmitsThumbprintHeader_VerifyHasPublicRateLimit_AndM4SemesterIdIsNonNull()
    {
        // 1. ExportShiftPdf emits X-Certificate-Hash & X-Signature-Thumbprint
        var senderMock = new Mock<ISender>();
        var shiftId = Guid.NewGuid();
        var expectedHash = new string('a', 64);
        var expectedThumbprint = new string('b', 64);
        senderMock.Setup(s => s.Send(It.IsAny<ExportShiftPdfQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExportShiftFileResponseDto>.Success(new ExportShiftFileResponseDto
            {
                FileContents = Encoding.ASCII.GetBytes("%PDF-1.4\n"),
                ContentType = "application/pdf",
                FileName = "report.pdf",
                CertificateHash = expectedHash,
                CertificateThumbprint = expectedThumbprint
            }));

        var controller = new OfficialExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Role, UserRole.ExaminationOfficer)
                    ], "Test"))
                }
            }
        };

        var fileResult = await controller.ExportShiftPdf(shiftId, null, CancellationToken.None);
        fileResult.Should().BeOfType<FileContentResult>();
        controller.Response.Headers["X-Certificate-Hash"].ToString().Should().Be(expectedHash);
        controller.Response.Headers["X-Signature-Thumbprint"].ToString().Should().Be(expectedThumbprint);

        // 2. VerificationController has [EnableRateLimiting("public-verify")] and default 30 permits/min
        var verifyMethod = typeof(VerificationController).GetMethod(nameof(VerificationController.VerifyGradeCertificate))!;
        var rateAttr = verifyMethod.GetCustomAttribute<EnableRateLimitingAttribute>();
        rateAttr.Should().NotBeNull();
        rateAttr!.PolicyName.Should().Be(OralExamRateLimiterExtensions.PublicVerifyPolicy);
        new AbuseProtectionOptions().PublicVerifyPermits.Should().Be(30);

        // 3. M4 Contract: OfficialExamSession.SemesterId is non-null + unfiltered unique index + redundant index removed
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var sessionEntity = model.FindEntityType(typeof(OfficialExamSession))!;
        sessionEntity.FindProperty(nameof(OfficialExamSession.SemesterId))!.IsNullable.Should().BeFalse();

        var attemptUniqueIdx = sessionEntity.GetIndexes()
            .Single(i => i.GetDatabaseName() == "uq_official_exam_sessions_semester_course_attempt");
        attemptUniqueIdx.IsUnique.Should().BeTrue();
        attemptUniqueIdx.GetFilter().Should().BeNull("M4 upgrades partial unique index to unfiltered UNIQUE(semester_id, course_id, exam_attempt_type)");

        sessionEntity.GetIndexes()
            .Any(i => i.GetDatabaseName() == "ix_official_exam_sessions_semester_course")
            .Should().BeFalse("M4 removes redundant ix_official_exam_sessions_semester_course covered by unique index");
    }
}
