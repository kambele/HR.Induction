using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddProgrammeCalendarInvitationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CalendarInvitationSentAtUtc",
                table: "ProgrammeAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalendarInvitationSentBy",
                table: "ProgrammeAttendances",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalendarInvitationSentTo",
                table: "ProgrammeAttendances",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalendarInvitationSentAtUtc",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "CalendarInvitationSentBy",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "CalendarInvitationSentTo",
                table: "ProgrammeAttendances");
        }
    }
}
