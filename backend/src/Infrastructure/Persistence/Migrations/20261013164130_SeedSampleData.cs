using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "semesters",
                columns: new[] { "id", "code", "created_at", "end_date", "is_active", "name", "start_date" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), "FA26", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 12, 31), true, "Fall 2026", new DateOnly(2026, 9, 1) });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "email", "full_name", "is_active", "role", "student_code", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@fpt.edu.vn", "Nguyen Quang Thanh", true, "admin", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "totnt@fe.edu.vn", "Nguyen Trong Tot", true, "lecturer", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hoanglvse170001@fpt.edu.vn", "Le Vu Hoang", true, "student", "SE170001", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "maittse170002@fpt.edu.vn", "Tran Thi Mai", true, "student", "SE170002", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "courses",
                columns: new[] { "id", "code", "created_at", "credits", "exam_input_mode", "has_follow_up", "is_active", "max_follow_up_questions", "name", "semester_id", "transcript_buffer_seconds" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), "PRN231", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, "VoiceWithTranscriptEdit", true, true, 2, "Building Cross-Platform Back-End Applications with .NET", new Guid("00000000-0000-0000-0000-000000000007"), 60 });

            migrationBuilder.InsertData(
                table: "classes",
                columns: new[] { "id", "code", "course_id", "created_at", "is_active", "lecturer_id", "max_students", "semester_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), "SE1801-NET", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("00000000-0000-0000-0000-000000000003"), 35, new Guid("00000000-0000-0000-0000-000000000007") });

            migrationBuilder.InsertData(
                table: "rubrics",
                columns: new[] { "id", "course_id", "created_at", "description", "is_active", "name", "total_max_score" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Đánh giá Clean Architecture", true, "Barem Đánh Giá Kiến Trúc .NET 8", 10.00m });

            migrationBuilder.InsertData(
                table: "class_enrollments",
                columns: new[] { "id", "class_id", "enrolled_at", "status", "student_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "enrolled", new Guid("00000000-0000-0000-0000-000000000005") },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "enrolled", new Guid("00000000-0000-0000-0000-000000000006") }
                });

            migrationBuilder.InsertData(
                table: "practice_questions",
                columns: new[] { "id", "bloom_level", "content", "course_id", "created_at", "difficulty", "follow_up_prompt", "is_active", "key_points", "rubric_id", "sample_answer", "title" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000017"), "Understand", "Hãy giải thích nguyên lý Dependency Inversion trong Clean Architecture .NET 8.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "medium", null, true, "[]", new Guid("00000000-0000-0000-0000-000000000013"), "Module cấp cao phụ thuộc abstraction. Application khai báo interface, Infrastructure hiện thực.", "Nguyên lý Dependency Inversion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "class_enrollments",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "class_enrollments",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "classes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "rubrics",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "semesters",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"));
        }
    }
}
