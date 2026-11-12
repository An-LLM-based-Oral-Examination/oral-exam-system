using System.Globalization;
using System.Linq;
using FluentAssertions;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Persistence;

public class PostgresSchemaReconcilerTests
{
    private readonly string[] _expectedTables =
    {
        "academic_programs",
        "course_offerings",
        "course_clos",
        "course_lecturers",
        "in_class_exam_shifts",
        "in_class_shift_passwords",
        "in_class_shift_participants",
        "in_class_exam_submissions",
        "exam_anti_cheat_logs",
        "notifications"
    };

    private readonly string[] _expectedColumns =
    {
        "academic_program_id",
        "course_offering_id",
        "join_password_plain",
        "status",
        "must_change_password",
        "flow_type",
        "managed_by_role"
    };

    private readonly string[] _expectedMigrationIds =
    {
        "20261022125559_InitialDB",
        "20261022214810_AddMockExamAnswerFollowUp",
        "20261030041911_OptimizePostgresIndexesAndConstraints",
        "20261102150149_AddCourseClosAndAntiCheatLogsAndExpandRoles",
        "20261104062322_AddDlqFlowTypeAndRoleSeparation",
        "20261105061650_AddInClassExamShiftsAndNotifications3NF",
        "20261105064957_AddAcademicProgramAndCourseOfferings",
        "20261105073054_AddCourseOfferingLecturerAndClassOwnership",
        "20261107111940_AddClassJoinPasswordAndShiftExamWindow",
        "20261108131126_SeedALT",
        "20261108141117_UpdateDB"
    };

    [Fact]
    public void GetPreMigrationSql_ShouldContainIdempotentChecksAndExceptionHandling()
    {
        // Act
        var sql = PostgresSchemaReconciler.GetPreMigrationSql();

        // Assert
        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("DO $reconcile_pre$");
        sql.Should().Contain("information_schema.tables");
        sql.Should().Contain("ck_users_role");
        sql.Should().Contain("admin");
        sql.Should().Contain("department_head");
        sql.Should().Contain("examination_officer");
        sql.Should().Contain("lecturer");
        sql.Should().Contain("proctor");
        sql.Should().Contain("student");
        sql.Should().Contain("EXCEPTION WHEN OTHERS THEN NULL");
        sql.Should().Contain("EXECUTE 'UPDATE classes SET join_code");
        sql.Should().Contain("EXECUTE 'UPDATE dead_letter_queues SET flow_type");
    }

    [Fact]
    public void GetPostMigrationSql_ShouldContainAllTenRequiredTables()
    {
        // Act
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Assert
        foreach (var tableName in _expectedTables)
        {
            sql.Should().Contain($"CREATE TABLE IF NOT EXISTS {tableName}",
                because: $"table {tableName} must be created idempotently to avoid schema drift HTTP 500");
        }
    }

    [Fact]
    public void GetPostMigrationSql_ShouldBeSafeForExecuteSqlRawCompositeFormatting()
    {
        // Arrange
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Act
        Action formatSql = () => string.Format(
            CultureInfo.InvariantCulture,
            sql,
            Array.Empty<object>());

        // Assert
        formatSql.Should().NotThrow(
            "ExecuteSqlRawAsync parses braces as composite-format placeholders even when no SQL parameters are supplied");
    }

    [Fact]
    public void GetPostMigrationSql_ShouldContainAllRequiredExtensionColumns()
    {
        // Act
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Assert
        foreach (var column in _expectedColumns)
        {
            sql.Should().Contain(column,
                because: $"column {column} is required by API queries to prevent undefined column errors");
        }
    }

    [Fact]
    public void GetPostMigrationSql_ShouldContainAllElevenMigrationHistoriesWithOnConflictDoNothing()
    {
        // Act
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Assert
        sql.Should().Contain("__EFMigrationsHistory");
        sql.Should().Contain("ON CONFLICT (\"MigrationId\") DO NOTHING");

        foreach (var migrationId in _expectedMigrationIds)
        {
            sql.Should().Contain(migrationId,
                because: $"migration {migrationId} must be recorded in __EFMigrationsHistory for sync");
        }
    }

    [Fact]
    public void GetPostMigrationSql_ShouldSelfHealSeedUsersMustChangePassword()
    {
        // Act
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Assert
        sql.Should().Contain("must_change_password = FALSE");
        sql.Should().Contain("00000000-0000-0000-0000-000000000001"); // admin
        sql.Should().Contain("00000000-0000-0000-0000-000000000002"); // headse
        sql.Should().Contain("00000000-0000-0000-0000-000000000003"); // totnt
        sql.Should().Contain("admin@fpt.edu.vn");
        sql.Should().Contain("headse@fe.edu.vn");
        sql.Should().Contain("totnt@fe.edu.vn");
    }

    [Fact]
    public void GetPostMigrationSql_ShouldUseDynamicExecuteForDmlToAvoidPlanCacheFailures()
    {
        // Act
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Assert
        sql.Should().Contain("EXECUTE 'INSERT INTO academic_programs");
        sql.Should().Contain("EXECUTE 'UPDATE courses");
        sql.Should().Contain("EXECUTE 'INSERT INTO course_offerings");
        sql.Should().Contain("EXECUTE 'UPDATE course_lecturers AS cl");
        sql.Should().Contain("EXECUTE 'UPDATE classes AS cls");
        sql.Should().Contain("EXECUTE 'UPDATE users");
        sql.Should().Contain("EXECUTE 'INSERT INTO \"__EFMigrationsHistory\"");
    }
}
