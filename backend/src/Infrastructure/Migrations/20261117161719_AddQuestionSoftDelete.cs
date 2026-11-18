using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE practice_questions ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT FALSE;");
            migrationBuilder.Sql("ALTER TABLE exam_questions ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT FALSE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE practice_questions DROP COLUMN IF EXISTS is_deleted;");
            migrationBuilder.Sql("ALTER TABLE exam_questions DROP COLUMN IF EXISTS is_deleted;");
        }
    }
}
