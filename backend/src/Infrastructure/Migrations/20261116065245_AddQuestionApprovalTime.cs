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
            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "practice_questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "exam_questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_practice_questions_approver_status_time",
                table: "practice_questions",
                columns: new[] { "approved_by", "approval_status", "approved_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exam_questions_approver_status_time",
                table: "exam_questions",
                columns: new[] { "approved_by", "approval_status", "approved_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_practice_questions_approver_status_time",
                table: "practice_questions");

            migrationBuilder.DropIndex(
                name: "ix_exam_questions_approver_status_time",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "practice_questions");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "exam_questions");
        }
    }
}
