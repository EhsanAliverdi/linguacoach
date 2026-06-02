using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T12_AdminAndProviderConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_provider_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_provider_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_provider_configs_feature_key",
                table: "ai_provider_configs",
                column: "feature_key",
                unique: true);

            // Seed default provider configs — one per feature key.
            var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var features = new[]
            {
                ("writing.exercise", "openai", "gpt-4o"),
                ("cefr.assessment", "openai", "gpt-4o"),
                ("speaking.turn", "openai", "gpt-4o"),
            };

            foreach (var (featureKey, provider, model) in features)
            {
                migrationBuilder.InsertData(
                    table: "ai_provider_configs",
                    columns: new[] { "id", "feature_key", "provider_name", "model_name", "updated_at", "created_at" },
                    values: new object[] { Guid.NewGuid(), featureKey, provider, model, seedDate, seedDate });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_provider_configs");
        }
    }
}
