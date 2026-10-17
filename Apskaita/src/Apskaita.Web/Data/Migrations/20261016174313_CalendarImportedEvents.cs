using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CalendarImportedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "calendar_event_calendar_provider",
                table: "time_trackings",
                type: "enum('Outlook365', 'Ics')",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "enum('Outlook365')",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "provider",
                table: "calendar_connectors",
                type: "enum('Outlook365', 'Ics')",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "enum('Outlook365')")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "calendar_imported_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    connector_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    external_event_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    end_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    subject = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    location = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    organizer_name = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    imported_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_online = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_imported_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_imported_events_calendar_connectors_connector_id",
                        column: x => x.connector_id,
                        principalTable: "calendar_connectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_imported_events_connector_id_start_utc",
                table: "calendar_imported_events",
                columns: new[] { "connector_id", "start_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_imported_events");

            migrationBuilder.AlterColumn<string>(
                name: "calendar_event_calendar_provider",
                table: "time_trackings",
                type: "enum('Outlook365')",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "enum('Outlook365', 'Ics')",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "provider",
                table: "calendar_connectors",
                type: "enum('Outlook365')",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "enum('Outlook365', 'Ics')")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
