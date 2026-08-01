using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Tests.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialSqliteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LocaleOverride = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ActiveFlatId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Flats",
                columns: table => new
                {
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AnnualKwhBaseline = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SpikeThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 2.0m),
                    PlannedAnnualSpend = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flats", x => x.FlatId);
                    table.ForeignKey(
                        name: "FK_Flats_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportJobs",
                columns: table => new
                {
                    ImportJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlugId = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ErrorCategory = table.Column<int>(type: "INTEGER", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false),
                    GapNotifications = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.ImportJobId);
                    table.ForeignKey(
                        name: "FK_ImportJobs_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "FlatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InsightRuns",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsightRuns", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_InsightRuns_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "FlatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                columns: table => new
                {
                    ReadingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KwhValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReadingDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsCorrected = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    OriginalKwhValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.ReadingId);
                    table.ForeignKey(
                        name: "FK_MeterReadings_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "FlatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_Rooms_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "FlatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmartPlugDailyData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlugId = table.Column<string>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    KwhValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsInterpolated = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartPlugDailyData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartPlugDailyData_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "FlatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmartPlugIntervalData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlugId = table.Column<string>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    WhValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartPlugIntervalData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartPlugIntervalData_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "FlatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tariffs",
                columns: table => new
                {
                    TariffId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PricePerKwh = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    MonthlyBaseFee = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContractStartDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ContractDurationMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tariffs", x => x.TariffId);
                    table.ForeignKey(
                        name: "FK_Tariffs_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "FlatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PowerPoints",
                columns: table => new
                {
                    PowerPointId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PlugId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerPoints", x => x.PowerPointId);
                    table.ForeignKey(
                        name: "FK_PowerPoints_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PowerPointId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PurchaseDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConsumptionApproach = table.Column<int>(type: "INTEGER", nullable: false),
                    EuLabelClass = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EuAnnualKwh = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    SelfMeasuredKwh = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    SelfMeasuredPeriod = table.Column<int>(type: "INTEGER", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_Devices_PowerPoints_PowerPointId",
                        column: x => x.PowerPointId,
                        principalTable: "PowerPoints",
                        principalColumn: "PowerPointId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Insights",
                columns: table => new
                {
                    InsightId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insights", x => x.InsightId);
                    table.ForeignKey(
                        name: "FK_Insights_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId");
                    table.ForeignKey(
                        name: "FK_Insights_Flats_FlatId",
                        column: x => x.FlatId,
                        principalTable: "Flats",
                        principalColumn: "FlatId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Insights_InsightRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "InsightRuns",
                        principalColumn: "RunId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PowerPointId",
                table: "Devices",
                column: "PowerPointId");

            migrationBuilder.CreateIndex(
                name: "IX_Flats_UserId",
                table: "Flats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_FlatId",
                table: "ImportJobs",
                column: "FlatId");

            migrationBuilder.CreateIndex(
                name: "IX_InsightRuns_FlatId_ActiveOnly",
                table: "InsightRuns",
                column: "FlatId",
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_Insights_DeviceId",
                table: "Insights",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Insights_FlatId_Type_CreatedAt",
                table: "Insights",
                columns: new[] { "FlatId", "Type", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Insights_RunId",
                table: "Insights",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_FlatId_ReadingDate",
                table: "MeterReadings",
                columns: new[] { "FlatId", "ReadingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PowerPoints_FlatId_PlugId_NotNull",
                table: "PowerPoints",
                columns: new[] { "FlatId", "PlugId" },
                unique: true,
                filter: "[PlugId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PowerPoints_RoomId",
                table: "PowerPoints",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_FlatId",
                table: "Rooms",
                column: "FlatId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartPlugDailyData_FlatId_PlugId_Date",
                table: "SmartPlugDailyData",
                columns: new[] { "FlatId", "PlugId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmartPlugIntervalData_FlatId_PlugId_Timestamp",
                table: "SmartPlugIntervalData",
                columns: new[] { "FlatId", "PlugId", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_FlatId_ContractStartDate",
                table: "Tariffs",
                columns: new[] { "FlatId", "ContractStartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportJobs");

            migrationBuilder.DropTable(
                name: "Insights");

            migrationBuilder.DropTable(
                name: "MeterReadings");

            migrationBuilder.DropTable(
                name: "SmartPlugDailyData");

            migrationBuilder.DropTable(
                name: "SmartPlugIntervalData");

            migrationBuilder.DropTable(
                name: "Tariffs");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "InsightRuns");

            migrationBuilder.DropTable(
                name: "PowerPoints");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Flats");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
