using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseClosAndAntiCheatLogsAndExpandRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mock_exam_sessions_exam_sets_exam_set_id",
                table: "mock_exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_official_exam_sessions_exam_structures_exam_structure_id",
                table: "official_exam_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role",
                table: "users");

            migrationBuilder.AddColumn<Guid>(
                name: "examiner_user_id",
                table: "real_exam_session_shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_exam_published",
                table: "real_exam_session_shifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                table: "real_exam_session_shifts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "practice_questions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "APPROVED");

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                table: "practice_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_notes",
                table: "practice_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "practice_questions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by",
                table: "practice_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "exam_structure_id",
                table: "official_exam_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "exam_set_id",
                table: "mock_exam_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "required_set_count",
                table: "exam_structures",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "slot_definitions",
                table: "exam_structures",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "exam_sets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                table: "exam_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "exam_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "handed_over_at",
                table: "exam_sets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_notes",
                table: "exam_sets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                table: "exam_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "exam_sets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DRAFT");

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_at",
                table: "exam_sets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "approval_status",
                table: "exam_questions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "APPROVED",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "DRAFT");

            migrationBuilder.CreateTable(
                name: "course_clos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clo_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    bloom_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Understand"),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_clos", x => x.id);
                    table.CheckConstraint("ck_course_clos_bloom", "bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create')");
                    table.ForeignKey(
                        name: "fk_course_clos_course",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_anti_cheat_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "medium"),
                    details = table.Column<string>(type: "text", nullable: true),
                    violation_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_anti_cheat_logs", x => x.id);
                    table.CheckConstraint("ck_anti_cheat_severity", "severity IN ('low', 'medium', 'high', 'critical')");
                    table.ForeignKey(
                        name: "fk_anti_cheat_logs_shift",
                        column: x => x.shift_id,
                        principalTable: "real_exam_session_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_anti_cheat_logs_student",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_anti_cheat_logs_ticket",
                        column: x => x.ticket_id,
                        principalTable: "student_exam_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "course_clos",
                columns: new[] { "id", "bloom_level", "clo_code", "course_id", "created_at", "description", "is_active", "order_index" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000061"), "Understand", "CLO1", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "Hiểu và áp dụng kiến trúc ASP.NET Core Web API, Middleware, Dependency Injection", true, 1 },
                    { new Guid("00000000-0000-0000-0000-000000000062"), "Apply", "CLO2", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "Thiết kế và triển khai RESTful API, DTOs, Validation và xử lý lỗi chuẩn RFC 7807", true, 2 },
                    { new Guid("00000000-0000-0000-0000-000000000063"), "Analyze", "CLO3", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "Tích hợp Entity Framework Core, Migration, tối ưu hóa truy vấn và giao dịch cơ sở dữ liệu", true, 3 },
                    { new Guid("00000000-0000-0000-0000-000000000064"), "Evaluate", "CLO4", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "Bảo mật hệ thống bằng JWT, OAuth 2.0, phân quyền RBAC và kiểm thử tự động", true, 4 }
                });

            migrationBuilder.UpdateData(
                table: "exam_sets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000019"),
                columns: new[] { "approved_at", "approved_by", "created_by", "handed_over_at", "review_notes", "session_id", "status", "submitted_at" },
                values: new object[] { null, null, null, null, null, null, "OFFICIAL", null });

            migrationBuilder.InsertData(
                table: "exam_sets",
                columns: new[] { "id", "approved_at", "approved_by", "course_id", "created_by", "generated_at", "handed_over_at", "is_active", "review_notes", "session_id", "set_code", "status", "structure_id", "submitted_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000001a"), null, null, new Guid("00000000-0000-0000-0000-000000000008"), null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, null, null, "PRN231-SET02", "OFFICIAL", new Guid("00000000-0000-0000-0000-000000000018"), null });

            migrationBuilder.UpdateData(
                table: "exam_structures",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000018"),
                columns: new[] { "required_set_count", "slot_definitions" },
                values: new object[] { 1, "[]" });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"),
                columns: new[] { "approval_status", "approved_by", "review_notes", "source", "submitted_by" },
                values: new object[] { "APPROVED", null, null, "manual", null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                columns: new[] { "approval_status", "approved_by", "review_notes", "source", "submitted_by" },
                values: new object[] { "APPROVED", null, null, "manual", null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000102"),
                columns: new[] { "approval_status", "approved_by", "review_notes", "source", "submitted_by" },
                values: new object[] { "APPROVED", null, null, "manual", null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000103"),
                columns: new[] { "approval_status", "approved_by", "review_notes", "source", "submitted_by" },
                values: new object[] { "APPROVED", null, null, "manual", null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000104"),
                columns: new[] { "approval_status", "approved_by", "review_notes", "source", "submitted_by" },
                values: new object[] { "APPROVED", null, null, "manual", null });

            migrationBuilder.UpdateData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000105"),
                columns: new[] { "approval_status", "approved_by", "review_notes", "source", "submitted_by" },
                values: new object[] { "APPROVED", null, null, "manual", null });

            migrationBuilder.UpdateData(
                table: "real_exam_session_shifts",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"),
                columns: new[] { "examiner_user_id", "published_at" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "student_exam_tickets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"),
                column: "exam_set_id",
                value: new Guid("00000000-0000-0000-0000-00000000001a"));

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "email", "full_name", "is_active", "password_hash", "role", "student_code", "updated_at", "username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000000c"), new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "khaothi@fe.edu.vn", "Phòng Khảo Thí FPTU", true, "pbkdf2-sha256.210000.gXk3elR90D2mDzQO6YESxg==.MFGqqSEgh1msSXnIZj554d/MyQvcksQQD0hIZNBiFNw=", "examination_officer", null, new DateTime(2026, 10, 22, 12, 0, 0, 0, DateTimeKind.Utc), "khaothi" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('admin', 'department_head', 'examination_officer', 'lecturer', 'proctor', 'student')");

            migrationBuilder.CreateIndex(
                name: "uq_tickets_shift_exam_set",
                table: "student_exam_tickets",
                columns: new[] { "shift_id", "exam_set_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_real_exam_shifts_examiner",
                table: "real_exam_session_shifts",
                column: "examiner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_practice_questions_approval_status",
                table: "practice_questions",
                column: "approval_status");

            migrationBuilder.CreateIndex(
                name: "ix_practice_questions_approved",
                table: "practice_questions",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_practice_questions_submitted",
                table: "practice_questions",
                column: "submitted_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_practice_questions_approval_status",
                table: "practice_questions",
                sql: "approval_status IN ('DRAFT', 'SUBMITTED_FOR_REVIEW', 'APPROVED', 'NEEDS_REVISION', 'REJECTED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_struct_required_sets",
                table: "exam_structures",
                sql: "required_set_count >= 1");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sets_approver",
                table: "exam_sets",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sets_creator",
                table: "exam_sets",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sets_session",
                table: "exam_sets",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sets_status",
                table: "exam_sets",
                column: "status");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_sets_status",
                table: "exam_sets",
                sql: "status IN ('DRAFT', 'SUBMITTED_FOR_REVIEW', 'OFFICIAL', 'HANDED_OVER', 'REJECTED')");

            migrationBuilder.CreateIndex(
                name: "ix_course_clos_course_id",
                table: "course_clos",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "uq_course_clos_course_code",
                table: "course_clos",
                columns: new[] { "course_id", "clo_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_anti_cheat_logs_occurred",
                table: "exam_anti_cheat_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_exam_anti_cheat_logs_shift",
                table: "exam_anti_cheat_logs",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_anti_cheat_logs_student",
                table: "exam_anti_cheat_logs",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_anti_cheat_logs_ticket",
                table: "exam_anti_cheat_logs",
                column: "ticket_id");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_sets_official_exam_sessions_session_id",
                table: "exam_sets",
                column: "session_id",
                principalTable: "official_exam_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_exam_sets_users_approved_by",
                table: "exam_sets",
                column: "approved_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_exam_sets_users_created_by",
                table: "exam_sets",
                column: "created_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_mock_exam_sessions_exam_sets_exam_set_id",
                table: "mock_exam_sessions",
                column: "exam_set_id",
                principalTable: "exam_sets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_official_exam_sessions_exam_structures_exam_structure_id",
                table: "official_exam_sessions",
                column: "exam_structure_id",
                principalTable: "exam_structures",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_practice_questions_users_approved_by",
                table: "practice_questions",
                column: "approved_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_practice_questions_users_submitted_by",
                table: "practice_questions",
                column: "submitted_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_real_exam_session_shifts_users_examiner_user_id",
                table: "real_exam_session_shifts",
                column: "examiner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_sets_official_exam_sessions_session_id",
                table: "exam_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_exam_sets_users_approved_by",
                table: "exam_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_exam_sets_users_created_by",
                table: "exam_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_mock_exam_sessions_exam_sets_exam_set_id",
                table: "mock_exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_official_exam_sessions_exam_structures_exam_structure_id",
                table: "official_exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_practice_questions_users_approved_by",
                table: "practice_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_practice_questions_users_submitted_by",
                table: "practice_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_real_exam_session_shifts_users_examiner_user_id",
                table: "real_exam_session_shifts");

            migrationBuilder.DropTable(
                name: "course_clos");

            migrationBuilder.DropTable(
                name: "exam_anti_cheat_logs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role",
                table: "users");

            migrationBuilder.DropIndex(
                name: "uq_tickets_shift_exam_set",
                table: "student_exam_tickets");

            migrationBuilder.DropIndex(
                name: "ix_real_exam_shifts_examiner",
                table: "real_exam_session_shifts");

            migrationBuilder.DropIndex(
                name: "ix_practice_questions_approval_status",
                table: "practice_questions");

            migrationBuilder.DropIndex(
                name: "ix_practice_questions_approved",
                table: "practice_questions");

            migrationBuilder.DropIndex(
                name: "ix_practice_questions_submitted",
                table: "practice_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_practice_questions_approval_status",
                table: "practice_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_struct_required_sets",
                table: "exam_structures");

            migrationBuilder.DropIndex(
                name: "ix_exam_sets_approver",
                table: "exam_sets");

            migrationBuilder.DropIndex(
                name: "ix_exam_sets_creator",
                table: "exam_sets");

            migrationBuilder.DropIndex(
                name: "ix_exam_sets_session",
                table: "exam_sets");

            migrationBuilder.DropIndex(
                name: "ix_exam_sets_status",
                table: "exam_sets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_sets_status",
                table: "exam_sets");

            migrationBuilder.DeleteData(
                table: "exam_sets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000001a"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000000c"));

            migrationBuilder.DropColumn(
                name: "examiner_user_id",
                table: "real_exam_session_shifts");

            migrationBuilder.DropColumn(
                name: "is_exam_published",
                table: "real_exam_session_shifts");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "real_exam_session_shifts");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "practice_questions");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "practice_questions");

            migrationBuilder.DropColumn(
                name: "review_notes",
                table: "practice_questions");

            migrationBuilder.DropColumn(
                name: "source",
                table: "practice_questions");

            migrationBuilder.DropColumn(
                name: "submitted_by",
                table: "practice_questions");

            migrationBuilder.DropColumn(
                name: "required_set_count",
                table: "exam_structures");

            migrationBuilder.DropColumn(
                name: "slot_definitions",
                table: "exam_structures");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "handed_over_at",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "review_notes",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "status",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                table: "exam_sets");

            migrationBuilder.AlterColumn<Guid>(
                name: "exam_structure_id",
                table: "official_exam_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "exam_set_id",
                table: "mock_exam_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "approval_status",
                table: "exam_questions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DRAFT",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "APPROVED");

            migrationBuilder.UpdateData(
                table: "student_exam_tickets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"),
                column: "exam_set_id",
                value: new Guid("00000000-0000-0000-0000-000000000019"));

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role",
                table: "users",
                sql: "role IN ('admin', 'department_head', 'lecturer', 'proctor', 'student')");

            migrationBuilder.AddForeignKey(
                name: "FK_mock_exam_sessions_exam_sets_exam_set_id",
                table: "mock_exam_sessions",
                column: "exam_set_id",
                principalTable: "exam_sets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_official_exam_sessions_exam_structures_exam_structure_id",
                table: "official_exam_sessions",
                column: "exam_structure_id",
                principalTable: "exam_structures",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
