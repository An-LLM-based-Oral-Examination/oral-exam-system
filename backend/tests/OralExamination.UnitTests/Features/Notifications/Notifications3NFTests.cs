using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using OralExamination.Application.Features.Notifications.Commands.MarkNotificationRead;
using OralExamination.Application.Features.Notifications.Queries.GetNotifications;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Notifications;

public class Notifications3NFTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "R4.1-01: GetNotifications truy vấn trực tiếp bảng notifications 3NF và phân trang chuẩn")]
    public async Task GetNotifications_ShouldQueryDirectlyFrom3NFTable_AndPaginate()
    {
        await using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.noti@fpt.edu.vn",
            FullName = "Sinh Viên Thông Báo 3NF",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(user);

        for (int i = 1; i <= 5; i++)
        {
            context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Title = $"Thông báo số {i}",
                Message = $"Nội dung thông báo {i}",
                Type = "TEST_NOTIFICATION",
                IsRead = i <= 2,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        var handler = new GetNotificationsQueryHandler(context);

        // Truy vấn tất cả với pageSize = 3
        var resultAll = await handler.Handle(
            new GetNotificationsQuery(user.Id, UnreadOnly: false, Page: 1, PageSize: 3),
            CancellationToken.None);

        resultAll.IsSuccess.Should().BeTrue();
        resultAll.Value.TotalCount.Should().Be(5);
        resultAll.Value.UnreadCount.Should().Be(3);
        resultAll.Value.Items.Should().HaveCount(3);
        resultAll.Value.Page.Should().Be(1);
        resultAll.Value.PageSize.Should().Be(3);

        // Truy vấn UnreadOnly = true
        var resultUnread = await handler.Handle(
            new GetNotificationsQuery(user.Id, UnreadOnly: true, Page: 1, PageSize: 10),
            CancellationToken.None);

        resultUnread.IsSuccess.Should().BeTrue();
        resultUnread.Value.Items.Should().HaveCount(3);
        resultUnread.Value.Items.All(n => !n.IsRead).Should().BeTrue();
    }

    [Fact(DisplayName = "R4.1-02: MarkNotificationRead cập nhật IsRead = true và ReadAt trực tiếp trên Notifications 3NF")]
    public async Task MarkNotificationRead_ShouldUpdateDirectlyOn3NFNotification()
    {
        await using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user.markread@fpt.edu.vn",
            FullName = "Người Dùng Đọc Thông Báo",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var noti = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Đơn phúc khảo mới",
            Message = "Có đơn phúc khảo cần chấm lại",
            Type = "APPEAL_ASSIGNED",
            IsRead = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.Users.Add(user);
        context.Notifications.Add(noti);
        await context.SaveChangesAsync();

        var initialAuditLogsCount = await context.AuditLogs.CountAsync();

        var handler = new MarkNotificationReadCommandHandler(context);
        var result = await handler.Handle(
            new MarkNotificationReadCommand(user.Id, noti.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updatedNoti = await context.Notifications.FirstAsync(n => n.Id == noti.Id);
        updatedNoti.IsRead.Should().BeTrue();
        updatedNoti.ReadAt.Should().NotBeNull();
        updatedNoti.ReadAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // BẢO ĐẢM TUYỆT ĐỐI: 0 truy vấn ghi vào AuditLogs
        var finalAuditLogsCount = await context.AuditLogs.CountAsync();
        finalAuditLogsCount.Should().Be(initialAuditLogsCount, "MarkNotificationRead không được nhồi vào bảng audit_logs");
    }

    [Fact(DisplayName = "R4.1-03: MarkNotificationRead chặn khi thông báo thuộc về User khác (Forbidden)")]
    public async Task MarkNotificationRead_WhenNotOwner_ShouldReturnForbidden()
    {
        await using var context = CreateDbContext();
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@fpt.edu.vn",
            FullName = "Chủ Sở Hữu",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var otherUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@fpt.edu.vn",
            FullName = "Người Khác",
            Role = UserRole.Student,
            IsActive = true
        };
        var noti = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            Title = "Thông báo riêng",
            Message = "Nội dung riêng",
            Type = "PRIVATE",
            IsRead = false
        };
        context.Users.AddRange(owner, otherUser);
        context.Notifications.Add(noti);
        await context.SaveChangesAsync();

        var handler = new MarkNotificationReadCommandHandler(context);
        var result = await handler.Handle(
            new MarkNotificationReadCommand(otherUser.Id, noti.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "R4.1-04: MarkAllNotificationsRead cập nhật tất cả thông báo chưa đọc của User")]
    public async Task MarkAllNotificationsRead_ShouldUpdateAllUnreadNotificationsOfUser()
    {
        await using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user.markall@fpt.edu.vn",
            FullName = "Người Dùng Đọc Hết",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(user);

        for (int i = 0; i < 4; i++)
        {
            context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Title = $"Thông báo {i}",
                Message = $"Nội dung {i}",
                Type = "BROADCAST",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        var initialAuditLogsCount = await context.AuditLogs.CountAsync();

        var handler = new MarkAllNotificationsReadCommandHandler(context);
        var result = await handler.Handle(new MarkAllNotificationsReadCommand(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var allNotis = await context.Notifications.Where(n => n.UserId == user.Id).ToListAsync();
        allNotis.All(n => n.IsRead).Should().BeTrue();
        allNotis.All(n => n.ReadAt.HasValue).Should().BeTrue();

        // BẢO ĐẢM TUYỆT ĐỐI: 0 truy vấn ghi vào AuditLogs
        var finalAuditLogsCount = await context.AuditLogs.CountAsync();
        finalAuditLogsCount.Should().Be(initialAuditLogsCount, "MarkAllNotificationsRead không được nhồi vào bảng audit_logs");
    }

    [Fact(DisplayName = "M1-F6-01: GetNotifications với UnreadOnly = true lọc TotalCount/TotalPages đúng số chưa đọc, tie-break Id desc, TotalPages >= 1")]
    public async Task GetNotifications_UnreadOnlyTrue_SetsTotalCountAndTotalPagesToUnreadCount_AndOrdersTieByIdDesc()
    {
        await using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.unread@fpt.edu.vn",
            FullName = "Unread Pagination User",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(user);

        var sameTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        var unreadLowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var unreadHighId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        context.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), UserId = user.Id, Title = "Read 1", Message = "R1", Type = "INFO", IsRead = true, CreatedAt = sameTime.AddMinutes(1) },
            new Notification { Id = Guid.NewGuid(), UserId = user.Id, Title = "Read 2", Message = "R2", Type = "INFO", IsRead = true, CreatedAt = sameTime.AddMinutes(2) },
            new Notification { Id = Guid.NewGuid(), UserId = user.Id, Title = "Read 3", Message = "R3", Type = "INFO", IsRead = true, CreatedAt = sameTime.AddMinutes(3) },
            new Notification { Id = unreadLowId, UserId = user.Id, Title = "Unread Low", Message = "U1", Type = "INFO", IsRead = false, CreatedAt = sameTime },
            new Notification { Id = unreadHighId, UserId = user.Id, Title = "Unread High", Message = "U2", Type = "INFO", IsRead = false, CreatedAt = sameTime });
        await context.SaveChangesAsync();

        var handler = new GetNotificationsQueryHandler(context);
        var page1 = await handler.Handle(new GetNotificationsQuery(user.Id, UnreadOnly: true, Page: 1, PageSize: 1), CancellationToken.None);
        var page2 = await handler.Handle(new GetNotificationsQuery(user.Id, UnreadOnly: true, Page: 2, PageSize: 1), CancellationToken.None);

        page1.IsSuccess.Should().BeTrue();
        page1.Value.UnreadCount.Should().Be(2);
        page1.Value.TotalCount.Should().Be(2);
        page1.Value.TotalPages.Should().Be(2);
        page1.Value.Items.Should().ContainSingle().Which.Id.Should().Be(unreadHighId);

        page2.IsSuccess.Should().BeTrue();
        page2.Value.Items.Should().ContainSingle().Which.Id.Should().Be(unreadLowId);

        var otherUser = new User { Id = Guid.NewGuid(), Email = "empty.noti@fpt.edu.vn", FullName = "Empty User", Role = UserRole.Student, IsActive = true };
        context.Users.Add(otherUser);
        await context.SaveChangesAsync();
        var emptyRes = await handler.Handle(new GetNotificationsQuery(otherUser.Id, UnreadOnly: true, Page: 1, PageSize: 10), CancellationToken.None);
        emptyRes.IsSuccess.Should().BeTrue();
        emptyRes.Value.TotalCount.Should().Be(0);
        emptyRes.Value.TotalPages.Should().Be(1);
    }
}
