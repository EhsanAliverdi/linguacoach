using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_AddStudentGoalWeights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_goal_weights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goal_tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_goal_weights", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_goal_weights_student_profiles_student_id",
                        column: x => x.student_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_goal_weights_student",
                table: "student_goal_weights",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_goal_weights_student_tag",
                table: "student_goal_weights",
                columns: new[] { "student_id", "goal_tag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_goal_weights");
        }
    }
}
