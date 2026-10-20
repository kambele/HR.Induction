using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddOneTimeEmployeeInvitationTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AttendanceAccessToken",
                table: "ProgrammeAttendances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttendanceAccessTokenIssuedAtUtc",
                table: "ProgrammeAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttendanceAccessTokenUsedAtUtc",
                table: "ProgrammeAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FeedbackAccessToken",
                table: "ProgrammeAttendances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackAccessTokenIssuedAtUtc",
                table: "ProgrammeAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackAccessTokenUsedAtUtc",
                table: "ProgrammeAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeAttendances_AttendanceAccessToken",
                table: "ProgrammeAttendances",
                column: "AttendanceAccessToken",
                unique: true,
                filter: "[AttendanceAccessToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeAttendances_FeedbackAccessToken",
                table: "ProgrammeAttendances",
                column: "FeedbackAccessToken",
                unique: true,
                filter: "[FeedbackAccessToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgrammeAttendances_AttendanceAccessToken",
                table: "ProgrammeAttendances");

            migrationBuilder.DropIndex(
                name: "IX_ProgrammeAttendances_FeedbackAccessToken",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "AttendanceAccessToken",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "AttendanceAccessTokenIssuedAtUtc",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "AttendanceAccessTokenUsedAtUtc",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "FeedbackAccessToken",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "FeedbackAccessTokenIssuedAtUtc",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "FeedbackAccessTokenUsedAtUtc",
                table: "ProgrammeAttendances");
        }
    }
}
