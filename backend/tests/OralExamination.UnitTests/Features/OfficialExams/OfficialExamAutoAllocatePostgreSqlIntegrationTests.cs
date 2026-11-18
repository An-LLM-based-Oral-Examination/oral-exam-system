using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;
using OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.UnitTests.Features.InClassShifts;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

[Trait("Category", "Integration")]
[Collection("PostgreSqlNonParallelSchemaCollection")]
public sealed class OfficialExamAutoAllocatePostgreSqlIntegrationTests
{
    private static OralExamDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(InClassShiftConcurrencyIntegrationTests.GetPostgresConnectionString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "MF04-PG-RACE: Hai request auto-allocate-students song song trên PostgreSQL 16 thật bị tuần tự hóa bởi Advisory Lock, không nhân đôi vé sinh viên xuyên các phòng")]
    public async Task ConcurrentAutoAllocateRequests_SerializeViaAdvisoryLock_AndAssignEachStudentAtMostOnce()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var officerId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var studentIds = new List<Guid>(50);

        await using var seed = NewContext();
        (await seed.Database.CanConnectAsync()).Should().BeTrue("PostgreSQL integration database must be available");
        await PostgresSchemaReconciler.ReconcilePostMigrationAsync(seed);

        seed.Users.Add(new User
        {
            Id = officerId,
            Email = $"eo-{suffix}@fpt.test",
            FullName = "Exam Officer",
            Username = $"eo_{suffix}",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        });

        seed.Semesters.Add(new Semester
        {
            Id = semesterId,
            Code = $"MF4{suffix[..6]}",
            Name = "MF04 Integration Semester",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        });

        seed.Courses.Add(new Course
        {
            Id = courseId,
            Code = $"C{suffix[..8]}",
            Name = "MF04 Concurrent Allocation Course",
            SemesterId = semesterId,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            IsActive = true
        });

        seed.Classes.Add(new Class
        {
            Id = classId,
            Code = $"CL{suffix[..8]}",
            JoinCode = $"JC{suffix[..8]}",
            Name = "MF04 Class",
            CourseId = courseId,
            SemesterId = semesterId,
            MaxStudents = 60,
            IsActive = true
        });

        seed.OfficialExamSessions.Add(new OfficialExamSession
        {
            Id = sessionId,
            CourseId = courseId,
            SemesterId = semesterId,
            Title = $"Kỳ thi tích hợp {suffix}",
            ExamDate = new DateOnly(2026, 12, 18),
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            Status = "scheduled",
            CreatedBy = officerId
        });

        for (var i = 1; i <= 50; i++)
        {
            var studentId = Guid.NewGuid();
            studentIds.Add(studentId);
            seed.Users.Add(new User
            {
                Id = studentId,
                Email = $"sv{i:D2}-{suffix}@fpt.test",
                FullName = $"Sinh viên {i:D2}",
                StudentCode = $"S{suffix[..5]}{i:D2}",
                Username = $"sv{i:D2}_{suffix[..6]}",
                Role = UserRole.Student,
                IsActive = true
            });
            seed.ClassEnrollments.Add(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                StudentId = studentId,
                Status = "enrolled",
                JoinMethod = "assigned"
            });
        }

        await seed.SaveChangesAsync();

        try
        {
            await using (var shiftCtx = NewContext())
            {
                var openResult = await new BatchOpenExamShiftsCommandHandler(shiftCtx).Handle(
                    new BatchOpenExamShiftsCommand(
                        officerId,
                        sessionId,
                        "Ca Tích Hợp",
                        new DateTime(2026, 12, 18, 1, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 12, 18, 3, 0, 0, DateTimeKind.Utc),
                        new[]
                        {
                            new BatchOpenExamShiftRoomDto { RoomLab = $"L1-{suffix[..5]}", MaxSeats = 24 },
                            new BatchOpenExamShiftRoomDto { RoomLab = $"L2-{suffix[..5]}", MaxSeats = 24 },
                            new BatchOpenExamShiftRoomDto { RoomLab = $"L3-{suffix[..5]}", MaxSeats = 24 }
                        }),
                    CancellationToken.None);

                openResult.IsSuccess.Should().BeTrue();
            }

            await using var holder = NewContext();
            await using var first = NewContext();
            await using var second = NewContext();

            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var heldLockTask = holder.ExecuteOfficialSessionAllocationAsync(sessionId, async ct =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(ct);
                return true;
            });

            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

                var firstAlloc = new AutoAllocateSessionStudentsCommandHandler(first)
                    .Handle(new AutoAllocateSessionStudentsCommand(officerId, sessionId), CancellationToken.None);
                var secondAlloc = new AutoAllocateSessionStudentsCommandHandler(second)
                    .Handle(new AutoAllocateSessionStudentsCommand(officerId, sessionId), CancellationToken.None);

                await Task.Delay(250);
                firstAlloc.IsCompleted.Should().BeFalse("request 1 bắt buộc phải chờ khóa pg_advisory_xact_lock cấp session");
                secondAlloc.IsCompleted.Should().BeFalse("request 2 bắt buộc phải chờ khóa pg_advisory_xact_lock cấp session");

                release.SetResult();
                (await heldLockTask).Should().BeTrue();

                var results = await Task.WhenAll(firstAlloc, secondAlloc).WaitAsync(TimeSpan.FromSeconds(15));
                var successes = results.Where(r => r.IsSuccess).ToList();
                var failures = results.Where(r => !r.IsSuccess).ToList();

                successes.Should().ContainSingle();
                failures.Should().ContainSingle();
                failures[0].Error.Should().StartWith("UNPROCESSABLE:");

                var winner = successes[0].Value;
                winner.NewlyAllocatedCount.Should().Be(50);
                winner.ShiftGroups.Single().IsFinalBalancedShift.Should().BeTrue();
                winner.ShiftGroups.Single().Rooms.Select(r => r.TotalAfter).Should().Equal(17, 17, 16);

                await using var verify = NewContext();
                var dbTickets = await verify.StudentExamTickets
                    .AsNoTracking()
                    .Where(t => t.Shift.SessionId == sessionId)
                    .ToListAsync();

                dbTickets.Should().HaveCount(50);
                dbTickets.Select(t => t.StudentId).Distinct().Should().HaveCount(50);
                dbTickets.All(t => t.ExamSetId == null).Should().BeTrue("uq_tickets_shift_exam_set trên PostgreSQL 16 cho phép nhiều vé cùng ca có exam_set_id IS NULL");
            }
            finally
            {
                release.TrySetResult();
                await heldLockTask;
            }
        }
        finally
        {
            await using var cleanup = NewContext();
            var shiftIds = await cleanup.RealExamSessionShifts
                .Where(s => s.SessionId == sessionId)
                .Select(s => s.Id)
                .ToListAsync();

            await cleanup.AuditLogs.Where(a => a.EntityId == sessionId || a.EntityId == semesterId).ExecuteDeleteAsync();
            if (shiftIds.Count > 0)
            {
                await cleanup.StudentExamTickets.Where(t => shiftIds.Contains(t.ShiftId)).ExecuteDeleteAsync();
                await cleanup.RealExamSessionShifts.Where(s => shiftIds.Contains(s.Id)).ExecuteDeleteAsync();
            }
            await cleanup.OfficialExamSessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync();
            await cleanup.ClassEnrollments.Where(e => e.ClassId == classId).ExecuteDeleteAsync();
            await cleanup.Classes.Where(c => c.Id == classId).ExecuteDeleteAsync();
            await cleanup.Courses.Where(c => c.Id == courseId).ExecuteDeleteAsync();
            await cleanup.Semesters.Where(s => s.Id == semesterId).ExecuteDeleteAsync();
            var allUserIds = studentIds.Append(officerId).ToList();
            await cleanup.Users.Where(u => allUserIds.Contains(u.Id)).ExecuteDeleteAsync();
        }
    }
}
