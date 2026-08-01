using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Tests.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddDeviceExistenceWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecommissionedDate",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InUseSince",
                table: "Devices",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecommissionedDate",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "InUseSince",
                table: "Devices");
        }
    }
}
