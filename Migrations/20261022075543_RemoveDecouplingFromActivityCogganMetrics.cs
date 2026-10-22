using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inzynierka.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDecouplingFromActivityCogganMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Decoupling",
                table: "ActivityCogganMetrics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Decoupling",
                table: "ActivityCogganMetrics",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);
        }
    }
}
