using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeJ.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MergedIntoTradeId",
                table: "Trades",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_MergedIntoTradeId",
                table: "Trades",
                column: "MergedIntoTradeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Trades_Trades_MergedIntoTradeId",
                table: "Trades",
                column: "MergedIntoTradeId",
                principalTable: "Trades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trades_Trades_MergedIntoTradeId",
                table: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_Trades_MergedIntoTradeId",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "MergedIntoTradeId",
                table: "Trades");
        }
    }
}
