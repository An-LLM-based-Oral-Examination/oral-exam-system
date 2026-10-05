using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Appeals.Queries.GetAppealById;
using OralExamination.Application.Features.Appeals.Queries.GetAppeals;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Appeals.Handlers;

public class GetAppealsQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Student, User DeptHead, StudentExamTicket Ticket, AppealRequest Appeal1, AppealRequest Appeal2)> SeedDataAsync(OralExamDbContext context)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.query@fpt.edu.vn",
            FullName = "Sinh Viên Tra Cứu",
            StudentCode = "SE170345",
            Role = UserRole.Student,
            IsActive = true
        };

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "depthead.query@fpt.edu.vn",
            FullName = "Trưởng Bộ Môn Tra Cứu",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = ".NET Architecture",
            Credits = 3,
            SemesterId = semester.Id
        };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = Guid.NewGuid(),
            Title = "Ca thi Tra cứu PRN231",
            ExamDate = new DateOnly(2026, 10, 10)
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1)
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 5,
            Status = "PUBLISHED"
        };

        var appeal1 = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
            Reason = "Lý do phúc khảo số 1",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var appeal2 = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
            Reason = "Lý do phúc khảo số 2",
            Status = AppealStatus.Approved,
            AssignedTo = deptHead.Id,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(student, deptHead);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.AppealRequests.AddRange(appeal1, appeal2);
        await context.SaveChangesAsync();

        return (student, deptHead, ticket, appeal1, appeal2);
    }

    [Fact(DisplayName = "1. GetAppealsQuery trả về danh sách đầy đủ khi không truyền bộ lọc")]
    public async Task Handle_GetAppeals_Should_Return_All_Appeals()
    {
        using var context = CreateDbContext();
        await SeedDataAsync(context);

        var handler = new GetAppealsQueryHandler(context);
        var query = new GetAppealsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact(DisplayName = "2. GetAppealsQuery lọc chính xác theo Status")]
    public async Task Handle_GetAppeals_Should_Filter_By_Status()
    {
        using var context = CreateDbContext();
        await SeedDataAsync(context);

        var handler = new GetAppealsQueryHandler(context);
        var query = new GetAppealsQuery(Status: AppealStatus.Pending);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Status.Should().Be(AppealStatus.Pending);
    }

    [Fact(DisplayName = "3. GetAppealByIdQuery trả về đơn phúc khảo chi tiết khi ID hợp lệ")]
    public async Task Handle_GetAppealById_Should_Return_Appeal_Detail()
    {
        using var context = CreateDbContext();
        var (_, _, _, appeal1, _) = await SeedDataAsync(context);

        var handler = new GetAppealByIdQueryHandler(context);
        var query = new GetAppealByIdQuery(appeal1.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(appeal1.Id);
        result.Value.Reason.Should().Be(appeal1.Reason);
    }

    [Fact(DisplayName = "4. GetAppealByIdQuery trả về Failure khi ID không tồn tại")]
    public async Task Handle_GetAppealById_Should_Fail_When_Not_Found()
    {
        using var context = CreateDbContext();
        await SeedDataAsync(context);

        var handler = new GetAppealByIdQueryHandler(context);
        var query = new GetAppealByIdQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không tồn tại");
    }
}
