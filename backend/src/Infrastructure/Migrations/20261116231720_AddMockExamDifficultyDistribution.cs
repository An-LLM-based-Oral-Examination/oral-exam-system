using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMockExamDifficultyDistribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE courses ADD COLUMN IF NOT EXISTS mock_exam_difficulty_distribution text NULL;");
            migrationBuilder.Sql("UPDATE courses SET mock_exam_difficulty_distribution = NULL WHERE id IN ('00000000-0000-0000-0000-000000000008'::uuid, '00000000-0000-0000-0000-000000000009'::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE courses DROP COLUMN IF EXISTS mock_exam_difficulty_distribution;");
        }
    }
}
