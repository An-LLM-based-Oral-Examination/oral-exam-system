using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionReviewRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(OralExamination.Infrastructure.Persistence.QuestionReviewRequestSchema.CreateSql);
            migrationBuilder.Sql(OralExamination.Infrastructure.Persistence.QuestionReviewRequestSchema.BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_review_request_items");

            migrationBuilder.DropTable(
                name: "question_review_requests");
        }
    }
}

