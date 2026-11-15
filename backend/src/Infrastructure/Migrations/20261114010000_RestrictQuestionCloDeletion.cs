using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.Infrastructure.Migrations;

// Metadata is declared here rather than in a duplicate generated designer file.
[DbContext(typeof(OralExamDbContext))]
[Migration("20261114010000_RestrictQuestionCloDeletion")]
public sealed class RestrictQuestionCloDeletion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing snapshots included these optional columns, but older deployments
        // obtained them from schema reconciliation instead of an EF migration.
        migrationBuilder.Sql("ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;");
        migrationBuilder.Sql("ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS clo_id uuid NULL;");
        ReplaceForeignKeys(migrationBuilder, ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        ReplaceForeignKeys(migrationBuilder, ReferentialAction.SetNull);

    private static void ReplaceForeignKeys(MigrationBuilder migrationBuilder, ReferentialAction action)
    {
        foreach (var table in new[] { "practice_questions", "exam_questions" })
        {
            var name = $"FK_{table}_course_clos_clo_id";
            migrationBuilder.Sql($"ALTER TABLE {table} DROP CONSTRAINT IF EXISTS \"{name}\";");
            migrationBuilder.AddForeignKey(name, table, "clo_id", "course_clos", principalColumn: "id", onDelete: action);
        }
    }
}
