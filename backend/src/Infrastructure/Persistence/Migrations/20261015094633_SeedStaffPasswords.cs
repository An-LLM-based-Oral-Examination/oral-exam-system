using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedStaffPasswords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "password_hash", "username" },
                values: new object[] { "pbkdf2-sha256.210000.5K2wrCgwPiV5n+ABUXfgiA==.008qe4tj/RRL8pPAl4EYA+A5+pfxVetYA6GDQh/R0mY=", "admin" });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                columns: new[] { "password_hash", "username" },
                values: new object[] { "pbkdf2-sha256.210000.HpTuTN7YNAPOdlxcQWqpHA==.ZwGxFXu923/XOLQOpsxyOeQVE5WyKcnaoz26ljezdsM=", "headse" });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "password_hash", "username" },
                values: new object[] { "pbkdf2-sha256.210000.NAemlIZmGVkenMK0C4M/nA==.QMwd1s/sMi+q+NaEjyE4zZmnjyaaoiXq/fPgSa/Bxrk=", "totnt" });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "password_hash", "username" },
                values: new object[] { "pbkdf2-sha256.210000.XWSrNfAl9MJ4kGLb8pTU3Q==.3Qk6V00ldedgCY86HSofmJWukSF1OjCzPzAphfouT30=", "haind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "password_hash", "username" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                columns: new[] { "password_hash", "username" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "password_hash", "username" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "password_hash", "username" },
                values: new object[] { null, null });
        }
    }
}
