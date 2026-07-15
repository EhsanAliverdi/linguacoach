using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_4_4_EditablePlansAndCostAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_stamp",
                table: "import_profiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "accrued_cost",
                table: "import_packages",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "accrued_cost_currency",
                table: "import_packages",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.CreateTable(
                name: "import_stt_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_operation_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transcript_text = table.Column<string>(type: "text", nullable: true),
                    assumed_minutes = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    price_per_minute_snapshot = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    calculated_cost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_stt_operations", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_stt_operations_import_assets_import_asset_id",
                        column: x => x.import_asset_id,
                        principalTable: "import_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_stt_operations_import_packages_import_package_id",
                        column: x => x.import_package_id,
                        principalTable: "import_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_stt_operations_import_asset_id",
                table: "import_stt_operations",
                column: "import_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_stt_operations_package",
                table: "import_stt_operations",
                column: "import_package_id");

            migrationBuilder.CreateIndex(
                name: "ux_import_stt_operations_logical_key",
                table: "import_stt_operations",
                column: "logical_operation_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_stt_operations");

            migrationBuilder.DropColumn(
                name: "concurrency_stamp",
                table: "import_profiles");

            migrationBuilder.DropColumn(
                name: "accrued_cost",
                table: "import_packages");

            migrationBuilder.DropColumn(
                name: "accrued_cost_currency",
                table: "import_packages");
        }
    }
}
