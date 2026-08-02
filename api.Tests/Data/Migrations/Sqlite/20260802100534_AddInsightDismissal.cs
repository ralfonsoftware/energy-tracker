using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Tests.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddInsightDismissal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DismissedAt",
                table: "Insights",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDismissed",
                table: "Insights",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DismissedAt",
                table: "Insights");

            migrationBuilder.DropColumn(
                name: "IsDismissed",
                table: "Insights");
        }
    }
}
