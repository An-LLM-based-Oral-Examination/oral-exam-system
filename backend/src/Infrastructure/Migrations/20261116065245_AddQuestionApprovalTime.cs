using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionApprovalTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone NULL;");
            migrationBuilder.Sql("ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone NULL;");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_practice_questions_approver_status_time ON practice_questions (approved_by, approval_status, approved_at);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_exam_questions_approver_status_time ON exam_questions (approved_by, approval_status, approved_at);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_practice_questions_approver_status_time;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_exam_questions_approver_status_time;");
            migrationBuilder.Sql("ALTER TABLE practice_questions DROP COLUMN IF EXISTS approved_at;");
            migrationBuilder.Sql("ALTER TABLE exam_questions DROP COLUMN IF EXISTS approved_at;");
        }
    }
}
