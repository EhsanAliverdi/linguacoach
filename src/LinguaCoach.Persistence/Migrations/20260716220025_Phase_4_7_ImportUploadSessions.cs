using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_4_7_ImportUploadSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_upload_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_resource_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    declared_total_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    part_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    total_parts_expected = table.Column<int>(type: "integer", nullable: false),
                    declared_checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    final_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    import_package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aborted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_upload_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_upload_sessions_cefr_resource_sources_cefr_resource_~",
                        column: x => x.cefr_resource_source_id,
                        principalTable: "cefr_resource_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_upload_session_parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_upload_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_number = table.Column<int>(type: "integer", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_upload_session_parts", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_upload_session_parts_import_upload_sessions_import_u~",
                        column: x => x.import_upload_session_id,
                        principalTable: "import_upload_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_import_upload_session_parts_session_part",
                table: "import_upload_session_parts",
                columns: new[] { "import_upload_session_id", "part_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_upload_sessions_created_by",
                table: "import_upload_sessions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_upload_sessions_source",
                table: "import_upload_sessions",
                column: "cefr_resource_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_upload_sessions_status",
                table: "import_upload_sessions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_upload_session_parts");

            migrationBuilder.DropTable(
                name: "import_upload_sessions");
        }
    }
}
