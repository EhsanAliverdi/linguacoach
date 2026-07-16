using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_4_8_ImportPackageClaimAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_stamp",
                table: "import_upload_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claim_expires_at_utc",
                table: "import_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claimed_at_utc",
                table: "import_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "claimed_by_worker_id",
                table: "import_packages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_stamp",
                table: "import_packages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_import_packages_claim_expires",
                table: "import_packages",
                column: "claim_expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_import_packages_claim_expires",
                table: "import_packages");

            migrationBuilder.DropColumn(
                name: "concurrency_stamp",
                table: "import_upload_sessions");

            migrationBuilder.DropColumn(
                name: "claim_expires_at_utc",
                table: "import_packages");

            migrationBuilder.DropColumn(
                name: "claimed_at_utc",
                table: "import_packages");

            migrationBuilder.DropColumn(
                name: "claimed_by_worker_id",
                table: "import_packages");

            migrationBuilder.DropColumn(
                name: "concurrency_stamp",
                table: "import_packages");
        }
    }
}
