using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceCandidatePublishFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "resource_candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_at_utc",
                table: "resource_candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_by_user_id",
                table: "resource_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_entity_id",
                table: "resource_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "published_entity_type",
                table: "resource_candidates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_candidates_is_published",
                table: "resource_candidates",
                column: "is_published");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_resource_candidates_is_published",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "is_published",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "published_at_utc",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "published_by_user_id",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "published_entity_id",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "published_entity_type",
                table: "resource_candidates");
        }
    }
}
