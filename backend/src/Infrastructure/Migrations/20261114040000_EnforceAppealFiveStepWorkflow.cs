using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.Infrastructure.Migrations;

[DbContext(typeof(OralExamDbContext))]
[Migration("20261114040000_EnforceAppealFiveStepWorkflow")]
public sealed class EnforceAppealFiveStepWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("ck_appeal_requests_status", "appeal_requests");
        migrationBuilder.AlterColumn<string>(
            name: "status", table: "appeal_requests", type: "character varying(40)", maxLength: 40,
            nullable: false, defaultValue: "SUBMITTED_TO_EXAM_OFFICE",
            oldClrType: typeof(string), oldType: "character varying(20)", oldMaxLength: 20,
            oldDefaultValue: "PENDING");
        migrationBuilder.Sql("UPDATE appeal_requests SET status = 'SUBMITTED_TO_EXAM_OFFICE' WHERE status = 'PENDING';");
        migrationBuilder.Sql("UPDATE appeal_requests SET status = 'ASSIGNED_TO_LECTURER' WHERE status = 'IN_REVIEW';");
        migrationBuilder.AddCheckConstraint(
            name: "ck_appeal_requests_status", table: "appeal_requests",
            sql: "status IN ('SUBMITTED_TO_EXAM_OFFICE', 'ASSIGNED_TO_LECTURER', 'REGRADED_BY_LECTURER', 'APPROVED', 'REJECTED', 'CANCELLED')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("ck_appeal_requests_status", "appeal_requests");
        migrationBuilder.Sql("UPDATE appeal_requests SET status = 'PENDING' WHERE status = 'SUBMITTED_TO_EXAM_OFFICE';");
        migrationBuilder.Sql("UPDATE appeal_requests SET status = 'IN_REVIEW' WHERE status IN ('ASSIGNED_TO_LECTURER', 'REGRADED_BY_LECTURER');");
        migrationBuilder.AlterColumn<string>(
            name: "status", table: "appeal_requests", type: "character varying(20)", maxLength: 20,
            nullable: false, defaultValue: "PENDING",
            oldClrType: typeof(string), oldType: "character varying(40)", oldMaxLength: 40,
            oldDefaultValue: "SUBMITTED_TO_EXAM_OFFICE");
        migrationBuilder.AddCheckConstraint(
            name: "ck_appeal_requests_status", table: "appeal_requests",
            sql: "status IN ('PENDING', 'IN_REVIEW', 'APPROVED', 'REJECTED', 'CANCELLED')");
    }
}
