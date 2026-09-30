using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kelvin.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHmiSensorLinkAndCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SensorId",
                table: "Hmis",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hmis_SensorId",
                table: "Hmis",
                column: "SensorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hmis_Sensors_SensorId",
                table: "Hmis",
                column: "SensorId",
                principalTable: "Sensors",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hmis_Sensors_SensorId",
                table: "Hmis");

            migrationBuilder.DropIndex(
                name: "IX_Hmis_SensorId",
                table: "Hmis");

            migrationBuilder.DropColumn(
                name: "SensorId",
                table: "Hmis");
        }
    }
}
