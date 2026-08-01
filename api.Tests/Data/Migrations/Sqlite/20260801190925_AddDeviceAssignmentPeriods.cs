using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Tests.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddDeviceAssignmentPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceAssignmentPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PowerPointId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceAssignmentPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceAssignmentPeriods_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAssignmentPeriods_DeviceId_From",
                table: "DeviceAssignmentPeriods",
                columns: new[] { "DeviceId", "From" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceAssignmentPeriods");
        }
    }
}
