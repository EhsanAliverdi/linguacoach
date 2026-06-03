using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T16_FixModelTestsDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix rows where T15 migration set model_tests to '' (empty string) instead of '{}'
            migrationBuilder.Sql(
                "UPDATE ai_provider_credentials SET model_tests = '{}' WHERE model_tests = '' OR model_tests IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
