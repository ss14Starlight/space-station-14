using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Automod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancel_speech",
                table: "AutoModRules");

            migrationBuilder.DropColumn(
                name: "count",
                table: "AutoModRules");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "AutoModRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cancel_speech",
                table: "AutoModRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "count",
                table: "AutoModRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "severity",
                table: "AutoModRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
