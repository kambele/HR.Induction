using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddPresenterAttendanceWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PresenterUsername",
                table: "InductionProgrammeSessions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProgrammeAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InductionProgrammeId = table.Column<int>(type: "int", nullable: false),
                    InductionCaseId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ConfirmedBySid = table.Column<string>(type: "nvarchar(184)", maxLength: 184, nullable: true),
                    AuthenticationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConfirmationSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvitationSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvitationSentBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    InvitationSentTo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgrammeAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgrammeAttendances_InductionCases_InductionCaseId",
                        column: x => x.InductionCaseId,
                        principalTable: "InductionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgrammeAttendances_InductionProgrammes_InductionProgrammeId",
                        column: x => x.InductionProgrammeId,
                        principalTable: "InductionProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeAttendances_InductionCaseId",
                table: "ProgrammeAttendances",
                column: "InductionCaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeAttendances_InductionProgrammeId_Status",
                table: "ProgrammeAttendances",
                columns: new[] { "InductionProgrammeId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "PresenterUsername",
                table: "InductionProgrammeSessions");
        }
    }
}
