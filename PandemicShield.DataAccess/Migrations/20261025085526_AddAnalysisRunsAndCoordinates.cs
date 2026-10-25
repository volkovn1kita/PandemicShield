using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandemicShield.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRunsAndCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Старі GlobalPosition були байтовими зсувами у файлі (разом із заголовком і переносами рядків),
            // а не координатами в геномі, тож їх не переносимо.
            migrationBuilder.DropColumn(
                name: "GlobalPosition",
                table: "Threats");

            migrationBuilder.AddColumn<int>(
                name: "AaPosition",
                table: "Threats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "NtEnd",
                table: "Threats",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NtStart",
                table: "Threats",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "RecordId",
                table: "Threats",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RunId",
                table: "Threats",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<char>(
                name: "Strand",
                table: "Threats",
                type: "character(1)",
                nullable: false,
                defaultValue: '.');   // '.' — невідомий ланцюг (як у GFF) для старих записів

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StreamedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    BaseCount = table.Column<long>(type: "bigint", nullable: false),
                    InvalidSymbolCount = table.Column<long>(type: "bigint", nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Threats_RunId",
                table: "Threats",
                column: "RunId");

            migrationBuilder.AddForeignKey(
                name: "FK_Threats_Runs_RunId",
                table: "Threats",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Threats_Runs_RunId",
                table: "Threats");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropIndex(
                name: "IX_Threats_RunId",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "NtEnd",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "NtStart",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "RecordId",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "RunId",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "Strand",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "AaPosition",
                table: "Threats");

            migrationBuilder.AddColumn<int>(
                name: "GlobalPosition",
                table: "Threats",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
