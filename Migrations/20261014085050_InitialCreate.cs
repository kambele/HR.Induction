using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChecklistTemplateItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Section = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    ResponsibleRole = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DueOffsetDays = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplateItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InductionCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmployeeUsername = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Division = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LineManagerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LineManagerUsername = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    HrBusinessPartnerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    HrBusinessPartnerUsername = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InductionCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InductionCaseId = table.Column<int>(type: "int", nullable: false),
                    UnderstandsResponsibilities = table.Column<bool>(type: "bit", nullable: false),
                    AwareOfDevelopmentOpportunities = table.Column<bool>(type: "bit", nullable: false),
                    MissingFromInduction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AdditionalComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeFeedback_InductionCases_InductionCaseId",
                        column: x => x.InductionCaseId,
                        principalTable: "InductionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InductionTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InductionCaseId = table.Column<int>(type: "int", nullable: false),
                    ChecklistTemplateItemId = table.Column<int>(type: "int", nullable: true),
                    Section = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    ResponsibleRole = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AssignedToUsername = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InductionTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InductionTasks_InductionCases_InductionCaseId",
                        column: x => x.InductionCaseId,
                        principalTable: "InductionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignOffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InductionCaseId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    SignedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Acknowledgement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignOffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignOffs_InductionCases_InductionCaseId",
                        column: x => x.InductionCaseId,
                        principalTable: "InductionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InductionCaseId = table.Column<int>(type: "int", nullable: false),
                    CourseTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CourseProvider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CourseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ObjectivesClearlyDefined = table.Column<int>(type: "int", nullable: false),
                    ParticipationEncouraged = table.Column<int>(type: "int", nullable: false),
                    ContentOrganized = table.Column<int>(type: "int", nullable: false),
                    MaterialsHelpful = table.Column<int>(type: "int", nullable: false),
                    UsefulInWork = table.Column<int>(type: "int", nullable: false),
                    ObjectivesMet = table.Column<int>(type: "int", nullable: false),
                    TimeSufficient = table.Column<int>(type: "int", nullable: false),
                    FacilitiesAdequate = table.Column<int>(type: "int", nullable: false),
                    DisagreementImprovements = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LikedMost = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CouldImprove = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OverallImpression = table.Column<int>(type: "int", nullable: false),
                    AdditionalComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TrainerKnowledge = table.Column<int>(type: "int", nullable: false),
                    TrainerExplanation = table.Column<int>(type: "int", nullable: false),
                    TrainerAnswers = table.Column<int>(type: "int", nullable: false),
                    VenueComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MealsComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OfficialResultsAttached = table.Column<bool>(type: "bit", nullable: false),
                    CertificateAttached = table.Column<bool>(type: "bit", nullable: false),
                    AttendanceRegisterAttached = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingEvaluations_InductionCases_InductionCaseId",
                        column: x => x.InductionCaseId,
                        principalTable: "InductionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplateItems_Phase_SortOrder",
                table: "ChecklistTemplateItems",
                columns: new[] { "Phase", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFeedback_InductionCaseId",
                table: "EmployeeFeedback",
                column: "InductionCaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InductionTasks_InductionCaseId",
                table: "InductionTasks",
                column: "InductionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SignOffs_InductionCaseId_Role",
                table: "SignOffs",
                columns: new[] { "InductionCaseId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEvaluations_InductionCaseId",
                table: "TrainingEvaluations",
                column: "InductionCaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistTemplateItems");

            migrationBuilder.DropTable(
                name: "EmployeeFeedback");

            migrationBuilder.DropTable(
                name: "InductionTasks");

            migrationBuilder.DropTable(
                name: "SignOffs");

            migrationBuilder.DropTable(
                name: "TrainingEvaluations");

            migrationBuilder.DropTable(
                name: "InductionCases");
        }
    }
}
