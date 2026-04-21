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
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PoWBits = table.Column<int>(type: "INTEGER", nullable: false),
                    PercentageFeeForward = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    PercentageFeeReverse = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    MinAmountForwardSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MinAmountReverseSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxAmountForwardSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxAmountReverseSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MiningFeeForwardSat = table.Column<long>(type: "INTEGER", nullable: false),
                    MiningFeeReverseSat = table.Column<long>(type: "INTEGER", nullable: false),
                    SlotsPresent = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotsMissed = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbSwapProvider", x => x.Pubkey);
                });

            migrationBuilder.CreateTable(
                name: "DbSwap",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FrontendId = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ProviderPubkey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserIpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsForward = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountToPaySats = table.Column<long>(type: "INTEGER", nullable: false),
                    AmountToReceiveSats = table.Column<long>(type: "INTEGER", nullable: false),
                    ClientAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LockupAddress = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LockupOutputIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    FundingTxId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TimeoutBlockHeight = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcceptedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FundingTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SpentTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FundingTxData = table.Column<string>(type: "TEXT", maxLength: 8388608, nullable: true),
                    ClientTxId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ClientTxData = table.Column<string>(type: "TEXT", maxLength: 8388608, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbSwap", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DbSwap_DbSwapProvider_ProviderPubkey",
                        column: x => x.ProviderPubkey,
                        principalTable: "DbSwapProvider",
                        principalColumn: "Pubkey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DbSwap_FrontendId",
                table: "DbSwap",
                column: "FrontendId");

            migrationBuilder.CreateIndex(
                name: "IX_DbSwap_IsForward",
                table: "DbSwap",
                column: "IsForward");

            migrationBuilder.CreateIndex(
                name: "IX_DbSwap_ProviderPubkey",
                table: "DbSwap",
                column: "ProviderPubkey");

            migrationBuilder.CreateIndex(
                name: "IX_DbSwap_Status",
                table: "DbSwap",
                column: "Status");

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
                name: "DbSwap");

            migrationBuilder.DropTable(
                name: "DbSwapProvider");
        }
    }
}
