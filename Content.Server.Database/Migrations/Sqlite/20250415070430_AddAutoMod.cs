using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddAutoMod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auto_mod_rules",
                columns: table => new
                {
                    auto_mod_rules_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    regex = table.Column<string>(type: "TEXT", nullable: true),
                    severity = table.Column<int>(type: "INTEGER", nullable: false),
                    message = table.Column<string>(type: "TEXT", nullable: true),
                    count = table.Column<int>(type: "INTEGER", nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    cancel_speech = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_mod_rules", x => x.auto_mod_rules_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auto_mod_rules_auto_mod_rules_id",
                table: "auto_mod_rules",
                column: "auto_mod_rules_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auto_mod_rules");
        }
    }
}
