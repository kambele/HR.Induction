using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Namfisa.Induction.Migrations
{
    /// <inheritdoc />
    public partial class AddProgrammeChecklistPhases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludedByProgramme",
                table: "InductionTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "InductionProgrammeChecklistPhases",
                columns: table => new
                {
                    InductionProgrammeId = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InductionProgrammeChecklistPhases", x => new { x.InductionProgrammeId, x.Phase });
                    table.ForeignKey(
                        name: "FK_InductionProgrammeChecklistPhases_InductionProgrammes_InductionProgrammeId",
                        column: x => x.InductionProgrammeId,
                        principalTable: "InductionProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [InductionProgrammeChecklistPhases] ([InductionProgrammeId], [Phase])
                SELECT [programme].[Id], [phase].[Value]
                FROM [InductionProgrammes] AS [programme]
                CROSS JOIN (VALUES (0), (1), (2), (3)) AS [phase]([Value]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InductionProgrammeChecklistPhases");

            migrationBuilder.DropColumn(
                name: "ExcludedByProgramme",
                table: "InductionTasks");
        }
    }
}
