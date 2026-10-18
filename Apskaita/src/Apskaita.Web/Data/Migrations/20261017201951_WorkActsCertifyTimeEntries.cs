using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class WorkActsCertifyTimeEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_act_lines");

            migrationBuilder.DropIndex(
                name: "ix_work_acts_number",
                table: "work_acts");

            migrationBuilder.DropIndex(
                name: "ix_work_acts_project_id_status",
                table: "work_acts");

            migrationBuilder.DropColumn(
                name: "number",
                table: "work_acts");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "work_acts",
                type: "enum('Draft', 'Invoiced')",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "enum('Draft', 'Final', 'Invoiced')")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "period_end_utc",
                table: "work_acts",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "period_start_utc",
                table: "work_acts",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "work_acts",
                type: "int",
                nullable: false,
                defaultValue: 0);

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
                name: "ix_work_acts_client_id_status",
                table: "work_acts",
                columns: new[] { "client_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_work_acts_project_id_period_end_utc",
                table: "work_acts",
                columns: new[] { "project_id", "period_end_utc" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_time_trackings_work_acts_work_act_id",
                table: "time_trackings");

            migrationBuilder.DropIndex(
                name: "ix_work_acts_client_id_status",
                table: "work_acts");

            migrationBuilder.DropIndex(
                name: "ix_work_acts_project_id_period_end_utc",
                table: "work_acts");

            migrationBuilder.DropIndex(
                name: "ix_time_trackings_work_act_id",
                table: "time_trackings");

            migrationBuilder.DropColumn(
                name: "period_end_utc",
                table: "work_acts");

            migrationBuilder.DropColumn(
                name: "period_start_utc",
                table: "work_acts");

            migrationBuilder.DropColumn(
                name: "version",
                table: "work_acts");

            migrationBuilder.DropColumn(
                name: "version",
                table: "time_trackings");

            migrationBuilder.DropColumn(
                name: "work_act_id",
                table: "time_trackings");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "work_acts",
                type: "enum('Draft', 'Final', 'Invoiced')",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "enum('Draft', 'Invoiced')")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "number",
                table: "work_acts",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "work_act_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_in_minutes = table.Column<int>(type: "int", nullable: false),
                    external_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hourly_rate = table.Column<int>(type: "int", nullable: false),
                    notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source = table.Column<string>(type: "enum('Clockify', 'Manual')", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    work_act_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_act_lines", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_work_acts_number",
                table: "work_acts",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_acts_project_id_status",
                table: "work_acts",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_work_act_lines_work_act_id_work_date",
                table: "work_act_lines",
                columns: new[] { "work_act_id", "work_date" });
        }
    }
}
