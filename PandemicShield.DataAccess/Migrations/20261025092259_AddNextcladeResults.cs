using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandemicShield.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddNextcladeResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SequenceResultId",
                table: "Threats",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Threats",
                type: "text",
                nullable: false,
                defaultValue: "orf-motif");   // усі старі сповіщення — від пошуку мотивів у ORF

            migrationBuilder.AddColumn<string>(
                name: "Variant",
                table: "Threats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextcladeCompletedAt",
                table: "Runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextcladeDataset",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextcladeMessage",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextcladeStatus",
                table: "Runs",
                type: "text",
                nullable: false,
                defaultValue: "NotRequested");

            migrationBuilder.AddColumn<int>(
                name: "SequencesAnalyzed",
                table: "Runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SequenceResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeqName = table.Column<string>(type: "text", nullable: false),
                    Dataset = table.Column<string>(type: "text", nullable: false),
                    DatasetTag = table.Column<string>(type: "text", nullable: true),
                    Clade = table.Column<string>(type: "text", nullable: true),
                    CladeDisplay = table.Column<string>(type: "text", nullable: true),
                    Lineage = table.Column<string>(type: "text", nullable: true),
                    QcStatus = table.Column<string>(type: "text", nullable: true),
                    QcScore = table.Column<double>(type: "double precision", nullable: true),
                    Coverage = table.Column<double>(type: "double precision", nullable: true),
                    IsReverseComplement = table.Column<bool>(type: "boolean", nullable: false),
                    TotalSubstitutions = table.Column<int>(type: "integer", nullable: false),
                    TotalDeletions = table.Column<int>(type: "integer", nullable: false),
                    TotalInsertions = table.Column<int>(type: "integer", nullable: false),
                    TotalMissing = table.Column<int>(type: "integer", nullable: false),
                    TotalFrameShifts = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    Warnings = table.Column<string>(type: "text", nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequenceResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SequenceResults_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VariantMarkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Notation = table.Column<string>(type: "text", nullable: false),
                    Dataset = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantMarkers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mutations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Gene = table.Column<string>(type: "text", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    EndPosition = table.Column<int>(type: "integer", nullable: true),
                    Ref = table.Column<string>(type: "text", nullable: true),
                    Alt = table.Column<string>(type: "text", nullable: true),
                    Label = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mutations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mutations_SequenceResults_SequenceResultId",
                        column: x => x.SequenceResultId,
                        principalTable: "SequenceResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mutations_Label",
                table: "Mutations",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_Mutations_RunId",
                table: "Mutations",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Mutations_SequenceResultId",
                table: "Mutations",
                column: "SequenceResultId");

            migrationBuilder.CreateIndex(
                name: "IX_SequenceResults_RunId",
                table: "SequenceResults",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mutations");

            migrationBuilder.DropTable(
                name: "VariantMarkers");

            migrationBuilder.DropTable(
                name: "SequenceResults");

            migrationBuilder.DropColumn(
                name: "SequenceResultId",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "Threats");

            migrationBuilder.DropColumn(
                name: "NextcladeCompletedAt",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "NextcladeDataset",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "NextcladeMessage",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "NextcladeStatus",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "SequencesAnalyzed",
                table: "Runs");
        }
    }
}
