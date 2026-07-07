using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_PlacementItemCalibrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "calibration_sample_size",
                table: "placement_item_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "difficulty_band",
                table: "placement_item_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<double>(
                name: "discrimination_index",
                table: "placement_item_definitions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "evidence_weight",
                table: "placement_item_definitions",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<int>(
                name: "item_version",
                table: "placement_item_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_version_id",
                table: "placement_item_definitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_status",
                table: "placement_item_definitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.CreateIndex(
                name: "IX_placement_item_definitions_previous_version_id",
                table: "placement_item_definitions",
                column: "previous_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_placement_item_definitions_review_status",
                table: "placement_item_definitions",
                column: "review_status");

            migrationBuilder.AddForeignKey(
                name: "FK_placement_item_definitions_placement_item_definitions_previ~",
                table: "placement_item_definitions",
                column: "previous_version_id",
                principalTable: "placement_item_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_placement_item_definitions_placement_item_definitions_previ~",
                table: "placement_item_definitions");

            migrationBuilder.DropIndex(
                name: "IX_placement_item_definitions_previous_version_id",
                table: "placement_item_definitions");

            migrationBuilder.DropIndex(
                name: "ix_placement_item_definitions_review_status",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "calibration_sample_size",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "difficulty_band",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "discrimination_index",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "evidence_weight",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "item_version",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "previous_version_id",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "review_status",
                table: "placement_item_definitions");
        }
    }
}
