using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_4_4D_AiEnrichmentOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_ai_enrichment_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_operation_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    operation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processing_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    result_reference_json = table.Column<string>(type: "text", nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: true),
                    output_tokens = table.Column<int>(type: "integer", nullable: true),
                    input_price_per_1k_tokens_snapshot = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    output_price_per_1k_tokens_snapshot = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    calculated_cost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_ai_enrichment_operations", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_ai_enrichment_operations_import_packages_import_pack~",
                        column: x => x.import_package_id,
                        principalTable: "import_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_ai_enrichment_operations_resource_candidates_resourc~",
                        column: x => x.resource_candidate_id,
                        principalTable: "resource_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_ai_enrichment_operations_package",
                table: "import_ai_enrichment_operations",
                column: "import_package_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_ai_enrichment_operations_resource_candidate_id",
                table: "import_ai_enrichment_operations",
                column: "resource_candidate_id");

            migrationBuilder.CreateIndex(
                name: "ux_import_ai_enrichment_operations_logical_key",
                table: "import_ai_enrichment_operations",
                column: "logical_operation_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_ai_enrichment_operations");
        }
    }
}
