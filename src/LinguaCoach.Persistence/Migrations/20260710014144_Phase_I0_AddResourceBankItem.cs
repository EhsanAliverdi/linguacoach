using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_I0_AddResourceBankItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resource_bank_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    context_tags_json = table.Column<string>(type: "text", nullable: true),
                    focus_tags_json = table.Column<string>(type: "text", nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_fingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content_json = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_bank_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_bank_items_cefr_resource_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resource_bank_items_created_at",
                table: "resource_bank_items",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_resource_bank_items_fingerprint",
                table: "resource_bank_items",
                column: "content_fingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_resource_bank_items_level",
                table: "resource_bank_items",
                column: "cefr_level");

            migrationBuilder.CreateIndex(
                name: "ix_resource_bank_items_source",
                table: "resource_bank_items",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_bank_items_type_level",
                table: "resource_bank_items",
                columns: new[] { "type", "cefr_level" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_bank_items");
        }
    }
}
