using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateTariffContractStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill must run first, while EffectiveDate still exists and ContractStartDate is
            // still nullable: SQL Server rejects a nullable->non-nullable ALTER COLUMN while NULL
            // rows remain, and this statement is the AC1/AC9 backfill rule itself.
            migrationBuilder.Sql("UPDATE Tariffs SET ContractStartDate = EffectiveDate WHERE ContractStartDate IS NULL");

            migrationBuilder.DropIndex(
                name: "IX_Tariffs_FlatId_EffectiveDate",
                table: "Tariffs");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "Tariffs");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ContractStartDate",
                table: "Tariffs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_FlatId_ContractStartDate",
                table: "Tariffs",
                columns: new[] { "FlatId", "ContractStartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tariffs_FlatId_ContractStartDate",
                table: "Tariffs");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ContractStartDate",
                table: "Tariffs",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveDate",
                table: "Tariffs",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Not unique: Up() overwrites every row with the same default EffectiveDate value,
            // so a unique index here would fail for any flat with more than one tariff. Down()
            // is a one-way-consolidation rollback (no historical value recovery), not a
            // data-preserving revert, so re-establishing uniqueness on lossy data is not attempted.
            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_FlatId_EffectiveDate",
                table: "Tariffs",
                columns: new[] { "FlatId", "EffectiveDate" });
        }
    }
}
