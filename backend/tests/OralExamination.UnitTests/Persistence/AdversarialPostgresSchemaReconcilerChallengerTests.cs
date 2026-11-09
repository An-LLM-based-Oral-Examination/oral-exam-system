using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Persistence;

/// <summary>
/// Empirical Adversarial Challenger Test Suite for Milestone M3 (PostgresSchemaReconciler).
/// Stress-tests idempotency, DDL safety, exception swallowing, dynamic SQL parsing, and edge cases.
/// Author: teamwork_challenger (Milestone M3 Empirical Adversarial Verification 1).
/// </summary>
public class AdversarialPostgresSchemaReconcilerChallengerTests
{
    private static readonly string[] Required10Tables =
    {
        "course_clos",
        "academic_programs",
        "course_offerings",
        "course_lecturers",
        "in_class_exam_shifts",
        "in_class_shift_passwords",
        "in_class_shift_participants",
        "in_class_exam_submissions",
        "exam_anti_cheat_logs",
        "notifications"
    };

    private static readonly string[] Required11Migrations =
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

    [Fact(DisplayName = "ADV-SCENARIO-01: Reconciler SQL is deterministic and idempotent across 10 repeated invocations")]
    public void Scenario1_ReconcilerSql_IsDeterministic_Across10RepeatedCalls()
    {
        // Act
        var baselinePre = PostgresSchemaReconciler.GetPreMigrationSql();
        var baselinePost = PostgresSchemaReconciler.GetPostMigrationSql();

        for (int i = 0; i < 10; i++)
        {
            var pre = PostgresSchemaReconciler.GetPreMigrationSql();
            var post = PostgresSchemaReconciler.GetPostMigrationSql();

            pre.Should().Be(baselinePre, "Pre-migration SQL must be purely deterministic across multiple invocations");
            post.Should().Be(baselinePost, "Post-migration SQL must be purely deterministic across multiple invocations");
        }

        // Assert all CREATE TABLE statements contain IF NOT EXISTS
        var createTableMatches = Regex.Matches(baselinePost, @"CREATE\s+TABLE\s+([^\(]+)", RegexOptions.IgnoreCase);
        createTableMatches.Should().NotBeEmpty();
        foreach (Match match in createTableMatches)
        {
            match.Value.Should().Contain("IF NOT EXISTS",
                $"Table creation statement '{match.Value.Trim()}' must be guarded by IF NOT EXISTS for repeat execution");
        }

        // Assert all CREATE INDEX statements contain IF NOT EXISTS
        var createIndexMatches = Regex.Matches(baselinePost, @"CREATE\s+(?:UNIQUE\s+)?INDEX\s+([^\s]+)", RegexOptions.IgnoreCase);
        createIndexMatches.Should().NotBeEmpty();
        foreach (Match match in createIndexMatches)
        {
            var line = baselinePost.Substring(match.Index, Math.Min(120, baselinePost.Length - match.Index));
            line.Should().Contain("IF NOT EXISTS",
                $"Index creation statement '{line.Trim()}' must be guarded by IF NOT EXISTS for repeat execution");
        }
    }

    [Fact(DisplayName = "ADV-SCENARIO-02: Every ADD COLUMN statement on existing tables must be NULL or have explicit DEFAULT")]
    public void Scenario2_AddColumn_MustBeNullableOrHaveExplicitDefault_ToPreventNotNullViolations()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Extract all "ALTER TABLE ... ADD COLUMN IF NOT EXISTS <col> <type> ..."
        var addColumnMatches = Regex.Matches(sql, @"ALTER\s+TABLE\s+(\w+)\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+([^;]+);", RegexOptions.IgnoreCase);
        addColumnMatches.Should().NotBeEmpty("There must be ADD COLUMN statements to reconcile schema drift");

        var dangerousNotNullColumns = new List<string>();

        foreach (Match match in addColumnMatches)
        {
            var fullStatement = match.Value;
            var columnDef = match.Groups[2].Value.Trim();

            // Check if column is NOT NULL
            if (Regex.IsMatch(columnDef, @"\bNOT\s+NULL\b", RegexOptions.IgnoreCase))
            {
                // Must have a DEFAULT clause
                if (!Regex.IsMatch(columnDef, @"\bDEFAULT\b", RegexOptions.IgnoreCase))
                {
                    dangerousNotNullColumns.Add(fullStatement);
                }
            }
        }

