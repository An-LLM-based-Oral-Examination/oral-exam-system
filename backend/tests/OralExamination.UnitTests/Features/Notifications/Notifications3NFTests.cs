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
}
