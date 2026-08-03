using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kelvin.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class MovingForecastLockouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivationTemperatureC",
                table: "SetPoints");

            migrationBuilder.DropColumn(
                name: "ActivationTemperatureC",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "Schedules");

            migrationBuilder.AddColumn<float>(
                name: "CoolingLockoutC",
                table: "Thermostats",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "HeatingLockoutC",
                table: "Thermostats",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoolingLockoutC",
                table: "Thermostats");

            migrationBuilder.DropColumn(
                name: "HeatingLockoutC",
                table: "Thermostats");

            migrationBuilder.AddColumn<float>(
                name: "ActivationTemperatureC",
                table: "SetPoints",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "ActivationTemperatureC",
                table: "Schedules",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "Schedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
