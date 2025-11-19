using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
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
                    auto_mod_categories_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "text", nullable: false),
                    is_collapsed = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<Guid>(type: "uuid", nullable: false),
                    last_modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_mod_categories", x => x.auto_mod_categories_id);
                });

            migrationBuilder.CreateTable(
                name: "AutoModRules",
                columns: table => new
                {
                    auto_mod_rules_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category = table.Column<string>(type: "text", nullable: true),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    regex = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    watch_oo_c = table.Column<bool>(type: "boolean", nullable: false),
                    secret = table.Column<bool>(type: "boolean", nullable: false),
                    offences = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_modified_by = table.Column<Guid>(type: "uuid", nullable: false),
                    last_modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
