using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionImagesAndMultimodalSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_description",
                table: "practice_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "practice_questions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_description",
                table: "exam_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "exam_questions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "exam_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000020"),
                columns: new[] { "image_description", "image_url" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"),
                columns: new[] { "image_description", "image_url" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                columns: new[] { "image_description", "image_url" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000102"),
                columns: new[] { "image_description", "image_url" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000103"),
                columns: new[] { "image_description", "image_url" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000104"),
                columns: new[] { "image_description", "image_url" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000105"),
                columns: new[] { "image_description", "image_url" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_description",
                table: "practice_questions");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "practice_questions");

            migrationBuilder.DropColumn(
                name: "image_description",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "exam_questions");
        }
    }
}
