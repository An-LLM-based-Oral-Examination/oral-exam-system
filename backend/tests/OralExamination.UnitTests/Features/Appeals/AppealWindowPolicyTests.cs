using OralExamination.Application.Features.Appeals;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using Xunit;

namespace OralExamination.UnitTests.Features.Appeals;

public class AppealWindowPolicyTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void Deadline_IsExclusive(long offsetTicks, bool allowed)
    {
        var publication = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
        var ticket = new StudentExamTicket { Status = ExamTicketStatus.Published, GradesPublishedAt = publication };
        Assert.Equal(allowed, AppealWindowPolicy.Restriction(ticket, publication.AddHours(72).AddTicks(offsetTicks), 72) == null);
    }

    [Fact]
    public void MissingPublication_DoesNotUseSubmissionOrCheckIn()
    {
        var ticket = new StudentExamTicket { Status = ExamTicketStatus.Published, SubmittedAt = DateTime.UtcNow, CheckedInAt = DateTime.UtcNow };
        Assert.NotNull(AppealWindowPolicy.Restriction(ticket, DateTime.UtcNow, 72));
    }

    [Fact]
    public void Acknowledged_CannotAppeal()
    {
        var ticket = new StudentExamTicket { Status = ExamTicketStatus.Published, GradesPublishedAt = DateTime.UtcNow, StudentAcknowledgementStatus = "ACKNOWLEDGED" };
        Assert.Contains("đồng ý", AppealWindowPolicy.Restriction(ticket, DateTime.UtcNow, 72));
    }

    [Fact]
    public void IsLockedAlone_IsNotPublication()
    {
        var ticket = new StudentExamTicket { Status = ExamTicketStatus.AiGraded, IsLocked = true, GradesPublishedAt = DateTime.UtcNow };
        Assert.False(AppealWindowPolicy.IsPublished(ticket));
    }
}
