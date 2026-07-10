using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_I4_Pass3_RenameDailyLessonToTodayPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase I4 Pass 3 — "Daily Lesson" -> "Today Plan" rename. Pure table/index rename, no
            // column changes and no data loss. dotnet ef migrations add initially scaffolded this
            // as DropTable+CreateTable (EF's diff heuristic doesn't recognize a table rename when
            // the table's own name changes in the same diff); hand-rewritten to RenameTable/
            // RenameIndex only, matching Pass 1's convention for this project's renames.
            migrationBuilder.RenameTable(
                name: "student_daily_module_assignments",
                newName: "student_today_plan_module_assignments");

            migrationBuilder.RenameIndex(
                name: "ix_daily_module_assignments_status",
                table: "student_today_plan_module_assignments",
                newName: "ix_today_plan_module_assignments_status");

            migrationBuilder.RenameIndex(
                name: "ix_daily_module_assignments_student_date",
                table: "student_today_plan_module_assignments",
                newName: "ix_today_plan_module_assignments_student_date");

            migrationBuilder.RenameIndex(
                name: "ix_daily_module_assignments_student_module",
                table: "student_today_plan_module_assignments",
                newName: "ix_today_plan_module_assignments_student_module");

            migrationBuilder.RenameIndex(
                name: "IX_student_daily_module_assignments_module_id",
                table: "student_today_plan_module_assignments",
                newName: "IX_student_today_plan_module_assignments_module_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_student_today_plan_module_assignments_module_id",
                table: "student_today_plan_module_assignments",
                newName: "IX_student_daily_module_assignments_module_id");

            migrationBuilder.RenameIndex(
                name: "ix_today_plan_module_assignments_student_module",
                table: "student_today_plan_module_assignments",
                newName: "ix_daily_module_assignments_student_module");

            migrationBuilder.RenameIndex(
                name: "ix_today_plan_module_assignments_student_date",
                table: "student_today_plan_module_assignments",
                newName: "ix_daily_module_assignments_student_date");

            migrationBuilder.RenameIndex(
                name: "ix_today_plan_module_assignments_status",
                table: "student_today_plan_module_assignments",
                newName: "ix_daily_module_assignments_status");

            migrationBuilder.RenameTable(
                name: "student_today_plan_module_assignments",
                newName: "student_daily_module_assignments");
        }
    }
}
