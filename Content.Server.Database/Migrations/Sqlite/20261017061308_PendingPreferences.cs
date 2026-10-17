using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PendingPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sl_pending_preference",
                columns: table => new
                {
                    sl_pending_preference_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    last_admin_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    last_admin_name = table.Column<string>(type: "TEXT", nullable: false),
                    revision = table.Column<int>(type: "INTEGER", nullable: false),
                    has_job_priorities = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sl_pending_preference", x => x.sl_pending_preference_id);
                });

            migrationBuilder.CreateTable(
                name: "sl_pending_job_priority",
                columns: table => new
                {
                    sl_pending_job_priority_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    pending_preference_id = table.Column<int>(type: "INTEGER", nullable: false),
                    job_name = table.Column<string>(type: "TEXT", nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sl_pending_job_priority", x => x.sl_pending_job_priority_id);
                    table.ForeignKey(
                        name: "FK_sl_pending_job_priority_sl_pending_preference_pending_preference_id",
                        column: x => x.pending_preference_id,
                        principalTable: "sl_pending_preference",
                        principalColumn: "sl_pending_preference_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sl_pending_profile",
                columns: table => new
                {
                    sl_pending_profile_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    pending_preference_id = table.Column<int>(type: "INTEGER", nullable: false),
                    slot = table.Column<int>(type: "INTEGER", nullable: false),
                    data = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sl_pending_profile", x => x.sl_pending_profile_id);
                    table.ForeignKey(
                        name: "FK_sl_pending_profile_sl_pending_preference_pending_preference_id",
                        column: x => x.pending_preference_id,
                        principalTable: "sl_pending_preference",
                        principalColumn: "sl_pending_preference_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sl_pending_job_priority_pending_preference_id",
                table: "sl_pending_job_priority",
                column: "pending_preference_id");

            migrationBuilder.CreateIndex(
                name: "IX_sl_pending_preference_user_id",
                table: "sl_pending_preference",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sl_pending_profile_pending_preference_id_slot",
                table: "sl_pending_profile",
                columns: new[] { "pending_preference_id", "slot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sl_pending_job_priority");

            migrationBuilder.DropTable(
                name: "sl_pending_profile");

            migrationBuilder.DropTable(
                name: "sl_pending_preference");
        }
    }
}
