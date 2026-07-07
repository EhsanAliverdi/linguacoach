using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_ActivityTemplateBankFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    previous_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    curriculum_objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    activity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    pattern_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    form_io_base_schema_json = table.Column<string>(type: "jsonb", nullable: true),
                    generation_instructions = table.Column<string>(type: "text", nullable: true),
                    scoring_model_json = table.Column<string>(type: "jsonb", nullable: true),
                    validation_rules_json = table.Column<string>(type: "jsonb", nullable: true),
                    review_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "NotRequired"),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    estimated_duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    asset_requirements_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_templates_activity_templates_previous_version_id",
                        column: x => x.previous_version_id,
                        principalTable: "activity_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_templates_key_version",
                table: "activity_templates",
                columns: new[] { "key", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activity_templates_previous_version_id",
                table: "activity_templates",
                column: "previous_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_templates_review_status",
                table: "activity_templates",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "ix_activity_templates_skill_level_published",
                table: "activity_templates",
                columns: new[] { "skill", "cefr_level", "is_published" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_templates");
        }
    }
}
