using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeSmartPlugIntervalDataTimestampUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmartPlugIntervalData_FlatId_PlugId_Timestamp",
                table: "SmartPlugIntervalData");

            migrationBuilder.CreateIndex(
                name: "IX_SmartPlugIntervalData_FlatId_PlugId_Timestamp",
                table: "SmartPlugIntervalData",
                columns: new[] { "FlatId", "PlugId", "Timestamp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmartPlugIntervalData_FlatId_PlugId_Timestamp",
                table: "SmartPlugIntervalData");

            migrationBuilder.CreateIndex(
                name: "IX_SmartPlugIntervalData_FlatId_PlugId_Timestamp",
                table: "SmartPlugIntervalData",
                columns: new[] { "FlatId", "PlugId", "Timestamp" });
        }
    }
}
