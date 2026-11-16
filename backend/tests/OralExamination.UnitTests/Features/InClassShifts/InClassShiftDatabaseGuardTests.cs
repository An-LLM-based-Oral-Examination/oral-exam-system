using FluentAssertions;
using Npgsql;
using Xunit;

namespace OralExamination.UnitTests.Features.InClassShifts;

public sealed class InClassShiftDatabaseGuardTests
{
    [Fact(DisplayName = "TASK-002 Guard: Từ chối kết nối nếu tên database không kết thúc bằng _test")]
    public void DatabaseConnectionGuard_WhenDatabaseDoesNotEndWithTest_ThrowsInvalidOperationException()
    {
        const string connectionString =
            "Host=localhost;Port=5432;Database=oralexam_db;Username=oralexam;Password=oralexam_password;";
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Action act = () => EnsureTestDatabase(builder.Database);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TASK-002 GUARD CHỐNG NHIỄM BẨN CSDL*");
    }

    private static void EnsureTestDatabase(string? database)
    {
        if (string.IsNullOrWhiteSpace(database) ||
            !database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"[TASK-002 GUARD CHỐNG NHIỄM BẨN CSDL] Database '{database}' bắt buộc phải kết thúc bằng hậu tố '_test'.");
        }
    }
}
