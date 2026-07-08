using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_E7_AddCefrReadingPassage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cefr_reading_passages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    passage_text = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    topic_tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    context_tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    focus_tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    estimated_reading_minutes = table.Column<int>(type: "integer", nullable: false),
                    attribution_text = table.Column<string>(type: "text", nullable: true),
                    content_fingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    quality_score = table.Column<double>(type: "double precision", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cefr_reading_passages", x => x.id);
                    table.ForeignKey(
                        name: "FK_cefr_reading_passages_cefr_resource_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cefr_reading_passages_created_at",
                table: "cefr_reading_passages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_cefr_reading_passages_fingerprint",
                table: "cefr_reading_passages",
                column: "content_fingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_cefr_reading_passages_level",
                table: "cefr_reading_passages",
                column: "cefr_level");

            migrationBuilder.CreateIndex(
                name: "ix_cefr_reading_passages_source",
                table: "cefr_reading_passages",
                column: "source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cefr_reading_passages");
        }
    }
}
