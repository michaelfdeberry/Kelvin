using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kelvin.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupportingOffsets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "CO2LevelPpmOffset",
                table: "Sensors",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "HumidityPercentageOffset",
                table: "Sensors",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "TemperatureCOffset",
                table: "Sensors",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CO2LevelPpmOffset",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "HumidityPercentageOffset",
                table: "Sensors");

            migrationBuilder.DropColumn(
                name: "TemperatureCOffset",
                table: "Sensors");
        }
    }
}
