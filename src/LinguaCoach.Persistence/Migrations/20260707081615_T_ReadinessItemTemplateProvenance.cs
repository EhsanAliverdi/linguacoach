using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_ReadinessItemTemplateProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "form_io_schema_snapshot_json",
                table: "student_activity_readiness_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "generated_by_model",
                table: "student_activity_readiness_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "generated_by_provider",
                table: "student_activity_readiness_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "personalization_reason",
                table: "student_activity_readiness_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scoring_rules_snapshot_json",
                table: "student_activity_readiness_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_bank_item_id",
                table: "student_activity_readiness_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_template_id",
                table: "student_activity_readiness_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "validation_status",
                table: "student_activity_readiness_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_readiness_items_source_bank_item_id",
                table: "student_activity_readiness_items",
                column: "source_bank_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_readiness_items_source_template_id",
                table: "student_activity_readiness_items",
                column: "source_template_id");

            migrationBuilder.AddForeignKey(
                name: "FK_student_activity_readiness_items_activity_templates_source_~",
                table: "student_activity_readiness_items",
                column: "source_template_id",
                principalTable: "activity_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_student_activity_readiness_items_placement_item_definitions~",
                table: "student_activity_readiness_items",
                column: "source_bank_item_id",
                principalTable: "placement_item_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_student_activity_readiness_items_activity_templates_source_~",
                table: "student_activity_readiness_items");

            migrationBuilder.DropForeignKey(
                name: "FK_student_activity_readiness_items_placement_item_definitions~",
                table: "student_activity_readiness_items");

            migrationBuilder.DropIndex(
                name: "IX_student_activity_readiness_items_source_bank_item_id",
                table: "student_activity_readiness_items");

            migrationBuilder.DropIndex(
                name: "IX_student_activity_readiness_items_source_template_id",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "form_io_schema_snapshot_json",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "generated_by_model",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "generated_by_provider",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "personalization_reason",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "scoring_rules_snapshot_json",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "source_bank_item_id",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "source_template_id",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "validation_status",
                table: "student_activity_readiness_items");
        }
    }
}
