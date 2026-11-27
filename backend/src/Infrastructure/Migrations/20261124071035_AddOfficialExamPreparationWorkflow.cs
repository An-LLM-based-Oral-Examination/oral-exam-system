using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialExamPreparationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_sets_status",
                table: "exam_sets");

            migrationBuilder.AddColumn<int>(
                name: "easy_count",
                table: "exam_structures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "hard_count",
                table: "exam_structures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "medium_count",
                table: "exam_structures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "assignment_id",
                table: "exam_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_question_id",
                table: "exam_set_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_paper_copy",
                table: "exam_questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "exam_preparations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_set_count = table.Column<int>(type: "integer", nullable: false),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    handed_over_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    handed_over_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_preparations", x => x.id);
                    table.CheckConstraint("ck_exam_preparation_count", "required_set_count > 0");
                    table.ForeignKey(
                        name: "FK_exam_preparations_official_exam_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "official_exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_preparations_users_handed_over_by",
                        column: x => x.handed_over_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_preparations_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_set_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_set_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_exam_set_revisions_exam_sets_exam_set_id",
                        column: x => x.exam_set_id,
                        principalTable: "exam_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_set_revisions_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_structure_clos",
                columns: table => new
                {
                    structure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_structure_clos", x => new { x.structure_id, x.clo_id });
                    table.ForeignKey(
                        name: "FK_exam_structure_clos_course_clos_clo_id",
                        column: x => x.clo_id,
                        principalTable: "course_clos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_structure_clos_exam_structures_structure_id",
                        column: x => x.structure_id,
                        principalTable: "exam_structures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_authoring_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    preparation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    structure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    set_count = table.Column<int>(type: "integer", nullable: false),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_authoring_assignments", x => x.id);
                    table.CheckConstraint("ck_exam_assignment_count", "set_count > 0");
                    table.ForeignKey(
                        name: "FK_exam_authoring_assignments_exam_preparations_preparation_id",
                        column: x => x.preparation_id,
                        principalTable: "exam_preparations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_authoring_assignments_exam_structures_structure_id",
                        column: x => x.structure_id,
                        principalTable: "exam_structures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_authoring_assignments_users_assigned_by",
                        column: x => x.assigned_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_authoring_assignments_users_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exam_sets_assignment_id",
                table: "exam_sets",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "uq_exam_sets_course_code",
                table: "exam_sets",
                columns: new[] { "course_id", "set_code" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_set_assignment_session",
                table: "exam_sets",
                sql: "assignment_id IS NULL OR session_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_sets_status",
                table: "exam_sets",
                sql: "status IN ('DRAFT', 'SUBMITTED_FOR_REVIEW', 'OFFICIAL', 'HANDED_OVER', 'REJECTED', 'NEEDS_REVISION')");

            migrationBuilder.CreateIndex(
                name: "IX_exam_authoring_assignments_assigned_by",
                table: "exam_authoring_assignments",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "IX_exam_authoring_assignments_lecturer_id",
                table: "exam_authoring_assignments",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_authoring_assignments_preparation_id_lecturer_id_struc~",
                table: "exam_authoring_assignments",
                columns: new[] { "preparation_id", "lecturer_id", "structure_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_authoring_assignments_structure_id",
                table: "exam_authoring_assignments",
                column: "structure_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_preparations_handed_over_by",
                table: "exam_preparations",
                column: "handed_over_by");

            migrationBuilder.CreateIndex(
                name: "IX_exam_preparations_requested_by",
                table: "exam_preparations",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_exam_preparations_session_id",
                table: "exam_preparations",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_set_revisions_actor_id",
                table: "exam_set_revisions",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_set_revisions_exam_set_id_sequence",
                table: "exam_set_revisions",
                columns: new[] { "exam_set_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_structure_clos_clo_id",
                table: "exam_structure_clos",
                column: "clo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_sets_exam_authoring_assignments_assignment_id",
                table: "exam_sets",
                column: "assignment_id",
                principalTable: "exam_authoring_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_sets_exam_authoring_assignments_assignment_id",
                table: "exam_sets");

            migrationBuilder.DropTable(
                name: "exam_authoring_assignments");

            migrationBuilder.DropTable(
                name: "exam_set_revisions");

            migrationBuilder.DropTable(
                name: "exam_structure_clos");

            migrationBuilder.DropTable(
                name: "exam_preparations");

            migrationBuilder.DropIndex(
                name: "IX_exam_sets_assignment_id",
                table: "exam_sets");

            migrationBuilder.DropIndex(
                name: "uq_exam_sets_course_code",
                table: "exam_sets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_set_assignment_session",
                table: "exam_sets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_sets_status",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "easy_count",
                table: "exam_structures");

            migrationBuilder.DropColumn(
                name: "hard_count",
                table: "exam_structures");

            migrationBuilder.DropColumn(
                name: "medium_count",
                table: "exam_structures");

            migrationBuilder.DropColumn(
                name: "assignment_id",
                table: "exam_sets");

            migrationBuilder.DropColumn(
                name: "source_question_id",
                table: "exam_set_questions");

            migrationBuilder.DropColumn(
                name: "is_paper_copy",
                table: "exam_questions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_sets_status",
                table: "exam_sets",
                sql: "status IN ('DRAFT', 'SUBMITTED_FOR_REVIEW', 'OFFICIAL', 'HANDED_OVER', 'REJECTED')");
        }
    }
}
