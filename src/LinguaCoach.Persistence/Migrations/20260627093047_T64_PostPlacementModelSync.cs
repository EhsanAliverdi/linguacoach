using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T64_PostPlacementModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // is_provisional and is_adaptive were added by T62 with DefaultValue=false.
            // In some production states T62 is recorded in __EFMigrationsHistory but the
            // columns were never committed (crash/restore mid-migration). Handle both cases:
            // missing column → add without default; column with default → just drop default.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'placement_assessments' AND column_name = 'is_provisional'
    ) THEN
        ALTER TABLE placement_assessments ADD COLUMN is_provisional boolean NOT NULL DEFAULT false;
    END IF;
    ALTER TABLE placement_assessments ALTER COLUMN is_provisional DROP DEFAULT;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'placement_assessments' AND column_name = 'is_adaptive'
    ) THEN
        ALTER TABLE placement_assessments ADD COLUMN is_adaptive boolean NOT NULL DEFAULT false;
    END IF;
    ALTER TABLE placement_assessments ALTER COLUMN is_adaptive DROP DEFAULT;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "is_provisional",
                table: "placement_assessments",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_adaptive",
                table: "placement_assessments",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }
    }
}
