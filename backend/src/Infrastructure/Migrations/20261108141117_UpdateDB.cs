using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Không gọi reconciler động từ migration lịch sử. Reconciler hiện tại có
            // chèn toàn bộ MigrationId, bao gồm chính migration này, làm EF ghi lịch sử
            // lần hai và lỗi unique key trên database sạch. Startup pipeline chịu trách
            // nhiệm chạy PostgresSchemaReconciler sau khi migrations hoàn tất.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
