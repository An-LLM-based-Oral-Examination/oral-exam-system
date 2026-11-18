using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseInsensitiveUniqueIndexesToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ix_users_email_lower ON users (lower(email));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ix_users_username_lower ON users (lower(username)) WHERE username IS NOT NULL;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ix_users_student_code_lower ON users (lower(student_code)) WHERE student_code IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_users_email_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_users_username_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_users_student_code_lower;");
        }
    }
}
