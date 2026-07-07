using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_CefrResourceBankFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cefr_resource_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    license_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    usage_restriction_notes = table.Column<string>(type: "text", nullable: true),
                    is_import_approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    imported_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cefr_resource_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cefr_descriptors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    can_do_statement = table.Column<string>(type: "text", nullable: false),
                    citation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cefr_descriptors", x => x.id);
                    table.ForeignKey(
                        name: "FK_cefr_descriptors_cefr_resource_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cefr_grammar_profile_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    grammar_point = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                name: "cefr_reading_references",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    text_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    difficulty_notes = table.Column<string>(type: "text", nullable: true),
                    reference_excerpt = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    part_of_speech = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                name: "ix_cefr_descriptors_level_skill",
                table: "cefr_descriptors",
                columns: new[] { "cefr_level", "skill" });

            migrationBuilder.CreateIndex(
                name: "IX_cefr_descriptors_source_id",
                table: "cefr_descriptors",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_cefr_grammar_profile_entries_level_point",
                table: "cefr_grammar_profile_entries",
                columns: new[] { "cefr_level", "grammar_point" });

            migrationBuilder.CreateIndex(
                name: "IX_cefr_grammar_profile_entries_source_id",
                table: "cefr_grammar_profile_entries",
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
                name: "ix_cefr_resource_sources_name",
                table: "cefr_resource_sources",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cefr_vocabulary_entries_source_id",
                table: "cefr_vocabulary_entries",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_cefr_vocabulary_entries_word_level",
                table: "cefr_vocabulary_entries",
                columns: new[] { "word", "cefr_level" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cefr_descriptors");

            migrationBuilder.DropTable(
                name: "cefr_grammar_profile_entries");

            migrationBuilder.DropTable(
                name: "cefr_reading_references");

            migrationBuilder.DropTable(
                name: "cefr_vocabulary_entries");

            migrationBuilder.DropTable(
                name: "cefr_resource_sources");
        }
    }
}