        dangerousNotNullColumns.Should().BeEmpty(
            "Adding a NOT NULL column without DEFAULT to an existing populated table will fail with 23502 (column contains null values). " +
            $"Violations found: {string.Join(" | ", dangerousNotNullColumns)}");
    }

    [Fact(DisplayName = "ADV-SCENARIO-03: Every Foreign Key constraint must be protected inside a BEGIN ... EXCEPTION WHEN OTHERS THEN NULL block")]
    public void Scenario3_ForeignKeys_MustBeWrappedInSubtransactionExceptionBlocks_ToSwallowOrphanErrors()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Find all foreign key statements
        var fkMatches = Regex.Matches(sql, @"ALTER\s+TABLE\s+\w+\s+ADD\s+CONSTRAINT\s+""?FK_[^;]+FOREIGN\s+KEY[^;]+;", RegexOptions.IgnoreCase);
        fkMatches.Should().NotBeEmpty("There must be Foreign Key constraint additions");

        // Verify that in the entire script, every ADD CONSTRAINT is preceded by BEGIN and followed by EXCEPTION WHEN OTHERS THEN NULL; END;
        var blocks = sql.Split(new[] { "BEGIN", "END;" }, StringSplitOptions.RemoveEmptyEntries);

        var protectedFkCount = 0;
        foreach (var block in blocks)
        {
            if (block.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            {
                block.Should().Contain("EXCEPTION WHEN OTHERS THEN",
                    "Any subtransaction declaring a FOREIGN KEY constraint must catch OTHERS to prevent aborting if dirty/orphaned rows exist");
                block.Should().Contain("NULL;",
                    "The exception handler must safely execute NULL to proceed without crashing");
                protectedFkCount++;
            }
        }

        protectedFkCount.Should().BeGreaterOrEqualTo(10,
            "All core foreign key declarations across academic programs, course offerings, classes, and shifts must be protected");
    }

    [Fact(DisplayName = "ADV-SCENARIO-04: __EFMigrationsHistory insert must use ON CONFLICT (\"MigrationId\") DO NOTHING")]
    public void Scenario4_MigrationHistorySync_MustUseOnConflictDoNothing_WithAll11Migrations()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        sql.Should().Contain("__EFMigrationsHistory", "Must reference the EF Core migrations table");
        sql.Should().Contain("ON CONFLICT (\"MigrationId\") DO NOTHING",
            "Must use exact ON CONFLICT clause matching EF Core primary key to ensure idempotency when migrations already exist");

        foreach (var migrationId in Required11Migrations)
        {
            sql.Should().Contain(migrationId, $"Migration '{migrationId}' must be present in the history sync list");
        }
    }

    [Fact(DisplayName = "ADV-PARSER-05: All DML statements inside PL/pgSQL must use dynamic EXECUTE to prevent plan cache failures")]
    public void Adversarial_DmlStatements_MustUseDynamicExecute_ToPreventPostgresPlanCacheErrors()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        // Extract lines inside the DO $reconcile_post$ block
        var lines = sql.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var staticDmlViolations = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Look for unquoted INSERT or UPDATE that is not part of EXECUTE '...'
            if ((line.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase) ||
                 line.StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase)) &&
                !line.StartsWith("EXECUTE", StringComparison.OrdinalIgnoreCase))
            {
                // Exclude comment lines
                if (!line.StartsWith("--"))
                {
                    staticDmlViolations.Add(line);
                }
            }
        }

        staticDmlViolations.Should().BeEmpty(
            "Static DML statements (INSERT/UPDATE) referencing newly added columns within the same PL/pgSQL block " +
            "cause compile-time 42703 (column does not exist) errors in PostgreSQL. All DML must use dynamic EXECUTE. " +
            $"Violations: {string.Join(" | ", staticDmlViolations)}");
    }

    [Fact(DisplayName = "ADV-SEED-06: Seed user must_change_password reset must cover admin, dept head, and sample users")]
    public void Adversarial_SeedUserPasswordReset_MustIncludeAllCoreDefaultAccounts()
    {
        var sql = PostgresSchemaReconciler.GetPostMigrationSql();

        var expectedUsernames = new[] { "admin", "headse", "totnt", "khaothi", "hoalvse170001", "maittse170002" };
        var expectedEmails = new[] { "admin@fpt.edu.vn", "headse@fe.edu.vn", "totnt@fe.edu.vn", "khaothi@fe.edu.vn" };
        var expectedIds = new[]
        {
            "00000000-0000-0000-0000-000000000001", // admin
            "00000000-0000-0000-0000-000000000002", // headse
            "00000000-0000-0000-0000-000000000003"  // totnt
        };

        foreach (var username in expectedUsernames)
        {
            sql.Should().Contain($"''{username}''", $"Username '{username}' must be included in the self-healing password reset query");
        }

        foreach (var email in expectedEmails)
        {
            sql.Should().Contain($"''{email}''", $"Email '{email}' must be included in the self-healing password reset query");
        }

        foreach (var id in expectedIds)
        {
            sql.Should().Contain($"''{id}''", $"User ID '{id}' must be included in the self-healing password reset query");
        }
    }
}
