using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAssignmentPeriodOneOpenIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DeviceAssignmentPeriods_DeviceId_OneOpenPeriod",
                table: "DeviceAssignmentPeriods",
                column: "DeviceId",
                unique: true,
                filter: "[To] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceAssignmentPeriods_DeviceId_OneOpenPeriod",
                table: "DeviceAssignmentPeriods");
        }
    }
}
