using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams.Queries.GetAuditEvidence;

public class GetAuditEvidenceQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. Trích xuất đầy đủ thông tin bằng chứng hậu kiểm (AudioURL, Transcript, CoT, Điểm)")]
    public async Task Handle_Should_Return_Full_Audit_Evidence_Items()
    {
        using var context = CreateDbContext();

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1 - Sáng Lab 301",
            RoomLab = "Lab 301",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.evidence@fpt.edu.vn",
            FullName = "Nguyễn Văn Bằng Chứng",
            StudentCode = "SE170999",
            Role = UserRole.Student,
            IsActive = true
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 5,
            IpAddress = "192.168.1.105",
            Status = "AUDITED",
            IsLocked = false
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AudioR2Url = "https://r2.fpt.edu.vn/exams/shift-1/05_SE170999.webm",
            AudioHashSha256 = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9",
            TranscriptWhisper = "Thí sinh trả lời chuẩn xác về nguyên lý Dependency Inversion.",
            AiScore = 8.50m,
            FinalScore = 8.50m,
            GradingStatus = "graded"
        };

        var aiEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            ExamSubmissionId = submission.Id,
            TotalScore = 8.50m,
            ConfidenceScore = 0.92m,
            CotTrace = "Bước 1: Nhận diện định nghĩa DIP. Bước 2: Phân tích ví dụ DbContext. Bước 3: Đánh giá barem 8.5/10.",
            Feedback = "Câu trả lời mạch lạc, hiểu rõ DIP."
        };

        submission.AiEvaluations.Add(aiEval);
        ticket.Submissions.Add(submission);

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.AiEvaluations.Add(aiEval);
        await context.SaveChangesAsync();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var query = new GetAuditEvidenceQuery(shift.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var item = result.Value[0];
        item.TicketId.Should().Be(ticket.Id);
        item.StudentCode.Should().Be("SE170999");
        item.FullName.Should().Be("Nguyễn Văn Bằng Chứng");
        item.SeatNumber.Should().Be(5);
        item.AudioR2Url.Should().Be("https://r2.fpt.edu.vn/exams/shift-1/05_SE170999.webm");
        item.AudioHashSha256.Should().Be("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9");
        item.TranscriptWhisper.Should().Contain("Dependency Inversion");
        item.AiScore.Should().Be(8.50m);
        item.FinalScore.Should().Be(8.50m);
        item.ConfidenceScore.Should().Be(0.92m);
        item.CotTrace.Should().Contain("Bước 1: Nhận diện định nghĩa DIP");
        item.IsSuspicious.Should().BeFalse();
        item.SuspiciousReason.Should().BeNull();
    }

    [Fact(DisplayName = "2. AI Doubt Guard: Đánh dấu IsSuspicious = true khi ConfidenceScore < 0.70")]
    public async Task Handle_Should_Mark_Suspicious_When_Confidence_Low()
    {
        using var context = CreateDbContext();

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 2",
            RoomLab = "Lab 302",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.lowconf@fpt.edu.vn",
            FullName = "Trần Nghi Ngờ",
            StudentCode = "SE170555",
            Role = UserRole.Student,
            IsActive = true
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = "AI_GRADED",
            IsLocked = false
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Âm thanh bị ồn nhiều tạp âm...",
            AiScore = 5.00m,
            GradingStatus = "graded"
        };

        var aiEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            ExamSubmissionId = submission.Id,
            TotalScore = 5.00m,
            ConfidenceScore = 0.55m, // < 0.70m
            CotTrace = "Độ tin cậy thấp do không rõ âm tiết.",
            Feedback = "Phát âm không rõ, cần nghe lại."
        };

        submission.AiEvaluations.Add(aiEval);
        ticket.Submissions.Add(submission);

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.AiEvaluations.Add(aiEval);
        await context.SaveChangesAsync();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var query = new GetAuditEvidenceQuery(shift.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].IsSuspicious.Should().BeTrue();
        result.Value[0].SuspiciousReason.Should().Contain("55% < 70%");
    }

    [Fact(DisplayName = "3. AI Doubt Guard: Đánh dấu IsSuspicious = true khi Giảng viên sửa điểm lệch >= 2.0")]
    public async Task Handle_Should_Mark_Suspicious_When_Lecturer_Override_Exceeds_Threshold()
    {
        using var context = CreateDbContext();

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 3",
            RoomLab = "Lab 303",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.override@fpt.edu.vn",
            FullName = "Lê Điểm Lệch",
            StudentCode = "SE170777",
            Role = UserRole.Student,
            IsActive = true
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 2,
            Status = "AUDITED",
            IsLocked = false
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 5.00m,
            FinalScore = 8.50m
        };

        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = Guid.NewGuid(),
            OriginalAiScore = 5.00m,
            AuditedScore = 8.50m, // Lệch 3.5 điểm >= 2.0
            OverrideReason = "AI nhận diện sai ý của sinh viên.",
            IsLocked = false
        };

        ticket.Submissions.Add(submission);
        ticket.LecturerAudit = audit;

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.LecturerAudits.Add(audit);
        await context.SaveChangesAsync();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var query = new GetAuditEvidenceQuery(shift.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].IsSuspicious.Should().BeTrue();
        result.Value[0].SuspiciousReason.Should().Contain("lệch lớn so với AI ban đầu");
    }

    [Fact(DisplayName = "4. Handle trả về Failure khi ca thi không tồn tại")]
    public async Task Handle_Should_Fail_When_Shift_Does_Not_Exist()
    {
        using var context = CreateDbContext();

        var handler = new GetAuditEvidenceQueryHandler(context);
        var query = new GetAuditEvidenceQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ca thi không tồn tại trong hệ thống.");
    }
}
