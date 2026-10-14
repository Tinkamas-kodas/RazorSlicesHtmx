using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEventReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "calendar_event_account_key",
                table: "time_trackings",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "calendar_event_calendar_provider",
                table: "time_trackings",
                type: "enum('Outlook365')",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "calendar_event_external_event_id",
                table: "time_trackings",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_time_trackings_calendar_event_calendar_provider_calendar_eve",
                table: "time_trackings",
                columns: new[] { "calendar_event_calendar_provider", "calendar_event_account_key", "calendar_event_external_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_time_trackings_calendar_event_calendar_provider_calendar_eve",
                table: "time_trackings");

            migrationBuilder.DropColumn(
                name: "calendar_event_account_key",
                table: "time_trackings");

            migrationBuilder.DropColumn(
                name: "calendar_event_calendar_provider",
                table: "time_trackings");

            migrationBuilder.DropColumn(
                name: "calendar_event_external_event_id",
                table: "time_trackings");
        }
    }
}
