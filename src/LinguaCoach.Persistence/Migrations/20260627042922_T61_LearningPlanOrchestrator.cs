using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T61_LearningPlanOrchestrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_learning_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_level_snapshot = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    regeneration_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    regeneration_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    planned_lesson_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_learning_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_learning_plan_objectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_learning_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    context = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    planned_order = table.Column<int>(type: "integer", nullable: true),
                    is_review = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    blocked_by_objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_learning_plan_objectives", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_learning_plan_objectives_student_learning_plans_stu~",
                        column: x => x.student_learning_plan_id,
                        principalTable: "student_learning_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_plan_objectives_plan_order",
                table: "student_learning_plan_objectives",
                columns: new[] { "student_learning_plan_id", "planned_order" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_objectives_plan_status",
                table: "student_learning_plan_objectives",
                columns: new[] { "student_learning_plan_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_plans_student",
                table: "student_learning_plans",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_learning_plans_student_status",
                table: "student_learning_plans",
                columns: new[] { "student_profile_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_learning_plan_objectives");

            migrationBuilder.DropTable(
                name: "student_learning_plans");
        }
    }
}
