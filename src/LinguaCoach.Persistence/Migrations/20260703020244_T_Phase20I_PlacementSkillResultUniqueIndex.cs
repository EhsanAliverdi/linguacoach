using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_Phase20I_PlacementSkillResultUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A completion race in PlacementAssessmentService (fixed alongside this migration)
            // could insert duplicate (assessment, skill) rows before either request saw the
            // other's insert. Remove existing duplicates, keeping the earliest row per pair,
            // before the unique index can be created.
            migrationBuilder.Sql(@"
                DELETE FROM placement_skill_results a
                USING placement_skill_results b
                WHERE a.placement_assessment_id = b.placement_assessment_id
                  AND a.skill = b.skill
                  AND (a.created_at > b.created_at
                       OR (a.created_at = b.created_at AND a.id > b.id));
            ");

            migrationBuilder.CreateIndex(
                name: "ux_placement_skill_results_assessment_skill",
                table: "placement_skill_results",
                columns: new[] { "placement_assessment_id", "skill" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_placement_skill_results_assessment_skill",
                table: "placement_skill_results");
        }
    }
}
