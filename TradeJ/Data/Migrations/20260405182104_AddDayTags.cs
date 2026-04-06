using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeJ.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDayTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DayTagDefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayTagDefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DayTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DayTagDefId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DayTags_DayTagDefs_DayTagDefId",
                        column: x => x.DayTagDefId,
                        principalTable: "DayTagDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DayTags_Date_DayTagDefId",
                table: "DayTags",
                columns: new[] { "Date", "DayTagDefId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayTags_DayTagDefId",
                table: "DayTags",
                column: "DayTagDefId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DayTags");

            migrationBuilder.DropTable(
                name: "DayTagDefs");
        }
    }
}
