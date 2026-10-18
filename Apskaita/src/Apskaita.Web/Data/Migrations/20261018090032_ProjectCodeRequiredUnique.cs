using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apskaita.Data.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ProjectCodeRequiredUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill before tightening: missing codes get generated unique ones, then any
            // remaining duplicates are disambiguated so the unique index can be created.
            migrationBuilder.Sql(
                """
                UPDATE projects p
                JOIN (
                    SELECT id, ROW_NUMBER() OVER (ORDER BY created_at_utc, id) AS rn
                    FROM projects
                    WHERE code IS NULL OR code = ''
                ) x ON x.id = p.id
                SET p.code = CONCAT('P', LPAD(x.rn, 4, '0'))
                """);

            migrationBuilder.Sql(
                """
                UPDATE projects p
                JOIN (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY code ORDER BY created_at_utc, id) AS dup
                    FROM projects
                ) x ON x.id = p.id
                SET p.code = CONCAT(p.code, '-', x.dup)
                WHERE x.dup > 1
                """);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "projects",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_projects_code",
                table: "projects",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_projects_code",
                table: "projects");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "projects",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
