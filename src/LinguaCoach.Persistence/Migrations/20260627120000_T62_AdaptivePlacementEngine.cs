using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T62_AdaptivePlacementEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extend placement_assessments with new columns
            migrationBuilder.AddColumn<DateTime>(
                name: "abandoned_at_utc",
                table: "placement_assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expired_at_utc",
                table: "placement_assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "overall_confidence",
                table: "placement_assessments",
                type: "double precision",
                nullable: true);

            // is_provisional and is_adaptive are guarded rather than added with a plain
            // AddColumn: this migration's filename timestamp (20260627120000) sorts AFTER
            // T64_PostPlacementModelSync (20260627093047), so on every environment that
            // migrates this repo from scratch, T64 runs BEFORE this migration and already
            // adds both columns defensively. A plain AddColumn here would fail with
            // "column already exists" and abort this entire migration's transaction,
            // silently halting every later migration (T63 onward) from ever applying.
            // See docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'placement_assessments' AND column_name = 'is_provisional'
    ) THEN
        ALTER TABLE placement_assessments ADD COLUMN is_provisional boolean NOT NULL DEFAULT false;
        ALTER TABLE placement_assessments ALTER COLUMN is_provisional DROP DEFAULT;
    END IF;
END $$;
");

            migrationBuilder.AddColumn<string>(
                name: "result_summary",
                table: "placement_assessments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "placement_assessments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'placement_assessments' AND column_name = 'is_adaptive'
    ) THEN
        ALTER TABLE placement_assessments ADD COLUMN is_adaptive boolean NOT NULL DEFAULT false;
        ALTER TABLE placement_assessments ALTER COLUMN is_adaptive DROP DEFAULT;
    END IF;
END $$;
");

            // Create placement_assessment_items table
            migrationBuilder.CreateTable(
                name: "placement_assessment_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    placement_assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    item_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    response = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: true),
                    evaluated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    item_order = table.Column<int>(type: "integer", nullable: false),
                    correct_answer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_placement_assessment_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_placement_assessment_items_placement_assessments_placement_a~",
                        column: x => x.placement_assessment_id,
                        principalTable: "placement_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create placement_skill_results table
            migrationBuilder.CreateTable(
                name: "placement_skill_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    placement_assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    estimated_cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    evidence_count = table.Column<int>(type: "integer", nullable: false),
                    strengths = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    weaknesses = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recommended_starting_objective_keys = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_placement_skill_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_placement_skill_results_placement_assessments_placement_asse~",
                        column: x => x.placement_assessment_id,
                        principalTable: "placement_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_placement_assessment_items_assessment_id",
                table: "placement_assessment_items",
                column: "placement_assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_placement_skill_results_assessment_id",
                table: "placement_skill_results",
                column: "placement_assessment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "placement_assessment_items");
            migrationBuilder.DropTable(name: "placement_skill_results");

            migrationBuilder.DropColumn(name: "abandoned_at_utc", table: "placement_assessments");
            migrationBuilder.DropColumn(name: "expired_at_utc", table: "placement_assessments");
            migrationBuilder.DropColumn(name: "overall_confidence", table: "placement_assessments");
            migrationBuilder.DropColumn(name: "is_provisional", table: "placement_assessments");
            migrationBuilder.DropColumn(name: "result_summary", table: "placement_assessments");
            migrationBuilder.DropColumn(name: "source", table: "placement_assessments");
            migrationBuilder.DropColumn(name: "is_adaptive", table: "placement_assessments");
        }
    }
}
