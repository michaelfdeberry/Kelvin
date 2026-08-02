using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kelvin.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControlStateChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousState = table.Column<int>(type: "INTEGER", nullable: true),
                    PreviousStateDurationSeconds = table.Column<double>(type: "REAL", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    EnvironmentTemperatureC = table.Column<float>(type: "REAL", nullable: true),
                    HumidityPercentage = table.Column<float>(type: "REAL", nullable: true),
                    CO2LevelPpm = table.Column<float>(type: "REAL", nullable: true),
                    TargetTemperatureC = table.Column<float>(type: "REAL", nullable: true),
                    HysteresisC = table.Column<float>(type: "REAL", nullable: true),
                    ForecastTemperatureC = table.Column<float>(type: "REAL", nullable: true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SetPointId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlStateChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gateways",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    HeatingPin = table.Column<int>(type: "INTEGER", nullable: true),
                    FanPin = table.Column<int>(type: "INTEGER", nullable: true),
                    CoolingPin = table.Column<int>(type: "INTEGER", nullable: true),
                    ControlPin = table.Column<int>(type: "INTEGER", nullable: true),
                    MinimumOffDurationMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    MinimumOnDurationMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gateways", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemperatureUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeFormat = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<long>(type: "INTEGER", nullable: true),
                    LocationName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sensors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    HasBattery = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasCO2Sensor = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasHumiditySensor = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sensors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Thermostats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    FanEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    HysteresisC = table.Column<float>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Thermostats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SensorPackets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: false),
                    TemperatureC = table.Column<float>(type: "REAL", nullable: false),
                    HumidityPercentage = table.Column<float>(type: "REAL", nullable: false),
                    CO2LevelPpm = table.Column<ushort>(type: "INTEGER", nullable: false),
                    BatteryLevelPercentage = table.Column<float>(type: "REAL", nullable: false),
                    SensorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorPackets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorPackets_Sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    TargetTemperatureC = table.Column<float>(type: "REAL", nullable: false),
                    ActivationTemperatureC = table.Column<float>(type: "REAL", nullable: true),
                    ThermostatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_Thermostats_ThermostatId",
                        column: x => x.ThermostatId,
                        principalTable: "Thermostats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetTemperatureC = table.Column<float>(type: "REAL", nullable: false),
                    ActivationTemperatureC = table.Column<float>(type: "REAL", nullable: true),
                    ThermostatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetPoints_Thermostats_ThermostatId",
                        column: x => x.ThermostatId,
                        principalTable: "Thermostats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlStateChanges_Kind_CreatedAt",
                table: "ControlStateChanges",
                columns: new[] { "Kind", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ThermostatId",
                table: "Schedules",
                column: "ThermostatId");

            migrationBuilder.CreateIndex(
                name: "IX_SensorPackets_SensorId",
                table: "SensorPackets",
                column: "SensorId");

            migrationBuilder.CreateIndex(
                name: "IX_SetPoints_ThermostatId",
                table: "SetPoints",
                column: "ThermostatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlStateChanges");

            migrationBuilder.DropTable(
                name: "Gateways");

            migrationBuilder.DropTable(
                name: "Preferences");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropTable(
                name: "SensorPackets");

            migrationBuilder.DropTable(
                name: "SetPoints");

            migrationBuilder.DropTable(
                name: "Sensors");

            migrationBuilder.DropTable(
                name: "Thermostats");
        }
    }
}
