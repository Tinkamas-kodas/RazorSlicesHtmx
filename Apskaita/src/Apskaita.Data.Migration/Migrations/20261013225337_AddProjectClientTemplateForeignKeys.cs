using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectClientTemplateForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_projects_invoice_template_id",
                table: "projects",
                column: "invoice_template_id");

            migrationBuilder.AddForeignKey(
                name: "fk_projects_clients_client_id",
                table: "projects",
                column: "client_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_projects_invoice_templates_invoice_template_id",
                table: "projects",
                column: "invoice_template_id",
                principalTable: "invoice_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_clients_client_id",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "fk_projects_invoice_templates_invoice_template_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_invoice_template_id",
                table: "projects");
        }
    }
}
