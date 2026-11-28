using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamReuseAndBankOrigins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "origin_question_id",
                table: "exam_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reuse_percent",
                table: "exam_preparations",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.CreateIndex(
                name: "IX_exam_questions_origin_question_id",
                table: "exam_questions",
                column: "origin_question_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_preparation_reuse",
                table: "exam_preparations",
                sql: "reuse_percent BETWEEN 10 AND 50");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_questions_exam_questions_origin_question_id",
                table: "exam_questions",
                column: "origin_question_id",
                principalTable: "exam_questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_questions_exam_questions_origin_question_id",
                table: "exam_questions");

            migrationBuilder.DropIndex(
                name: "IX_exam_questions_origin_question_id",
                table: "exam_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_preparation_reuse",
                table: "exam_preparations");

            migrationBuilder.DropColumn(
                name: "origin_question_id",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "reuse_percent",
                table: "exam_preparations");
        }
    }
}
