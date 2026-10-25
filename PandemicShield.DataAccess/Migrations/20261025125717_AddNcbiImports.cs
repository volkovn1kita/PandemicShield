using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandemicShield.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddNcbiImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportId",
                table: "Runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAccession",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    NextcladeDataset = table.Column<string>(type: "text", nullable: true),
                    AmrOrganism = table.Column<string>(type: "text", nullable: true),
                    Accessions = table.Column<string>(type: "text", nullable: false),
                    ArchiveName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Done = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Errors = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Imports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_ImportId",
                table: "Runs",
                column: "ImportId");

            migrationBuilder.AddForeignKey(
                name: "FK_Runs_Imports_ImportId",
                table: "Runs",
                column: "ImportId",
                principalTable: "Imports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Runs_Imports_ImportId",
                table: "Runs");

            migrationBuilder.DropTable(
                name: "Imports");

            migrationBuilder.DropIndex(
                name: "IX_Runs_ImportId",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "ImportId",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "SourceAccession",
                table: "Runs");
        }
    }
}
