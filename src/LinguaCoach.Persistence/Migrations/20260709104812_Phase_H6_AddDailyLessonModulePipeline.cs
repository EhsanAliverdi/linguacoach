using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_H6_AddDailyLessonModulePipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_daily_module_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_for_date = table.Column<DateTime>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    selection_reason = table.Column<string>(type: "text", nullable: true),
                    fallback_reason = table.Column<string>(type: "text", nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: true),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_daily_module_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_daily_module_assignments_module_definitions_module_~",
                        column: x => x.module_definition_id,
                        principalTable: "module_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_daily_module_assignments_status",
                table: "student_daily_module_assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_daily_module_assignments_student_date",
                table: "student_daily_module_assignments",
                columns: new[] { "student_id", "assigned_for_date" });

            migrationBuilder.CreateIndex(
                name: "ix_daily_module_assignments_student_module",
                table: "student_daily_module_assignments",
                columns: new[] { "student_id", "module_definition_id" });

            migrationBuilder.CreateIndex(
                name: "IX_student_daily_module_assignments_module_definition_id",
                table: "student_daily_module_assignments",
                column: "module_definition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_daily_module_assignments");
        }
    }
}
