using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyTracker.Api.Data.Migrations
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PowerPointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    To = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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

            migrationBuilder.Sql(@"
                INSERT INTO DeviceAssignmentPeriods (Id, DeviceId, PowerPointId, FlatId, [From], [To])
                SELECT NEWID(), d.DeviceId, d.PowerPointId, pp.FlatId,
                       ISNULL(d.InUseSince, CAST('0001-01-01T00:00:00.0000000+00:00' AS datetimeoffset)),
                       NULL
                FROM Devices d
                INNER JOIN PowerPoints pp ON d.PowerPointId = pp.PowerPointId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceAssignmentPeriods");
        }
    }
}
