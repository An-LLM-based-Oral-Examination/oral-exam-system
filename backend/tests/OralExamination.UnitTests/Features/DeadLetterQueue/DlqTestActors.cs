using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.UnitTests.Features.DeadLetterQueue;

internal static class DlqTestActors
{
    internal static readonly Guid Admin = Guid.Parse("09000000-0000-0000-0000-000000000001");
    internal static readonly Guid Officer = Guid.Parse("09000000-0000-0000-0000-000000000002");
    internal static readonly Guid Lecturer = Guid.Parse("09000000-0000-0000-0000-000000000003");

    internal static void Seed(OralExamDbContext db)
    {
        db.Users.AddRange(
            User(Admin, "admin"), User(Officer, "examination_officer"), User(Lecturer, "lecturer"));
        db.SaveChanges();
    }

    internal static User User(Guid id, string role) => new()
    {
        Id = id, Role = role, IsActive = true, Email = $"{id}@qa.invalid", FullName = "QA DLQ actor"
    };
}
