using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
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
                    sl_pending_preference_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_admin_name = table.Column<string>(type: "text", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    has_job_priorities = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sl_pending_preference", x => x.sl_pending_preference_id);
                });

            migrationBuilder.CreateTable(
                name: "sl_pending_job_priority",
                columns: table => new
                {
                    sl_pending_job_priority_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pending_preference_id = table.Column<int>(type: "integer", nullable: false),
                    job_name = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sl_pending_job_priority", x => x.sl_pending_job_priority_id);
                    table.ForeignKey(
                        name: "FK_sl_pending_job_priority_sl_pending_preference_pending_prefe~",
                        column: x => x.pending_preference_id,
                        principalTable: "sl_pending_preference",
                        principalColumn: "sl_pending_preference_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sl_pending_profile",
                columns: table => new
                {
                    sl_pending_profile_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pending_preference_id = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sl_pending_profile", x => x.sl_pending_profile_id);
                    table.ForeignKey(
                        name: "FK_sl_pending_profile_sl_pending_preference_pending_preference~",
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
