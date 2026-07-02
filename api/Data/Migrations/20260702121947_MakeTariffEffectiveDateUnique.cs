using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeTariffEffectiveDateUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tariffs_FlatId_EffectiveDate",
                table: "Tariffs");

            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_FlatId_EffectiveDate",
                table: "Tariffs",
                columns: new[] { "FlatId", "EffectiveDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tariffs_FlatId_EffectiveDate",
                table: "Tariffs");

            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_FlatId_EffectiveDate",
                table: "Tariffs",
                columns: new[] { "FlatId", "EffectiveDate" });
        }
    }
}
