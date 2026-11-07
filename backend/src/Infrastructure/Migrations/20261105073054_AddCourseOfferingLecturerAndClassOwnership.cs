using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseOfferingLecturerAndClassOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "course_offering_id",
                table: "course_lecturers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "course_offering_id",
                table: "classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE course_lecturers AS cl
                SET course_offering_id = co.id,
                    semester_id = COALESCE(cl.semester_id, co.semester_id)
                FROM course_offerings AS co,
                     courses AS c
                WHERE cl.course_offering_id IS NULL
                  AND c.id = cl.course_id
                  AND co.course_id = cl.course_id
                  AND co.semester_id = COALESCE(cl.semester_id, c.semester_id);

                UPDATE classes AS class
                SET course_offering_id = co.id
                FROM course_offerings AS co
                WHERE class.course_offering_id IS NULL
                  AND co.course_id = class.course_id
                  AND co.semester_id = class.semester_id;
                """);

            migrationBuilder.UpdateData(
                table: "classes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "course_offering_id",
                value: new Guid("00000000-0000-0000-0000-000000000011"));

            migrationBuilder.CreateIndex(
                name: "uq_course_offering_lecturers",
                table: "course_lecturers",
                columns: new[] { "course_offering_id", "lecturer_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_classes_course_offering",
                table: "classes",
                column: "course_offering_id");

            migrationBuilder.AddForeignKey(
                name: "FK_classes_course_offerings_course_offering_id",
                table: "classes",
                column: "course_offering_id",
                principalTable: "course_offerings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_course_lecturers_course_offerings_course_offering_id",
                table: "course_lecturers",
                column: "course_offering_id",
                principalTable: "course_offerings",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_classes_course_offerings_course_offering_id",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "FK_course_lecturers_course_offerings_course_offering_id",
                table: "course_lecturers");

            migrationBuilder.DropIndex(
                name: "uq_course_offering_lecturers",
                table: "course_lecturers");

            migrationBuilder.DropIndex(
                name: "ix_classes_course_offering",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "course_offering_id",
                table: "course_lecturers");

            migrationBuilder.DropColumn(
                name: "course_offering_id",
                table: "classes");
        }
    }
}
