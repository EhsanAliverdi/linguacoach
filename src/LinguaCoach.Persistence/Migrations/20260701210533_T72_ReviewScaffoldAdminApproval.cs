using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T72_ReviewScaffoldAdminApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "admin_review_notes",
                table: "student_activity_readiness_items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "admin_review_reason",
                table: "student_activity_readiness_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "admin_review_status",
                table: "student_activity_readiness_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<DateTime>(
                name: "admin_reviewed_at_utc",
                table: "student_activity_readiness_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "admin_reviewed_by_user_id",
                table: "student_activity_readiness_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_readiness_items_admin_review_status",
                table: "student_activity_readiness_items",
                columns: new[] { "requires_admin_review", "admin_review_status" });

            // Backfill: items already held under the Phase 19A global gate become PendingReview
            // under the new per-item model instead of the column default (NotRequired).
            migrationBuilder.Sql(
                "UPDATE student_activity_readiness_items " +
                "SET admin_review_status = 'PendingReview' " +
                "WHERE requires_admin_review = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_readiness_items_admin_review_status",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "admin_review_notes",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "admin_review_reason",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "admin_review_status",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "admin_reviewed_at_utc",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "admin_reviewed_by_user_id",
                table: "student_activity_readiness_items");
        }
    }
}
