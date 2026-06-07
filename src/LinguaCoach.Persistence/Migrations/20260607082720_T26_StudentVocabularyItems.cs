using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T26_StudentVocabularyItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_vocabulary_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_activity_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_learning_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    term = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    suggested_phrase = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    meaning_or_explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    example_sentence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    seen_count = table.Column<int>(type: "integer", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_review_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_vocabulary_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_vocabulary_items_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_vocabulary_items_student_status",
                table: "student_vocabulary_items",
                columns: new[] { "student_profile_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_student_vocabulary_items_student_term_cat",
                table: "student_vocabulary_items",
                columns: new[] { "student_profile_id", "term", "category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_vocabulary_items");
        }
    }
}
