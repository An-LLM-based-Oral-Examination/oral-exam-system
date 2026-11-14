using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicProgramAndCourseOfferings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Các model snapshot trước migration đã kỳ vọng cột này nhưng migration
            // ban đầu không tạo nó và phụ thuộc ngầm vào reconciler. Bổ sung idempotent
            // để chuỗi migration chạy được trên PostgreSQL sạch.
            migrationBuilder.Sql("""
                ALTER TABLE courses
                    ADD COLUMN IF NOT EXISTS department_head_user_id uuid NULL;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "academic_program_id",
                table: "courses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "academic_programs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    department_head_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_programs", x => x.id);
                    table.ForeignKey(
                        name: "FK_academic_programs_users_department_head_user_id",
                        column: x => x.department_head_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_offerings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_offerings", x => x.id);
                    table.ForeignKey(
                        name: "FK_course_offerings_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_course_offerings_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "academic_programs",
                columns: new[] { "id", "code", "created_at", "department_head_user_id", "is_active", "name", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), "SE", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000002"), true, "Software Engineering", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "course_offerings",
                columns: new[] { "id", "course_id", "created_at", "is_enabled", "semester_id", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("00000000-0000-0000-0000-000000000007"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000009"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("00000000-0000-0000-0000-000000000007"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                columns: new[] { "academic_program_id", "department_head_user_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                columns: new[] { "academic_program_id", "department_head_user_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.CreateIndex(
                name: "ix_courses_academic_program",
                table: "courses",
                column: "academic_program_id");

            migrationBuilder.CreateIndex(
                name: "ix_academic_programs_code",
                table: "academic_programs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_academic_programs_department_head",
                table: "academic_programs",
                column: "department_head_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_offerings_semester",
                table: "course_offerings",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "uq_course_offerings_course_semester",
                table: "course_offerings",
                columns: new[] { "course_id", "semester_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_courses_academic_programs_academic_program_id",
                table: "courses",
                column: "academic_program_id",
                principalTable: "academic_programs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Backfill every pre-existing catalog course, not only HasData rows.
            migrationBuilder.Sql(
                """
                UPDATE courses
                SET academic_program_id = '00000000-0000-0000-0000-000000000010',
                    department_head_user_id = COALESCE(
                        department_head_user_id,
                        '00000000-0000-0000-0000-000000000002')
                WHERE academic_program_id IS NULL;

                INSERT INTO course_offerings
                    (id, course_id, semester_id, is_enabled, created_at, updated_at)
                SELECT gen_random_uuid(), c.id, c.semester_id, c.is_active, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM courses c
                ON CONFLICT (course_id, semester_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_courses_academic_programs_academic_program_id",
                table: "courses");

            migrationBuilder.DropTable(
                name: "academic_programs");

            migrationBuilder.DropTable(
                name: "course_offerings");

            migrationBuilder.DropIndex(
                name: "ix_courses_academic_program",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "academic_program_id",
                table: "courses");

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "department_head_user_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "department_head_user_id",
                value: null);
        }
    }
}
