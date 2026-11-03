using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class NonDuplicateExamSetAssignmentTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "SET-ASSIGN-01: Model DbContext cấu hình Unique Index uq_tickets_shift_exam_set")]
    public void DbContext_Must_Configure_Unique_Index_On_ShiftId_And_ExamSetId()
    {
        using var context = CreateDbContext();
        var ticketType = context.Model.FindEntityType(typeof(StudentExamTicket))!;
        var uniqueIndex = ticketType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "uq_tickets_shift_exam_set");

        uniqueIndex.Should().NotBeNull("Bảng student_exam_tickets phải có Unique Index uq_tickets_shift_exam_set");
        uniqueIndex!.IsUnique.Should().BeTrue();
        uniqueIndex.Properties.Select(p => p.Name).Should().Contain("ShiftId");
        uniqueIndex.Properties.Select(p => p.Name).Should().Contain("ExamSetId");
    }

    [Fact(DisplayName = "SET-ASSIGN-02: Hai sinh viên trong cùng một ca thi không bị gán trùng bộ đề")]
    public async Task Two_Students_In_Same_Shift_Must_Not_Be_Assigned_Same_ExamSet()
    {
        await using var context = CreateDbContext();

        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Cross-Platform", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Structure 1", IsActive = true };
        var head = new User { Id = Guid.NewGuid(), Email = "head@fe.edu.vn", FullName = "Trưởng BM", Role = UserRole.DepartmentHead, IsActive = true };
        var student1 = new User { Id = Guid.NewGuid(), Email = "sv1@fe.edu.vn", FullName = "Sinh viên 1", StudentCode = "SE180001", Role = UserRole.Student, IsActive = true };
        var student2 = new User { Id = Guid.NewGuid(), Email = "sv2@fe.edu.vn", FullName = "Sinh viên 2", StudentCode = "SE180002", Role = UserRole.Student, IsActive = true };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            Title = "Kỳ thi PRN231",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Status = "scheduled",
            CreatedBy = head.Id
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "AL-L201",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            ProctorUserId = head.Id,
            Status = "scheduled"
        };

        var set1 = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "PRN231-SET01", Status = ExamSetStatus.Official, IsActive = true };
        var set2 = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "PRN231-SET02", Status = ExamSetStatus.Official, IsActive = true };

        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        context.Users.AddRange(head, student1, student2);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.ExamSets.AddRange(set1, set2);
        await context.SaveChangesAsync();

        var handler = new AssignExamTicketCommandHandler(context, new AssignExamTicketCommandValidator());

        // Assign ticket 1 for student 1 at Seat 1
        var command1 = new AssignExamTicketCommand(head.Id, shift.Id, student1.Id, 1, "192.168.1.101");
        var result1 = await handler.Handle(command1, CancellationToken.None);

        result1.IsSuccess.Should().BeTrue();
        var ticket1 = await context.StudentExamTickets.FindAsync(result1.Value.Id);
        ticket1.Should().NotBeNull();
        ticket1!.ExamSetId.Should().NotBeNull();

        // Assign ticket 2 for student 2 at Seat 2 in same shift
        var command2 = new AssignExamTicketCommand(head.Id, shift.Id, student2.Id, 2, "192.168.1.102");
        var result2 = await handler.Handle(command2, CancellationToken.None);

        result2.IsSuccess.Should().BeTrue();
        var ticket2 = await context.StudentExamTickets.FindAsync(result2.Value.Id);
        ticket2.Should().NotBeNull();
        ticket2!.ExamSetId.Should().NotBeNull();

        // Bất biến: ticket1 và ticket2 phải có ExamSetId khác nhau trong cùng ca thi
        ticket1.ExamSetId!.Value.Should().NotBe(ticket2.ExamSetId!.Value, "Trong cùng 1 ca thi, hai thí sinh không được dùng chung một bộ đề");
    }

    [Fact(DisplayName = "SET-ASSIGN-03: Hai ca thi khác nhau có thể sử dụng lại cùng một bộ đề")]
    public async Task Different_Shifts_Can_Reuse_Same_ExamSet()
    {
        await using var context = CreateDbContext();

        var course = new Course { Id = Guid.NewGuid(), Code = "CSD201", Name = "Algorithms", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Structure 1", IsActive = true };
        var head = new User { Id = Guid.NewGuid(), Email = "head@fe.edu.vn", FullName = "Trưởng BM", Role = UserRole.DepartmentHead, IsActive = true };
        var student1 = new User { Id = Guid.NewGuid(), Email = "sv1@fe.edu.vn", FullName = "Sinh viên 1", StudentCode = "SE180001", Role = UserRole.Student, IsActive = true };
        var student2 = new User { Id = Guid.NewGuid(), Email = "sv2@fe.edu.vn", FullName = "Sinh viên 2", StudentCode = "SE180002", Role = UserRole.Student, IsActive = true };

        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = structure.Id, Title = "Kỳ thi CSD201", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), Status = "scheduled", CreatedBy = head.Id };

        var shift1 = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca Sáng", RoomLab = "AL-L201", StartTime = DateTime.UtcNow.AddHours(1), EndTime = DateTime.UtcNow.AddHours(3), ProctorUserId = head.Id, Status = "scheduled" };
        var shift2 = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca Chiều", RoomLab = "AL-L201", StartTime = DateTime.UtcNow.AddHours(5), EndTime = DateTime.UtcNow.AddHours(7), ProctorUserId = head.Id, Status = "scheduled" };

        var singleSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "CSD201-SET01", Status = ExamSetStatus.Official, IsActive = true };

        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        context.Users.AddRange(head, student1, student2);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.AddRange(shift1, shift2);
        context.ExamSets.Add(singleSet);
        await context.SaveChangesAsync();

        var handler = new AssignExamTicketCommandHandler(context, new AssignExamTicketCommandValidator());

        var res1 = await handler.Handle(new AssignExamTicketCommand(head.Id, shift1.Id, student1.Id, 1, "192.168.1.101"), CancellationToken.None);
        var res2 = await handler.Handle(new AssignExamTicketCommand(head.Id, shift2.Id, student2.Id, 1, "192.168.1.101"), CancellationToken.None);

        res1.IsSuccess.Should().BeTrue();
        res2.IsSuccess.Should().BeTrue();

        var ticket1 = await context.StudentExamTickets.FindAsync(res1.Value.Id);
        var ticket2 = await context.StudentExamTickets.FindAsync(res2.Value.Id);

        ticket1!.ExamSetId.Should().Be(singleSet.Id);
        ticket2!.ExamSetId.Should().Be(singleSet.Id);
    }
}
