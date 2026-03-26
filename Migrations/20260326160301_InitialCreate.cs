using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhalesExchangeBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DbSwapProvider",
                columns: table => new
                {
                    Pubkey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PoWBits = table.Column<int>(type: "INTEGER", nullable: false),
                    PercentageFeeForward = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    PercentageFeeReverse = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    MinAmountForwardSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MinAmountReverseSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxAmountForwardSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxAmountReverseSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MiningFeeForwardSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MiningFeeReverseSat = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbSwapProvider", x => x.Pubkey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DbSwapProvider_LastSeen",
                table: "DbSwapProvider",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_DbSwapProvider_Pubkey",
                table: "DbSwapProvider",
                column: "Pubkey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DbSwapProvider");
        }
    }
}
