using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inzynierka.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceActivityRecordsWithTelemetryBlob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityRecords");

            migrationBuilder.AddColumn<byte[]>(
                name: "TelemetryBlob",
                table: "Activities",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelemetryBlob",
                table: "Activities");

            migrationBuilder.CreateTable(
                name: "ActivityRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    AltitudeMeters = table.Column<decimal>(type: "TEXT", precision: 6, scale: 1, nullable: true),
                    Cadence = table.Column<int>(type: "INTEGER", nullable: true),
                    DistanceMeters = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    HeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    Power = table.Column<int>(type: "INTEGER", nullable: true),
                    SpeedKmh = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityRecords_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_ActivityId_Timestamp",
                table: "ActivityRecords",
                columns: new[] { "ActivityId", "Timestamp" });
        }
    }
}
