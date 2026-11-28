using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueActiveSampleExamSetIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "uq_exam_sets_active_sample_structure",
                table: "exam_sets",
                column: "structure_id",
                unique: true,
                filter: "is_sample_set = true AND is_active = true AND status <> 'REJECTED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_exam_sets_active_sample_structure",
                table: "exam_sets");
        }
    }
}
