using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeProfileAndSignOffIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthenticationType",
                table: "SignOffs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedSignerUsername",
                table: "SignOffs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SignedBySid",
                table: "SignOffs",
                type: "nvarchar(184)",
                maxLength: 184,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeTitle",
                table: "InductionCases",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "InductionCases",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "InductionCases",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "InductionCases",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "InductionCases",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE InductionCases SET EmployeeTitle = 'Not recorded', FirstName = LEFT(EmployeeName, 100), LastName = 'Not recorded', Gender = 'Not recorded', Position = 'Not recorded'");

            migrationBuilder.Sql(
                "UPDATE s SET ExpectedSignerUsername = CASE s.Role WHEN 0 THEN i.EmployeeUsername WHEN 1 THEN i.LineManagerUsername WHEN 2 THEN i.HrBusinessPartnerUsername ELSE s.SignedBy END FROM SignOffs s INNER JOIN InductionCases i ON i.Id = s.InductionCaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthenticationType",
                table: "SignOffs");

            migrationBuilder.DropColumn(
                name: "ExpectedSignerUsername",
                table: "SignOffs");

            migrationBuilder.DropColumn(
                name: "SignedBySid",
                table: "SignOffs");

            migrationBuilder.DropColumn(
                name: "EmployeeTitle",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "InductionCases");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "InductionCases");
        }
    }
}
