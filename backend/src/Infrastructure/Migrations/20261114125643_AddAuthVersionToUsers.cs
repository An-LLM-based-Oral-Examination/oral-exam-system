using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthVersionToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE users ADD COLUMN IF NOT EXISTS auth_version integer NOT NULL DEFAULT 1;");
            migrationBuilder.Sql("""
                UPDATE users
                SET auth_version = 1
                WHERE id IN (
                    '00000000-0000-0000-0000-000000000001'::uuid,
                    '00000000-0000-0000-0000-000000000002'::uuid,
                    '00000000-0000-0000-0000-000000000003'::uuid,
                    '00000000-0000-0000-0000-000000000004'::uuid,
                    '00000000-0000-0000-0000-000000000005'::uuid,
                    '00000000-0000-0000-0000-000000000006'::uuid,
                    '00000000-0000-0000-0000-00000000000b'::uuid,
                    '00000000-0000-0000-0000-00000000000c'::uuid
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE users DROP COLUMN IF EXISTS auth_version;");
        }
    }
}
