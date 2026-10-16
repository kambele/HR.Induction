using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateInductionProgrammesAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InductionProgrammeId",
                table: "InductionCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImprovementRecommendations",
                table: "EmployeeFeedback",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MostValuableAspect",
                table: "EmployeeFeedback",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverallProgrammeRating",
                table: "EmployeeFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgrammeUnderstandingRating",
                table: "EmployeeFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeAllocationResponse",
                table: "EmployeeFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InductionProgrammes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProgrammeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InductionProgrammes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InductionProgrammeSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InductionProgrammeId = table.Column<int>(type: "int", nullable: false),
                    Division = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PresenterName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InductionProgrammeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InductionProgrammeSessions_InductionProgrammes_InductionProgrammeId",
                        column: x => x.InductionProgrammeId,
                        principalTable: "InductionProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PresentationRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeFeedbackId = table.Column<int>(type: "int", nullable: false),
                    InductionProgrammeSessionId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentationRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresentationRatings_EmployeeFeedback_EmployeeFeedbackId",
                        column: x => x.EmployeeFeedbackId,
                        principalTable: "EmployeeFeedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PresentationRatings_InductionProgrammeSessions_InductionProgrammeSessionId",
                        column: x => x.InductionProgrammeSessionId,
                        principalTable: "InductionProgrammeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InductionCases_InductionProgrammeId",
                table: "InductionCases",
                column: "InductionProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_InductionProgrammeSessions_InductionProgrammeId_SortOrder",
                table: "InductionProgrammeSessions",
                columns: new[] { "InductionProgrammeId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PresentationRatings_EmployeeFeedbackId_InductionProgrammeSessionId",
                table: "PresentationRatings",
                columns: new[] { "EmployeeFeedbackId", "InductionProgrammeSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PresentationRatings_InductionProgrammeSessionId",
                table: "PresentationRatings",
                column: "InductionProgrammeSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_InductionCases_InductionProgrammes_InductionProgrammeId",
                table: "InductionCases",
                column: "InductionProgrammeId",
                principalTable: "InductionProgrammes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InductionCases_InductionProgrammes_InductionProgrammeId",
                table: "InductionCases");

            migrationBuilder.DropTable(
                name: "PresentationRatings");

            migrationBuilder.DropTable(
                name: "InductionProgrammeSessions");

            migrationBuilder.DropTable(
                name: "InductionProgrammes");

            migrationBuilder.DropIndex(
                name: "IX_InductionCases_InductionProgrammeId",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "InductionProgrammeId",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "ImprovementRecommendations",
                table: "EmployeeFeedback");

            migrationBuilder.DropColumn(
                name: "MostValuableAspect",
                table: "EmployeeFeedback");

            migrationBuilder.DropColumn(
                name: "OverallProgrammeRating",
                table: "EmployeeFeedback");

            migrationBuilder.DropColumn(
                name: "ProgrammeUnderstandingRating",
                table: "EmployeeFeedback");

            migrationBuilder.DropColumn(
                name: "TimeAllocationResponse",
                table: "EmployeeFeedback");
        }
    }
}
