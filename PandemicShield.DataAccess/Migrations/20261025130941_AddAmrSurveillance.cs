using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandemicShield.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAmrSurveillance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SurveillanceIsolates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Taxgroup = table.Column<string>(type: "text", nullable: false),
                    PdgVersion = table.Column<string>(type: "text", nullable: true),
                    TargetAccession = table.Column<string>(type: "text", nullable: false),
                    AssemblyAccession = table.Column<string>(type: "text", nullable: true),
                    BioSample = table.Column<string>(type: "text", nullable: true),
                    Organism = table.Column<string>(type: "text", nullable: true),
                    Strain = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "text", nullable: true),
                    CollectionDate = table.Column<string>(type: "text", nullable: true),
                    CollectionYear = table.Column<int>(type: "integer", nullable: true),
                    IsolationSource = table.Column<string>(type: "text", nullable: true),
                    Host = table.Column<string>(type: "text", nullable: true),
                    AmrGenotypes = table.Column<string>(type: "text", nullable: true),
                    AmrGeneCount = table.Column<int>(type: "integer", nullable: false),
                    AmrFinderVersion = table.Column<string>(type: "text", nullable: true),
                    DatabaseVersion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveillanceIsolates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveillanceSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Taxgroup = table.Column<string>(type: "text", nullable: false),
                    Countries = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PdgVersion = table.Column<string>(type: "text", nullable: true),
                    BytesTotal = table.Column<long>(type: "bigint", nullable: false),
                    BytesRead = table.Column<long>(type: "bigint", nullable: false),
                    RowsScanned = table.Column<int>(type: "integer", nullable: false),
                    IsolatesStored = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveillanceSyncs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveillanceGenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsolateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Family = table.Column<string>(type: "text", nullable: false),
                    Qualifier = table.Column<string>(type: "text", nullable: true),
                    Class = table.Column<string>(type: "text", nullable: true),
                    Subclass = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveillanceGenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveillanceGenes_SurveillanceIsolates_IsolateId",
                        column: x => x.IsolateId,
                        principalTable: "SurveillanceIsolates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SurveillanceGenes_Family",
                table: "SurveillanceGenes",
                column: "Family");

            migrationBuilder.CreateIndex(
                name: "IX_SurveillanceGenes_IsolateId",
                table: "SurveillanceGenes",
                column: "IsolateId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveillanceIsolates_AssemblyAccession",
                table: "SurveillanceIsolates",
                column: "AssemblyAccession");

            migrationBuilder.CreateIndex(
                name: "IX_SurveillanceIsolates_Taxgroup_Country",
                table: "SurveillanceIsolates",
                columns: new[] { "Taxgroup", "Country" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveillanceGenes");

            migrationBuilder.DropTable(
                name: "SurveillanceSyncs");

            migrationBuilder.DropTable(
                name: "SurveillanceIsolates");
        }
    }
}
