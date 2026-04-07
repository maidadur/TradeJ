using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeJ.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAccountIdFromTagCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TagCategories_Accounts_AccountId",
                table: "TagCategories");

            migrationBuilder.DropIndex(
                name: "IX_TagCategories_AccountId",
                table: "TagCategories");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "TagCategories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "TagCategories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TagCategories_AccountId",
                table: "TagCategories",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_TagCategories_Accounts_AccountId",
                table: "TagCategories",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
