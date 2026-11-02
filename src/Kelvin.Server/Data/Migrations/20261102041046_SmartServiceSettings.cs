using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kelvin.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class SmartServiceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CO2RecirculationDurationMs",
                table: "Thermostats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CO2RecirculationEnabled",
                table: "Thermostats",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CO2RecirculationMode",
                table: "Thermostats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<ushort>(
                name: "CO2RecirculationSatisfiedPpm",
                table: "Thermostats",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<ushort>(
                name: "CO2RecirculationTriggerPpm",
                table: "Thermostats",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<int>(
                name: "HeatingAutoBalanceDurationMs",
                table: "Thermostats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HeatingAutoBalanceEnabled",
                table: "Thermostats",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "HeatingBalanceSatisfiedC",
                table: "Thermostats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "HeatingBalanceTriggerC",
                table: "Thermostats",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CO2RecirculationDurationMs",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "CO2RecirculationEnabled",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "CO2RecirculationMode",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "CO2RecirculationSatisfiedPpm",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "CO2RecirculationTriggerPpm",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "HeatingAutoBalanceDurationMs",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "HeatingAutoBalanceEnabled",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "HeatingBalanceSatisfiedC",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "HeatingBalanceTriggerC",
                table: "Thermostats");
        }
    }
}
