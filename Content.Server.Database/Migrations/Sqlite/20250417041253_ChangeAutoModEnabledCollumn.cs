using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ChangeAutoModEnabledCollumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "message",
                table: "auto_mod_rules");

            migrationBuilder.DropColumn(
                name: "regex",
                table: "auto_mod_rules");

            migrationBuilder.RenameColumn(
                name: "is_enabled",
                table: "auto_mod_rules",
                newName: "enabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "enabled",
                table: "auto_mod_rules",
                newName: "is_enabled");

            migrationBuilder.AddColumn<string>(
                name: "message",
                table: "auto_mod_rules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regex",
                table: "auto_mod_rules",
                type: "TEXT",
                nullable: true);
        }
    }
}
