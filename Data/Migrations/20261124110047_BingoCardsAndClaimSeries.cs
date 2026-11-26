using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Julebanko.Data.Migrations
{
    /// <inheritdoc />
    public partial class BingoCardsAndClaimSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoValidated",
                table: "Claims",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BingoCardId",
                table: "Claims",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeriesNumber",
                table: "Claims",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationNote",
                table: "Claims",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidationStatus",
                table: "Claims",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BingoCardBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "int", nullable: false),
                    PlateCount = table.Column<int>(type: "int", nullable: false),
                    PlatesPerPage = table.Column<int>(type: "int", nullable: false),
                    BannerText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BingoCardBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BingoCardBatches_AdminUsers_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BingoCardBatches_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BingoCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    SeasonId = table.Column<int>(type: "int", nullable: false),
                    SeriesNumber = table.Column<int>(type: "int", nullable: false),
                    CellsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BingoCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BingoCards_BingoCardBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "BingoCardBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BingoCards_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_BingoCardId",
                table: "Claims",
                column: "BingoCardId");

            migrationBuilder.CreateIndex(
                name: "IX_BingoCardBatches_CreatedByAdminId",
                table: "BingoCardBatches",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BingoCardBatches_SeasonId",
                table: "BingoCardBatches",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_BingoCards_BatchId",
                table: "BingoCards",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BingoCards_SeasonId_Fingerprint",
                table: "BingoCards",
                columns: new[] { "SeasonId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BingoCards_SeasonId_SeriesNumber",
                table: "BingoCards",
                columns: new[] { "SeasonId", "SeriesNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_BingoCards_BingoCardId",
                table: "Claims",
                column: "BingoCardId",
                principalTable: "BingoCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Claims_BingoCards_BingoCardId",
                table: "Claims");

            migrationBuilder.DropTable(
                name: "BingoCards");

            migrationBuilder.DropTable(
                name: "BingoCardBatches");

            migrationBuilder.DropIndex(
                name: "IX_Claims_BingoCardId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "AutoValidated",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "BingoCardId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "SeriesNumber",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ValidationNote",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ValidationStatus",
                table: "Claims");
        }
    }
}
