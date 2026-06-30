using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T69_GenerationValidationFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generation_validation_failures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    activity_type_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    validation_errors = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_validation_failures", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gen_val_failures_cefr_level",
                table: "generation_validation_failures",
                column: "cefr_level");

            migrationBuilder.CreateIndex(
                name: "ix_gen_val_failures_created_at",
                table: "generation_validation_failures",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_gen_val_failures_pattern_key",
                table: "generation_validation_failures",
                column: "pattern_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generation_validation_failures");
        }
    }
}
