using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceImportStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allows_commercial_use",
                table: "cefr_resource_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allows_student_display",
                table: "cefr_resource_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "attribution_text",
                table: "cefr_resource_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "download_url",
                table: "cefr_resource_sources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language_code",
                table: "cefr_resource_sources",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "source_version",
                table: "cefr_resource_sources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at_utc",
                table: "cefr_resource_sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "resource_import_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_resource_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    imported_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    import_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    file_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    parser_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ai_model_used = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    total_record_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    succeeded_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rejected_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    warning_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_summary = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_import_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_import_runs_cefr_resource_sources_cefr_resource_so~",
                        column: x => x.cefr_resource_source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_raw_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_import_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_record_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    raw_json = table.Column<string>(type: "text", nullable: true),
                    raw_text = table.Column<string>(type: "text", nullable: true),
                    raw_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    detected_language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    detected_format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    extraction_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    extraction_warnings_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_raw_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_raw_records_resource_import_runs_resource_import_r~",
                        column: x => x.resource_import_run_id,
                        principalTable: "resource_import_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_raw_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    canonical_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    normalized_json = table.Column<string>(type: "text", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cefr_confidence = table.Column<double>(type: "double precision", nullable: true),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    context_tags_json = table.Column<string>(type: "text", nullable: true, defaultValue: "[]"),
                    focus_tags_json = table.Column<string>(type: "text", nullable: true, defaultValue: "[]"),
                    grammar_tags_json = table.Column<string>(type: "text", nullable: true),
                    vocabulary_tags_json = table.Column<string>(type: "text", nullable: true),
                    pronunciation_tags_json = table.Column<string>(type: "text", nullable: true),
                    activity_suitability_tags_json = table.Column<string>(type: "text", nullable: true),
                    safety_tags_json = table.Column<string>(type: "text", nullable: true),
                    license_tags_json = table.Column<string>(type: "text", nullable: true),
                    quality_score = table.Column<double>(type: "double precision", nullable: true),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    content_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ai_analysis_json = table.Column<string>(type: "text", nullable: true),
                    validation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reject_reason = table.Column<string>(type: "text", nullable: true),
                    admin_notes = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_candidates", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_candidates_resource_raw_records_resource_raw_recor~",
                        column: x => x.resource_raw_record_id,
                        principalTable: "resource_raw_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resource_candidates_fingerprint",
                table: "resource_candidates",
                column: "content_fingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_resource_candidates_raw_record",
                table: "resource_candidates",
                column: "resource_raw_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_candidates_review_status",
                table: "resource_candidates",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "ix_resource_candidates_type",
                table: "resource_candidates",
                column: "candidate_type");

            migrationBuilder.CreateIndex(
                name: "ix_resource_candidates_validation_status",
                table: "resource_candidates",
                column: "validation_status");

            migrationBuilder.CreateIndex(
                name: "ix_resource_import_runs_source",
                table: "resource_import_runs",
                column: "cefr_resource_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_import_runs_status",
                table: "resource_import_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_resource_raw_records_run",
                table: "resource_raw_records",
                column: "resource_import_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_raw_records_run_hash",
                table: "resource_raw_records",
                columns: new[] { "resource_import_run_id", "raw_hash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_candidates");

            migrationBuilder.DropTable(
                name: "resource_raw_records");

            migrationBuilder.DropTable(
                name: "resource_import_runs");

            migrationBuilder.DropColumn(
                name: "allows_commercial_use",
                table: "cefr_resource_sources");

            migrationBuilder.DropColumn(
                name: "allows_student_display",
                table: "cefr_resource_sources");

            migrationBuilder.DropColumn(
                name: "attribution_text",
                table: "cefr_resource_sources");

            migrationBuilder.DropColumn(
                name: "download_url",
                table: "cefr_resource_sources");

            migrationBuilder.DropColumn(
                name: "language_code",
                table: "cefr_resource_sources");

            migrationBuilder.DropColumn(
                name: "source_version",
                table: "cefr_resource_sources");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "cefr_resource_sources");
        }
    }
}
