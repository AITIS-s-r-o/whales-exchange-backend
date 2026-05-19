using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhalesExchangeBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddForwardSwapFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClientAddress",
                table: "DbSwap",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "PaymentHashHex",
                table: "DbSwap",
                type: "TEXT",
                maxLength: 66,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedeemScriptHex",
                table: "DbSwap",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DbSwap_PaymentHashHex",
                table: "DbSwap",
                column: "PaymentHashHex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DbSwap_PaymentHashHex",
                table: "DbSwap");

            migrationBuilder.DropColumn(
                name: "PaymentHashHex",
                table: "DbSwap");

            migrationBuilder.DropColumn(
                name: "RedeemScriptHex",
                table: "DbSwap");

            migrationBuilder.AlterColumn<string>(
                name: "ClientAddress",
                table: "DbSwap",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
