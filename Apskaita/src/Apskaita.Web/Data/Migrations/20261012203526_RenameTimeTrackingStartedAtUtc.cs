using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RenameTimeTrackingStartedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "started_at_local",
                table: "time_trackings",
                newName: "started_at_utc");

            migrationBuilder.RenameIndex(
                name: "ix_time_trackings_project_id_started_at_local",
                table: "time_trackings",
                newName: "ix_time_trackings_project_id_started_at_utc");

            // Data conversion: stored values were Europe/Vilnius wall time typed in the browser;
            // shift them to UTC. Fixed 3-hour offset = Vilnius summer time at the migration date
            // (2026-10-12); winter-time entries would be 1h off — acceptable for dev data.
            migrationBuilder.Sql(
                "UPDATE time_trackings SET started_at_utc = DATE_SUB(started_at_utc, INTERVAL 3 HOUR);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "started_at_utc",
                table: "time_trackings",
                newName: "started_at_local");

            migrationBuilder.RenameIndex(
                name: "ix_time_trackings_project_id_started_at_utc",
                table: "time_trackings",
                newName: "ix_time_trackings_project_id_started_at_local");

            // Inverse of the Up conversion: UTC back to Vilnius wall time (+3h).
            migrationBuilder.Sql(
                "UPDATE time_trackings SET started_at_local = DATE_ADD(started_at_local, INTERVAL 3 HOUR);");
        }
    }
}
