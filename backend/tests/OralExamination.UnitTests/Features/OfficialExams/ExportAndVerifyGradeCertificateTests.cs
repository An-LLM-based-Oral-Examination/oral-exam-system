using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftExcel;
using OralExamination.Application.Features.OfficialExams.Queries.ExportShiftPdf;
using OralExamination.Application.Features.OfficialExams.Queries.VerifyGradeCertificate;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class ExportAndVerifyGradeCertificateTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null, bool withLockInterceptor = false)
    {
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString());

        if (withLockInterceptor)
        {
            builder.AddInterceptors(new OneWayLockInterceptor());
        }

        return new OralExamDbContext(builder.Options);
    }

    private static async Task<(RealExamSessionShift Shift, StudentExamTicket Ticket, LecturerAudit Audit, AppealRequest Appeal, User DeptHead)> SeedShiftDataAsync(
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

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            Semester = semester,
            IsActive = true
        };

        var proctor = new User
        {
            Id = Guid.NewGuid(),
            Email = "proctor.lab@fpt.edu.vn",
            FullName = "ThS. Giám Thị Phòng Lab",
            Role = UserRole.Proctor,
            IsActive = true
        };

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "depthead.se@fpt.edu.vn",
            FullName = "TS. Trưởng Bộ Môn Kỹ Thuật Phần Mềm",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "se170123@fpt.edu.vn",
            FullName = "Nguyễn Quang Thành",
            StudentCode = "SE170123",
            Role = UserRole.Student,
            IsActive = true
        };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Course = course,
            ExamStructureId = Guid.NewGuid(),
            Title = "Kỳ Thi Vấn Đáp Cuối Kỳ PRN231",
            ExamDate = new DateOnly(2026, 10, 20),
            Status = "published"
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Session = session,
            ShiftName = "Ca 1 - Sáng",
            RoomLab = "Lab 204",
            ProctorUserId = proctor.Id,
            ProctorUser = proctor,
            StartTime = new DateTime(2026, 10, 20, 7, 30, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 10, 20, 9, 30, 0, DateTimeKind.Utc),
            Status = "completed"
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            Shift = shift,
            StudentId = student.Id,
            Student = student,
            SeatNumber = 1,
            IpAddress = "10.24.4.101",
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AudioR2Url = "https://r2.fpt.edu.vn/01_SE170123.webm",
            AudioHashSha256 = new string('a', 64),
            TranscriptWhisper = "Clean Architecture tách biệt Domain khỏi Infrastructure.",
            AiScore = 8.50m,
            FinalScore = 9.00m,
            GradingStatus = "audited"
        };

        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = deptHead.Id,
            OriginalAiScore = 8.50m,
            AuditedScore = 9.00m,
            OverrideReason = "Thí sinh trả lời xuất sắc câu hỏi phụ về MediatR Pipeline.",
            IsLocked = true
        };

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
            SubmissionId = submission.Id,
            Reason = "Xin phúc khảo nâng điểm câu 2",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id,
            OriginalScore = 9.00m
        };

        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Users.AddRange(proctor, deptHead, student);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.LecturerAudits.Add(audit);
        context.AppealRequests.Add(appeal);

        await context.SaveChangesAsync();
        return (shift, ticket, audit, appeal, deptHead);
    }

    [Fact(DisplayName = "BE-V01: ExportShiftExcelQueryHandler sinh file .xlsx chuẩn OpenXML kèm mã băm chữ ký số SHA-256")]
    public async Task ExportShiftExcel_Should_Generate_Valid_Xlsx_With_CertificateHash()
    {
        using var context = CreateDbContext();
        var (shift, _, _, _, _) = await SeedShiftDataAsync(context);

        var handler = new ExportShiftExcelQueryHandler(context);
        var result = await handler.Handle(new ExportShiftExcelQuery(shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().EndWith(".xlsx");
        result.Value.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.Value.CertificateHash.Should().HaveLength(64);
        result.Value.FileContents.Should().NotBeEmpty();

        using var ms = new MemoryStream(result.Value.FileContents);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).Should().Contain(new[]
        {
            "[Content_Types].xml",
            "xl/workbook.xml",
            "xl/worksheets/sheet1.xml"
        });

        var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml")!;
        using var reader = new StreamReader(sheetEntry.Open(), Encoding.UTF8);
        var xml = await reader.ReadToEndAsync();
        xml.Should().Contain("SE170123");
        xml.Should().Contain("Nguyễn Quang Thành");
        xml.Should().Contain("9.00");
        xml.Should().Contain(result.Value.CertificateHash);
    }

    [Fact(DisplayName = "BE-V01: ExportShiftPdfQueryHandler sinh file PDF 1.4 chuẩn kèm chữ ký số SHA-256")]
    public async Task ExportShiftPdf_Should_Generate_Valid_Pdf_With_Digital_Seal()
    {
        using var context = CreateDbContext();
        var (shift, _, _, _, _) = await SeedShiftDataAsync(context);

        var handler = new ExportShiftPdfQueryHandler(context);
        var result = await handler.Handle(new ExportShiftPdfQuery(shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().EndWith(".pdf");
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.CertificateHash.Should().HaveLength(64);

        var pdfText = Encoding.ASCII.GetString(result.Value.FileContents);
        pdfText.Should().StartWith("%PDF-1.4");
        pdfText.Should().Contain("SE170123");
        pdfText.Should().Contain("Nguyen Quang Thanh");
        pdfText.Should().Contain(result.Value.CertificateHash);
        pdfText.TrimEnd().Should().EndWith("%%EOF");
    }

    [Fact(DisplayName = "BE-V02: VerifyGradeCertificateQueryHandler xác thực thành công mã băm QR và phát hiện giả mạo điểm")]
    public async Task VerifyGradeCertificate_Should_Return_Valid_And_Detect_Tampering()
    {
        using var context = CreateDbContext();
        var (shift, _, audit, _, _) = await SeedShiftDataAsync(context);

        var excelHandler = new ExportShiftExcelQueryHandler(context);
        var exportResult = await excelHandler.Handle(new ExportShiftExcelQuery(shift.Id), CancellationToken.None);
        var certificateHash = exportResult.Value.CertificateHash;

        var verifyHandler = new VerifyGradeCertificateQueryHandler(context);
        var verifyResult = await verifyHandler.Handle(new VerifyGradeCertificateQuery(certificateHash), CancellationToken.None);

        verifyResult.IsSuccess.Should().BeTrue();
        verifyResult.Value.Status.Should().Be("VALID");
        verifyResult.Value.CourseCode.Should().Be("PRN231");
        verifyResult.Value.ShiftId.Should().Be(shift.Id);
        verifyResult.Value.TotalStudents.Should().Be(1);
        verifyResult.Value.IsFullyLocked.Should().BeTrue();
        verifyResult.Value.StudentScores[0].StudentCode.Should().Be("SE170123");
        verifyResult.Value.StudentScores[0].FinalScore.Should().Be(9.00m);

        // Giả lập sửa điểm trong DB -> Mã băm cũ phải lập tức mất hiệu lực (Tamper Detection)
        audit.AuditedScore = 10.00m;
        await context.SaveChangesAsync();

        var tamperedVerify = await verifyHandler.Handle(new VerifyGradeCertificateQuery(certificateHash), CancellationToken.None);
        tamperedVerify.IsSuccess.Should().BeFalse();
        tamperedVerify.Error.Should().Contain("Không tìm thấy chứng thư bảng điểm hợp lệ");
    }

    [Fact(DisplayName = "MF-04 OneWayLockInterceptor: Cho phép cập nhật LecturerAudit.AuditedScore khi AppealRequest được APPROVED trong cùng giao dịch")]
    public async Task OneWayLockInterceptor_Should_Allow_AuditScore_Update_When_Appeal_Is_Approved()
    {
        var dbName = Guid.NewGuid().ToString();
        using var seedContext = CreateDbContext(dbName, withLockInterceptor: false);
        var (_, _, audit, appeal, deptHead) = await SeedShiftDataAsync(seedContext);

        using var activeContext = CreateDbContext(dbName, withLockInterceptor: true);
        var reviewHandler = new ReviewAppealDecisionCommandHandler(activeContext);

        var result = await reviewHandler.Handle(new ReviewAppealDecisionCommand(
            AppealId: appeal.Id,
            ReviewerId: deptHead.Id,
            Decision: AppealStatus.Approved,
            ProposedScore: 9.75m,
            ReviewNotes: "Hội đồng phúc khảo thống nhất nâng điểm lên 9.75 sau khi nghe lại băng ghi âm R2."
        ), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedAudit = await activeContext.LecturerAudits.FindAsync(audit.Id);
        updatedAudit!.AuditedScore.Should().Be(9.75m);
        updatedAudit.IsLocked.Should().BeTrue();
    }

    [Fact(DisplayName = "BE-V01 & BE-V02 Controllers: OfficialExamsController và VerificationController trả đúng FileResult và 200/404")]
    public async Task Controllers_Should_Return_Expected_File_And_Verification_Responses()
    {
        var senderMock = new Mock<ISender>();
        var officialController = new OfficialExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var emptyResult = await officialController.ExportShiftExcel(null, null, CancellationToken.None);
        emptyResult.Should().BeOfType<BadRequestObjectResult>();

        var verificationController = new VerificationController(senderMock.Object);
        senderMock
            .Setup(s => s.Send(It.IsAny<VerifyGradeCertificateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<OralExamination.Application.Features.OfficialExams.DTOs.GradeCertificateVerificationDto>.Failure("Không hợp lệ"));

        var notFoundVerify = await verificationController.VerifyGradeCertificate("invalid-hash", CancellationToken.None);
        notFoundVerify.Should().BeOfType<NotFoundObjectResult>();
    }
}
