using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddProgrammeProfileInvitationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProfileInvitationSentAtUtc",
                table: "ProgrammeAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileInvitationSentBy",
                table: "ProgrammeAttendances",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileInvitationSentTo",
                table: "ProgrammeAttendances",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileInvitationSentAtUtc",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "ProfileInvitationSentBy",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "ProfileInvitationSentTo",
                table: "ProgrammeAttendances");
        }
    }
}
