using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_I0_DropTypedBankTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cefr_grammar_profile_entries");

            migrationBuilder.DropTable(
                name: "cefr_reading_passages");

            migrationBuilder.DropTable(
                name: "cefr_reading_references");

            migrationBuilder.DropTable(
                name: "cefr_vocabulary_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cefr_grammar_profile_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    context_tags_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    description = table.Column<string>(type: "text", nullable: true),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    focus_tags_json = table.Column<string>(type: "text", nullable: true),
                    grammar_point = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cefr_grammar_profile_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_cefr_grammar_profile_entries_cefr_resource_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cefr_reading_passages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribution_text = table.Column<string>(type: "text", nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content_fingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    context_tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    estimated_reading_minutes = table.Column<int>(type: "integer", nullable: false),
                    focus_tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    passage_text = table.Column<string>(type: "text", nullable: false),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quality_score = table.Column<double>(type: "double precision", nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    topic_tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    word_count = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "cefr_reading_references",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    context_tags_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    difficulty_notes = table.Column<string>(type: "text", nullable: true),
                    focus_tags_json = table.Column<string>(type: "text", nullable: true),
                    reference_excerpt = table.Column<string>(type: "text", nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    text_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cefr_reading_references", x => x.id);
                    table.ForeignKey(
                        name: "FK_cefr_reading_references_cefr_resource_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cefr_vocabulary_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    context_tags_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    focus_tags_json = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    part_of_speech = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    word = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cefr_vocabulary_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_cefr_vocabulary_entries_cefr_resource_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cefr_grammar_profile_entries_level_point",
                table: "cefr_grammar_profile_entries",
                columns: new[] { "cefr_level", "grammar_point" });

            migrationBuilder.CreateIndex(
                name: "IX_cefr_grammar_profile_entries_source_id",
                table: "cefr_grammar_profile_entries",
                column: "source_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_cefr_reading_references_level",
                table: "cefr_reading_references",
                column: "cefr_level");

            migrationBuilder.CreateIndex(
                name: "IX_cefr_reading_references_source_id",
                table: "cefr_reading_references",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_cefr_vocabulary_entries_source_id",
                table: "cefr_vocabulary_entries",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_cefr_vocabulary_entries_word_level",
                table: "cefr_vocabulary_entries",
                columns: new[] { "word", "cefr_level" });
        }
    }
}
