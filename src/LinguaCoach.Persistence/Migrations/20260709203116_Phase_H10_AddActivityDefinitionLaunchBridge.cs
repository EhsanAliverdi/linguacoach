using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_H10_AddActivityDefinitionLaunchBridge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_activity_definition_launches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learn_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    launched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_activity_definition_launches", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_activity_definition_launches_activity_definitions_a~",
                        column: x => x.activity_definition_id,
                        principalTable: "activity_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_activity_definition_launches_learn_items_learn_item~",
                        column: x => x.learn_item_id,
                        principalTable: "learn_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_activity_definition_launches_learning_activities_le~",
                        column: x => x.learning_activity_id,
                        principalTable: "learning_activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_activity_definition_launches_module_definitions_mod~",
                        column: x => x.module_definition_id,
                        principalTable: "module_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_definition_launches_activity",
                table: "student_activity_definition_launches",
                column: "activity_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_definition_launches_learning_activity",
                table: "student_activity_definition_launches",
                column: "learning_activity_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_activity_definition_launches_module",
                table: "student_activity_definition_launches",
                column: "module_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_definition_launches_student_launched",
                table: "student_activity_definition_launches",
                columns: new[] { "student_id", "launched_at" });

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_definition_launches_learn_item_id",
                table: "student_activity_definition_launches",
                column: "learn_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_activity_definition_launches");
        }
    }
}
