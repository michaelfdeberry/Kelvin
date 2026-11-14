using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kelvin.Server.Data.Migrations
{
  /// <inheritdoc />
  public partial class AddThermostatFanMode : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<int>(name: "FanMode", table: "Thermostats", type: "INTEGER", nullable: false, defaultValue: 1);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(name: "FanMode", table: "Thermostats");
    }
  }
}
