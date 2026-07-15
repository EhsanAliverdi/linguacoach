using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_4_ImportPackagesAndAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "import_package_id",
                table: "resource_import_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metadata_provenance_json",
                table: "resource_candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stt_model_name",
                table: "resource_candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stt_provider_name",
                table: "resource_candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "transcript_confidence",
                table: "resource_candidates",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transcript_origin",
                table: "resource_candidates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "import_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_resource_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_archive_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    archive_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    archive_checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    compressed_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    processing_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    processing_mode_reason = table.Column<string>(type: "text", nullable: true),
                    manifest_json = table.Column<string>(type: "text", nullable: true),
                    approved_import_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_summary = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    files_inspected_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    files_processed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    records_processed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    candidates_created_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    candidates_failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_completed_stage_index = table.Column<int>(type: "integer", nullable: false, defaultValue: -1),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_packages", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_packages_cefr_resource_sources_cefr_resource_source_~",
                        column: x => x.cefr_resource_source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    relative_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    detected_media_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    file_extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    compressed_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    uncompressed_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    role_origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    processing_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    validation_errors_json = table.Column<string>(type: "text", nullable: true),
                    validation_warnings_json = table.Column<string>(type: "text", nullable: true),
                    uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processing_metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_assets", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_assets_import_packages_import_package_id",
                        column: x => x.import_package_id,
                        principalTable: "import_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    profile_json = table.Column<string>(type: "text", nullable: false),
                    ai_provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ai_model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sample_asset_ids_json = table.Column<string>(type: "text", nullable: false),
                    estimated_candidate_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    estimated_cost_expected = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false, defaultValue: 0m),
                    estimated_cost_min = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false, defaultValue: 0m),
                    estimated_cost_max = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false, defaultValue: 0m),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    plan_estimate_json = table.Column<string>(type: "text", nullable: true),
                    pricing_snapshot_json = table.Column<string>(type: "text", nullable: true),
                    approved_cost_ceiling = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    change_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    pause_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_profiles_import_packages_import_package_id",
                        column: x => x.import_package_id,
                        principalTable: "import_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_candidate_asset_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_candidate_asset_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_candidate_asset_links_import_assets_import_asset_id",
                        column: x => x.import_asset_id,
                        principalTable: "import_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_candidate_asset_links_resource_candidates_resource_c~",
                        column: x => x.resource_candidate_id,
                        principalTable: "resource_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resource_import_runs_package",
                table: "resource_import_runs",
                column: "import_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_assets_checksum",
                table: "import_assets",
                column: "checksum");

            migrationBuilder.CreateIndex(
                name: "ix_import_assets_package",
                table: "import_assets",
                column: "import_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_assets_role",
                table: "import_assets",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "ix_import_candidate_asset_links_asset",
                table: "import_candidate_asset_links",
                column: "import_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_candidate_asset_links_candidate",
                table: "import_candidate_asset_links",
                column: "resource_candidate_id");

            migrationBuilder.CreateIndex(
                name: "ux_import_candidate_asset_links_pair",
                table: "import_candidate_asset_links",
                columns: new[] { "resource_candidate_id", "import_asset_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_packages_source",
                table: "import_packages",
                column: "cefr_resource_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_packages_status",
                table: "import_packages",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_import_profiles_package",
                table: "import_profiles",
                column: "import_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_profiles_status",
                table: "import_profiles",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_candidate_asset_links");

            migrationBuilder.DropTable(
                name: "import_profiles");

            migrationBuilder.DropTable(
                name: "import_assets");

            migrationBuilder.DropTable(
                name: "import_packages");

            migrationBuilder.DropIndex(
                name: "ix_resource_import_runs_package",
                table: "resource_import_runs");

            migrationBuilder.DropColumn(
                name: "import_package_id",
                table: "resource_import_runs");

            migrationBuilder.DropColumn(
                name: "metadata_provenance_json",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "stt_model_name",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "stt_provider_name",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "transcript_confidence",
                table: "resource_candidates");

            migrationBuilder.DropColumn(
                name: "transcript_origin",
                table: "resource_candidates");
        }
    }
}
