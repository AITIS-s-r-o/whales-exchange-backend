using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhalesExchangeBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimPk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaimPublicKey",
                table: "DbSwap",
                type: "TEXT",
                maxLength: 66,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DbSwap_ClaimPublicKey",
                table: "DbSwap",
                column: "ClaimPublicKey");

            migrationBuilder.CreateIndex(
                name: "IX_DbSwap_ClientAddress",
                table: "DbSwap",
                column: "ClientAddress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DbSwap_ClaimPublicKey",
                table: "DbSwap");

            migrationBuilder.DropIndex(
                name: "IX_DbSwap_ClientAddress",
                table: "DbSwap");

            migrationBuilder.DropColumn(
                name: "ClaimPublicKey",
                table: "DbSwap");
        }
    }
}
