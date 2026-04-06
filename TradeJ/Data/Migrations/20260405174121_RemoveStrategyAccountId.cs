using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeJ.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStrategyAccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Strategies_Accounts_AccountId",
                table: "Strategies");

            migrationBuilder.DropIndex(
                name: "IX_Strategies_AccountId",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Strategies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Strategies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_AccountId",
                table: "Strategies",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Strategies_Accounts_AccountId",
                table: "Strategies",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
