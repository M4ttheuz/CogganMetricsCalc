using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inzynierka.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    WeightKg = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    SportType = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    MovingTimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DistanceMeters = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    TotalElevationGain = table.Column<int>(type: "INTEGER", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalTss = table.Column<decimal>(type: "TEXT", precision: 6, scale: 1, nullable: false),
                    Ctl = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false),
                    Atl = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false),
                    Tsb = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyMetrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFtpHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    FtpValue = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFtpHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFtpHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityCogganMetrics",
                columns: table => new
                {
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    NormalizedPower = table.Column<int>(type: "INTEGER", nullable: true),
                    IntensityFactor = table.Column<decimal>(type: "TEXT", precision: 4, scale: 3, nullable: true),
                    TrainingStressScore = table.Column<decimal>(type: "TEXT", precision: 6, scale: 1, nullable: true),
                    WorkKj = table.Column<int>(type: "INTEGER", nullable: true),
                    AvgPower = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxPower = table.Column<int>(type: "INTEGER", nullable: true),
                    AvgHeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxHeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    Decoupling = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityCogganMetrics", x => x.ActivityId);
                    table.ForeignKey(
                        name: "FK_ActivityCogganMetrics_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityPowerPeaks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxWatts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityPowerPeaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityPowerPeaks_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Power = table.Column<int>(type: "INTEGER", nullable: true),
                    HeartRate = table.Column<int>(type: "INTEGER", nullable: true),
                    Cadence = table.Column<int>(type: "INTEGER", nullable: true),
                    SpeedKmh = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    AltitudeMeters = table.Column<decimal>(type: "TEXT", precision: 6, scale: 1, nullable: true),
                    DistanceMeters = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true)
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
                name: "IX_Activities_FileHash",
                table: "Activities",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_StartTime",
                table: "Activities",
                columns: new[] { "UserId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityPowerPeaks_ActivityId_DurationSeconds",
                table: "ActivityPowerPeaks",
                columns: new[] { "ActivityId", "DurationSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_ActivityId_Timestamp",
                table: "ActivityRecords",
                columns: new[] { "ActivityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyMetrics_UserId_Date",
                table: "DailyMetrics",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFtpHistories_UserId_EffectiveDate",
                table: "UserFtpHistories",
                columns: new[] { "UserId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityCogganMetrics");

            migrationBuilder.DropTable(
                name: "ActivityPowerPeaks");

            migrationBuilder.DropTable(
                name: "ActivityRecords");

            migrationBuilder.DropTable(
                name: "DailyMetrics");

            migrationBuilder.DropTable(
                name: "UserFtpHistories");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
