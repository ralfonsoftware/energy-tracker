using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlatIdAndUniqueIndexToPowerPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FlatId",
                table: "PowerPoints",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(@"
                UPDATE pp
                SET pp.FlatId = r.FlatId
                FROM PowerPoints pp
                INNER JOIN Rooms r ON pp.RoomId = r.RoomId;");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM PowerPoints
                    WHERE PlugId IS NOT NULL
                    GROUP BY FlatId, PlugId
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Migration AddFlatIdAndUniqueIndexToPowerPoints aborted: one or more Power Points share the same PlugId within the same Flat. Resolve the duplicate PlugId(s) before re-running this migration.', 1;");

            migrationBuilder.CreateIndex(
                name: "IX_PowerPoints_FlatId_PlugId_NotNull",
                table: "PowerPoints",
                columns: new[] { "FlatId", "PlugId" },
                unique: true,
                filter: "[PlugId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PowerPoints_FlatId_PlugId_NotNull",
                table: "PowerPoints");

            migrationBuilder.DropColumn(
                name: "FlatId",
                table: "PowerPoints");
        }
    }
}
