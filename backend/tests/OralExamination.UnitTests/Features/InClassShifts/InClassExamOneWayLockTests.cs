using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace OralExamination.UnitTests.Features.InClassShifts;

public class InClassExamOneWayLockTests
{
    private static OralExamDbContext CreateDbContextWithInterceptor()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new OneWayLockInterceptor())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact]
    public async Task LockedShift_AttemptToModifyShift_ThrowsOneWayLockException()
    {
        // Arrange
        using var context = CreateDbContextWithInterceptor();
        var shiftId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Ca thi khóa một chiều",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = "secret_key_test",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        // Act & Assert
        // Modify shift name after it has been locked
        shift.ShiftName = "Ca thi bị đổi tên trái phép";

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*không thể sửa hoặc xóa*khóa một chiều*");
    }

    [Fact]
    public async Task LockedShift_AttemptToDeleteShift_ThrowsOneWayLockException()
    {
        // Arrange
        using var context = CreateDbContextWithInterceptor();
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Ca thi đã chốt điểm",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = "secret_key_test",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        // Act & Assert
        context.InClassExamShifts.Remove(shift);

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*không thể sửa hoặc xóa*khóa một chiều*");
    }

    [Fact]
    public async Task LockedShift_AttemptToModifySubmission_ThrowsOneWayLockException()
    {
        // Arrange
        using var context = CreateDbContextWithInterceptor();
        var shiftId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Ca thi đã khóa điểm",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = "secret_key_test",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow
        };
        context.InClassExamShifts.Add(shift);

        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Bài nộp sinh viên",
            AiScore = 8.00m,
            FinalScore = 8.00m,
            Status = "AUDITED"
        };
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        // Act & Assert
        // Attempt to modify score of submission belonging to locked shift
        submission.FinalScore = 10.00m;

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*in-class exam grade aggregate*");
    }
}
