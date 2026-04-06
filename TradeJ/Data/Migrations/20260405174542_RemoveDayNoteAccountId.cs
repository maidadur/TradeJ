using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeJ.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDayNoteAccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DayNotes_Accounts_AccountId",
                table: "DayNotes");

            migrationBuilder.DropIndex(
                name: "IX_DayNotes_AccountId_Date",
                table: "DayNotes");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "DayNotes");

            migrationBuilder.CreateIndex(
                name: "IX_DayNotes_Date",
                table: "DayNotes",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DayNotes_Date",
                table: "DayNotes");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "DayNotes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DayNotes_AccountId_Date",
                table: "DayNotes",
                columns: new[] { "AccountId", "Date" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DayNotes_Accounts_AccountId",
                table: "DayNotes",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
