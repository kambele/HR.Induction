using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackInvitationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackInvitationSentAtUtc",
                table: "ProgrammeAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackInvitationSentBy",
                table: "ProgrammeAttendances",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackInvitationSentTo",
                table: "ProgrammeAttendances",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedbackInvitationSentAtUtc",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "FeedbackInvitationSentBy",
                table: "ProgrammeAttendances");

            migrationBuilder.DropColumn(
                name: "FeedbackInvitationSentTo",
                table: "ProgrammeAttendances");
        }
    }
}
