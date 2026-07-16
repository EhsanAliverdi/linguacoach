using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_4_4B_CostCeilingAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_cost_ceiling_amendments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_ceiling = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    new_ceiling = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "USD"),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    administrator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_cost_ceiling_amendments", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_cost_ceiling_amendments_import_packages_import_packa~",
                        column: x => x.import_package_id,
                        principalTable: "import_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_cost_ceiling_amendments_import_profiles_import_profi~",
                        column: x => x.import_profile_id,
                        principalTable: "import_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_cost_ceiling_amendments_package",
                table: "import_cost_ceiling_amendments",
                column: "import_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_cost_ceiling_amendments_profile",
                table: "import_cost_ceiling_amendments",
                column: "import_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_cost_ceiling_amendments");
        }
    }
}
