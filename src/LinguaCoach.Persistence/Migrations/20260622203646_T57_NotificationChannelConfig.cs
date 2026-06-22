using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T57_NotificationChannelConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationChannelConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FromAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FromDisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Host = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: true),
                    UseSsl = table.Column<bool>(type: "boolean", nullable: true),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SenderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SecretEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationChannelConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannelConfigs_Channel",
                table: "NotificationChannelConfigs",
                column: "Channel",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationChannelConfigs");
        }
    }
}
