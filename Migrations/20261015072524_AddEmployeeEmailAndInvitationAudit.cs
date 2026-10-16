using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeEmailAndInvitationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeEmail",
                table: "InductionCases",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationSentAtUtc",
                table: "InductionCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationSentBy",
                table: "InductionCases",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationSentTo",
                table: "InductionCases",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeEmail",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "InvitationSentAtUtc",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "InvitationSentBy",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "InvitationSentTo",
                table: "InductionCases");
        }
    }
}
