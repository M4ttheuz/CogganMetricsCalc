using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inzynierka.Migrations
{
    /// <inheritdoc />
    public partial class ScopedFileHashPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_FileHash",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_FileHash",
                table: "Activities",
                columns: new[] { "UserId", "FileHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_UserId_FileHash",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_FileHash",
                table: "Activities",
                column: "FileHash");
        }
    }
}
