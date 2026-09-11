using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeJ.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeStopLossTakeProfit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StopLoss",
                table: "Trades",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TakeProfit",
                table: "Trades",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StopLoss",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "TakeProfit",
                table: "Trades");
        }
    }
}
