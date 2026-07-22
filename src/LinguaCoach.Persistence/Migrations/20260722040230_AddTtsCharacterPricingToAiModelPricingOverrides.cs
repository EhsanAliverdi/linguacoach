using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsCharacterPricingToAiModelPricingOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "input_price_per_1k_characters",
                table: "ai_model_pricing_overrides",
                type: "numeric(12,8)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "input_price_per_1k_characters",
                table: "ai_model_pricing_overrides");
        }
    }
}
