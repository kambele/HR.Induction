using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrationWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "ChecklistTemplateItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ChecklistTemplateItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE ChecklistTemplateItems SET UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = 'System migration'");

            migrationBuilder.CreateTable(
                name: "MasterDataItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterDataItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequireMandatoryTasksBeforeSignOff = table.Column<bool>(type: "bit", nullable: false),
                    RequireEmployeeFeedbackBeforeSignOff = table.Column<bool>(type: "bit", nullable: false),
                    EnforceSequentialSignOff = table.Column<bool>(type: "bit", nullable: false),
                    LockChecklistWhenSignOffStarts = table.Column<bool>(type: "bit", nullable: false),
                    EmailSubjectTemplate = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EmailBodyTemplate = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EmployeeAcknowledgement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LineManagerAcknowledgement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    HrBusinessPartnerAcknowledgement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterDataItems_Category_Name",
                table: "MasterDataItems",
                columns: new[] { "Category", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterDataItems_Category_SortOrder",
                table: "MasterDataItems",
                columns: new[] { "Category", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterDataItems");

            migrationBuilder.DropTable(
                name: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "ChecklistTemplateItems");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ChecklistTemplateItems");
        }
    }
}
