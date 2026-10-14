using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CalendarConnectorMultiAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reordered by hand: MySQL backs fk_calendar_connectors_users_user_id with the old
            // (user_id, provider) index, so the replacement (user_id, provider, account_key) index —
            // whose leftmost prefix is user_id — must exist BEFORE the old one is dropped.
            migrationBuilder.AddColumn<string>(
                name: "account_key",
                table: "calendar_connectors",
                type: "varchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "calendar_connectors",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_connectors_user_id_provider_account_key",
                table: "calendar_connectors",
                columns: new[] { "user_id", "provider", "account_key" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "ix_calendar_connectors_user_id_provider",
                table: "calendar_connectors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reordered by hand (mirror of Up): recreate the old (user_id, provider) index before
            // dropping the new one so the foreign key always has a backing index.
            migrationBuilder.CreateIndex(
                name: "ix_calendar_connectors_user_id_provider",
                table: "calendar_connectors",
                columns: new[] { "user_id", "provider" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "ix_calendar_connectors_user_id_provider_account_key",
                table: "calendar_connectors");

            migrationBuilder.DropColumn(
                name: "account_key",
                table: "calendar_connectors");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "calendar_connectors");
        }
    }
}
