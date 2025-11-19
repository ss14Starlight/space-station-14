using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AutoMod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoModCategories",
                columns: table => new
                {
                    auto_mod_categories_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    color = table.Column<string>(type: "TEXT", nullable: false),
                    is_collapsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_modified_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    last_modified_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_mod_categories", x => x.auto_mod_categories_id);
                });

            migrationBuilder.CreateTable(
                name: "AutoModRules",
                columns: table => new
                {
                    auto_mod_rules_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    category = table.Column<string>(type: "TEXT", nullable: true),
                    severity = table.Column<int>(type: "INTEGER", nullable: false),
                    regex = table.Column<string>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    watch_oo_c = table.Column<bool>(type: "INTEGER", nullable: false),
                    secret = table.Column<bool>(type: "INTEGER", nullable: false),
                    offences = table.Column<string>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_modified_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    last_modified_at = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "AutoModCategories");

            migrationBuilder.DropTable(
                name: "AutoModRules");
        }
    }
}
