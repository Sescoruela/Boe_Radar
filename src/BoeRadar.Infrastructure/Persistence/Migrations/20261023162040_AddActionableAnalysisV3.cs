using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoeRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActionableAnalysisV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "actionable",
                table: "document_analyses",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actionable",
                table: "document_analyses");
        }
    }
}
