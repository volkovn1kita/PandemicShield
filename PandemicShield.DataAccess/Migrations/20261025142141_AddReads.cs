using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandemicShield.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReadsSummary",
                table: "Runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Imports",
                type: "text",
                nullable: false,
                // Пакети, створені до FASTQ, — це номери NCBI або архіви; точно відрізнити їх можна за ArchiveName.
                defaultValue: "accessions");

            migrationBuilder.Sql(@"UPDATE ""Imports"" SET ""Kind"" = 'archive' WHERE ""ArchiveName"" IS NOT NULL");

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "Imports",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadsSummary",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Imports");

            migrationBuilder.DropColumn(
                name: "Platform",
                table: "Imports");
        }
    }
}
