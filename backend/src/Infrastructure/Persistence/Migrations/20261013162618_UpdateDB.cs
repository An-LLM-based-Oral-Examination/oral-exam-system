using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "exam_input_mode",
                table: "official_exam_sessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "VoiceWithTranscriptEdit",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "VoiceAndTextInput");

            migrationBuilder.AlterColumn<string>(
                name: "exam_input_mode",
                table: "courses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "VoiceWithTranscriptEdit",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "VoiceAndTextInput");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_activity_at",
                table: "practice_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "selected_difficulties",
                table: "practice_sessions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "system_configs",
                columns: new[] { "id", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000028"), "Số lượng câu hỏi luyện tập tối thiểu cho chế độ Dễ đến Khó do Admin cấu hình", "MinMixedPracticeQuestions", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "3" },
                    { new Guid("00000000-0000-0000-0000-000000000029"), "Số lượng câu hỏi luyện tập tối đa cho chế độ Dễ đến Khó do Admin cấu hình", "MaxMixedPracticeQuestions", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "10" },
                    { new Guid("00000000-0000-0000-0000-000000000030"), "Thời gian đệm hiệu đính transcript mặc định (giây) do Admin cấu hình", "TranscriptBufferSeconds", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "60" },
                    { new Guid("00000000-0000-0000-0000-000000000031"), "Số lượng câu hỏi follow-up luyện tập tối đa do Admin cấu hình (1-5 câu)", "MaxPracticeFollowUpQuestions", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "2" },
                    { new Guid("00000000-0000-0000-0000-000000000032"), "Thời gian timeout không tương tác của phiên luyện tập (phút)", "SessionInactivityTimeoutMinutes", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "10" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_practice_sessions_last_activity",
                table: "practice_sessions",
                columns: new[] { "status", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user",
                table: "refresh_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_practice_sessions_last_activity",
                table: "practice_sessions");

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000029"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000030"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000031"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"));

            migrationBuilder.DropColumn(
                name: "last_activity_at",
                table: "practice_sessions");

            migrationBuilder.DropColumn(
                name: "selected_difficulties",
                table: "practice_sessions");

            migrationBuilder.AlterColumn<string>(
                name: "exam_input_mode",
                table: "official_exam_sessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "VoiceAndTextInput",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "VoiceWithTranscriptEdit");

            migrationBuilder.AlterColumn<string>(
                name: "exam_input_mode",
                table: "courses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "VoiceAndTextInput",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "VoiceWithTranscriptEdit");

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }
    }
}
