using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class WorkActLinesComposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "work_acts",
                type: "enum('Draft', 'Final', 'Invoiced')",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "enum('Draft', 'Invoiced')")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "work_act_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    work_act_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    time_tracking_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    duration_minutes = table.Column<int>(type: "int", nullable: false),
                    hourly_rate = table.Column<int>(type: "int", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_act_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_act_lines_time_trackings_time_tracking_id",
                        column: x => x.time_tracking_id,
                        principalTable: "time_trackings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_act_lines_work_acts_work_act_id",
                        column: x => x.work_act_id,
                        principalTable: "work_acts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_work_act_lines_time_tracking_id",
                table: "work_act_lines",
                column: "time_tracking_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_act_lines_work_act_id_started_at_utc",
                table: "work_act_lines",
                columns: new[] { "work_act_id", "started_at_utc" });

            // Data-preserving composition change: every existing entry→act link becomes a line
            // (rate = the entry's own override, else the project rate) before the column goes away.
            migrationBuilder.Sql(
                """
                INSERT INTO work_act_lines
                    (id, work_act_id, time_tracking_id, duration_minutes, hourly_rate, started_at_utc, description,
                     created_at_utc, created_by_user_id, updated_at_utc, updated_by_user_id)
                SELECT UUID(), tt.work_act_id, tt.id, tt.duration_in_minutes,
                       COALESCE(tt.hourly_rate, p.hourly_rate, 0),
                       tt.started_at_utc, tt.description,
                       tt.created_at_utc, tt.created_by_user_id, tt.updated_at_utc, tt.updated_by_user_id
                FROM time_trackings tt
                JOIN work_acts wa ON wa.id = tt.work_act_id
                JOIN projects p ON p.id = tt.project_id
                WHERE tt.work_act_id IS NOT NULL
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_time_trackings_work_acts_work_act_id",
                table: "time_trackings");

            migrationBuilder.DropIndex(
                name: "ix_time_trackings_work_act_id",
                table: "time_trackings");

            migrationBuilder.DropColumn(
                name: "version",
                table: "time_trackings");

            migrationBuilder.DropColumn(
                name: "work_act_id",
                table: "time_trackings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "work_acts",
                type: "enum('Draft', 'Invoiced')",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "enum('Draft', 'Final', 'Invoiced')")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "time_trackings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "work_act_id",
                table: "time_trackings",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "ix_time_trackings_work_act_id",
                table: "time_trackings",
                column: "work_act_id");

            migrationBuilder.AddForeignKey(
                name: "fk_time_trackings_work_acts_work_act_id",
                table: "time_trackings",
                column: "work_act_id",
                principalTable: "work_acts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Restore the link column from lines that are unique per entry; multi-act memberships
            // cannot be expressed in the old shape and stay uncertified after the rollback.
            migrationBuilder.Sql(
                """
                UPDATE time_trackings tt
                JOIN (
                    SELECT time_tracking_id, MIN(work_act_id) AS work_act_id
                    FROM work_act_lines
                    GROUP BY time_tracking_id
                    HAVING COUNT(DISTINCT work_act_id) = 1
                ) x ON x.time_tracking_id = tt.id
                SET tt.work_act_id = x.work_act_id
                """);

            migrationBuilder.DropTable(
                name: "work_act_lines");
        }
    }
}
