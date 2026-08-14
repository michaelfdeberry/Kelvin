using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kelvin.Server.Data.Migrations
{
  /// <inheritdoc />
  public partial class OptionalBatteryOnSensorPacket : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AlterColumn<float>(
        name: "BatteryLevelPercentage",
        table: "SensorPackets",
        type: "REAL",
        nullable: true,
        oldClrType: typeof(float),
        oldType: "REAL"
      );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AlterColumn<float>(
        name: "BatteryLevelPercentage",
        table: "SensorPackets",
        type: "REAL",
        nullable: false,
        defaultValue: 0f,
        oldClrType: typeof(float),
        oldType: "REAL",
        oldNullable: true
      );
    }
  }
}
