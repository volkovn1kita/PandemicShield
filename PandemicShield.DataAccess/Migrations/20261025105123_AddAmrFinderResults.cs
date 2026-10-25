using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandemicShield.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAmrFinderResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AmrCompletedAt",
                table: "Runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmrDatabaseVersion",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmrMessage",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmrOrganism",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmrStatus",
                table: "Runs",
                type: "text",
                nullable: false,
                defaultValue: "NotRequested");

            migrationBuilder.CreateTable(
                name: "AmrHits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContigId = table.Column<string>(type: "text", nullable: false),
                    Start = table.Column<long>(type: "bigint", nullable: false),
                    Stop = table.Column<long>(type: "bigint", nullable: false),
                    Strand = table.Column<char>(type: "character(1)", nullable: false),
                    ElementSymbol = table.Column<string>(type: "text", nullable: false),
                    ElementName = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Subtype = table.Column<string>(type: "text", nullable: false),
                    Class = table.Column<string>(type: "text", nullable: false),
                    Subclass = table.Column<string>(type: "text", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    CoveragePercent = table.Column<double>(type: "double precision", nullable: true),
                    IdentityPercent = table.Column<double>(type: "double precision", nullable: true),
                    ClosestReference = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmrHits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AmrHits_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmrHits_ElementSymbol",
                table: "AmrHits",
                column: "ElementSymbol");

            migrationBuilder.CreateIndex(
                name: "IX_AmrHits_RunId",
                table: "AmrHits",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmrHits");

            migrationBuilder.DropColumn(
                name: "AmrCompletedAt",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "AmrDatabaseVersion",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "AmrMessage",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "AmrOrganism",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "AmrStatus",
                table: "Runs");
        }
    }
}
