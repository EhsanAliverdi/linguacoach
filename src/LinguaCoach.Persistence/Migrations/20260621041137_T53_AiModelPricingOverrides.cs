using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T53_AiModelPricingOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_model_pricing_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    input_price_per_1k_tokens = table.Column<decimal>(type: "numeric(12,8)", nullable: false),
                    output_price_per_1k_tokens = table.Column<decimal>(type: "numeric(12,8)", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_model_pricing_overrides", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_model_pricing_overrides_active_from",
                table: "ai_model_pricing_overrides",
                columns: new[] { "is_active", "effective_from_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_model_pricing_overrides_provider_model",
                table: "ai_model_pricing_overrides",
                columns: new[] { "provider_name", "model_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_model_pricing_overrides");
        }
    }
}
