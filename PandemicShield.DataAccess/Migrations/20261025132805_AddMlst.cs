using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandemicShield.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddMlst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MlstAlleles",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MlstCompletedAt",
                table: "Runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlstMessage",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlstScheme",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlstSequenceType",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlstStatus",
                table: "Runs",
                type: "text",
                nullable: false,
                // Старі запуски MLST не замовляли (порожній рядок не розібрався б як ToolStatus).
                defaultValue: "NotRequested");

            migrationBuilder.AddColumn<string>(
                name: "MlstVersion",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Runs_MlstSequenceType",
                table: "Runs",
                column: "MlstSequenceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Runs_MlstSequenceType",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "MlstAlleles",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "MlstCompletedAt",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "MlstMessage",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "MlstScheme",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "MlstSequenceType",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "MlstStatus",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "MlstVersion",
                table: "Runs");
        }
    }
}
