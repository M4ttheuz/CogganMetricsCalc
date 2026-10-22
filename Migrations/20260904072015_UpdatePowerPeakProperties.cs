using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inzynierka.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePowerPeakProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaxWatts",
                table: "ActivityPowerPeaks",
                newName: "Watts");

            migrationBuilder.AddColumn<decimal>(
                name: "WattsPerKg",
                table: "ActivityPowerPeaks",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WattsPerKg",
                table: "ActivityPowerPeaks");

            migrationBuilder.RenameColumn(
                name: "Watts",
                table: "ActivityPowerPeaks",
                newName: "MaxWatts");
        }
    }
}
