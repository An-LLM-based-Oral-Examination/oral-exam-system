using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class R2AudioUploadSecurityTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "R2-AUDIO-01: Đường dẫn lưu trữ audio bất biến theo định dạng STT_MSSV.webm")]
    public void Audio_Object_Key_Must_Adhere_To_Standard_Format()
    {
        var shiftId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var ticketId = Guid.Parse("00000000-0000-0000-0000-000000000020");
        var seatNumber = 1;
        var studentCode = "SE170123";

        var key = OfficialExamAudioObjectKey.Create(shiftId, ticketId, seatNumber, studentCode);

        key.Should().EndWith("01_SE170123.webm");
        key.Should().Contain(shiftId.ToString());
        key.Should().Contain(ticketId.ToString());
    }

    [Fact(DisplayName = "R2-AUDIO-02: Cấp phép tải lên audio Cloudflare R2 cho vé thi đang thi")]
    public async Task CreateAudioUploadAuthorization_Should_Generate_Presigned_Url()
    {
        await using var context = CreateDbContext();

        var student = new User { Id = Guid.NewGuid(), Email = "sv@fe.edu.vn", FullName = "Học sinh A", StudentCode = "SE170123", Role = UserRole.Student, IsActive = true };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1",
            RoomLab = "AL-L201",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(60),
            Status = ExamShiftWindow.InProgress,
            ProctorUserId = Guid.NewGuid()
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 5,
            IpAddress = "192.168.1.50",
            Status = ExamTicketStatus.InProgress
        };

        context.Users.Add(student);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.CreatePresignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, string ct, TimeSpan ttl, CancellationToken token) =>
                new StoragePresignedUploadResult("https://r2.cloudflarestorage.com/bucket/upload-presigned-url", key, DateTime.UtcNow.AddMinutes(15), new Dictionary<string, string>()));

        var handler = new CreateAudioUploadAuthorizationCommandHandler(context, mockStorage.Object);
        var command = new CreateAudioUploadAuthorizationCommand(student.Id, ticket.Id, "192.168.1.50", DateTime.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UploadUrl.Should().Be("https://r2.cloudflarestorage.com/bucket/upload-presigned-url");
        result.Value.ObjectKey.Should().EndWith("05_SE170123.webm");
    }

    [Fact(DisplayName = "R2-AUDIO-03: Niêm phong audio thành công khi mã băm SHA-256 khớp tuyệt đối với file trên R2")]
    public async Task SealOfficialExamAudio_With_Matching_Sha256_Should_Succeed()
    {
        await using var context = CreateDbContext();

        var student = new User { Id = Guid.NewGuid(), Email = "sv@fe.edu.vn", FullName = "Học sinh A", StudentCode = "SE170123", Role = UserRole.Student, IsActive = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = student.Id,
            SeatNumber = 1,
            IpAddress = "192.168.1.50",
            Status = ExamTicketStatus.InProgress
        };

        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var objectKey = OfficialExamAudioObjectKey.Create(ticket.ShiftId, ticket.Id, ticket.SeatNumber, student.StudentCode);
        var dummyAudioBytes = Encoding.UTF8.GetBytes("WEBM_BINARY_AUDIO_PAYLOAD_FOR_ORAL_EXAM");
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(dummyAudioBytes)).ToLowerInvariant();

        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.GetObjectMetadataAsync(objectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectMetadata(objectKey, dummyAudioBytes.Length, "audio/webm", null, DateTime.UtcNow, null));
        mockStorage.Setup(s => s.VerifySha256Async(objectKey, expectedSha256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new SealOfficialExamAudioCommandHandler(context, mockStorage.Object);
        var command = new SealOfficialExamAudioCommand(student.Id, ticket.Id, "192.168.1.50", objectKey, expectedSha256);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sha256.Should().Be(expectedSha256);
        result.Value.ObjectKey.Should().Be(objectKey);

        var reloadedTicket = await context.StudentExamTickets.FindAsync(ticket.Id);
        reloadedTicket!.AudioVerifiedAt.Should().NotBeNull();
        reloadedTicket.AudioHashSha256.Should().Be(expectedSha256);
    }

    [Fact(DisplayName = "R2-AUDIO-04: Từ chối niêm phong khi mã băm SHA-256 bị sai lệch (AudioHashMismatch)")]
    public async Task SealOfficialExamAudio_With_Mismatched_Sha256_Should_Fail()
    {
        await using var context = CreateDbContext();

        var student = new User { Id = Guid.NewGuid(), Email = "sv@fe.edu.vn", FullName = "Học sinh A", StudentCode = "SE170123", Role = UserRole.Student, IsActive = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = student.Id,
            SeatNumber = 1,
            IpAddress = "192.168.1.50",
            Status = ExamTicketStatus.InProgress
        };

        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var objectKey = OfficialExamAudioObjectKey.Create(ticket.ShiftId, ticket.Id, ticket.SeatNumber, student.StudentCode);
        var actualBytes = Encoding.UTF8.GetBytes("GENUINE_AUDIO_DATA");
        var tamperedSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.GetObjectMetadataAsync(objectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectMetadata(objectKey, actualBytes.Length, "audio/webm", null, DateTime.UtcNow, null));
        mockStorage.Setup(s => s.VerifySha256Async(objectKey, tamperedSha256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new SealOfficialExamAudioCommandHandler(context, mockStorage.Object);
        var command = new SealOfficialExamAudioCommand(student.Id, ticket.Id, "192.168.1.50", objectKey, tamperedSha256);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.AudioHashMismatch);
    }
}
