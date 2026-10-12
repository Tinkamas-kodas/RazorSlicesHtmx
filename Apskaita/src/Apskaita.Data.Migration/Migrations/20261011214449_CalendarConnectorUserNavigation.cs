using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CalendarConnectorUserNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "fk_calendar_connectors_users_user_id",
                table: "calendar_connectors",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_calendar_connectors_users_user_id",
                table: "calendar_connectors");
        }
    }
}
