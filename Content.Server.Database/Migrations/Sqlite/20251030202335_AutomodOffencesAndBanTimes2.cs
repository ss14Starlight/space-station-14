using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AutomodOffencesAndBanTimes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoModRules",
                columns: table => new
                {
                    auto_mod_rules_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    regex = table.Column<string>(type: "TEXT", nullable: false),
                    severity = table.Column<int>(type: "INTEGER", nullable: false),
                    count = table.Column<int>(type: "INTEGER", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    cancel_speech = table.Column<bool>(type: "INTEGER", nullable: false),
                    offences = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_mod_rules", x => x.auto_mod_rules_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoModRules_auto_mod_rules_id",
                table: "AutoModRules",
                column: "auto_mod_rules_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoModRules");
        }
    }
}
