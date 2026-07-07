using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSyncedAtAndTransactionDedupeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankAccountTransactions_LinkedBankAccountId",
                table: "BankAccountTransactions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "LinkedBankAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccountTransactions_LinkedBankAccountId_ExternalTransactionId",
                table: "BankAccountTransactions",
                columns: new[] { "LinkedBankAccountId", "ExternalTransactionId" },
                unique: true,
                filter: "\"ExternalTransactionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankAccountTransactions_LinkedBankAccountId_ExternalTransactionId",
                table: "BankAccountTransactions");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "LinkedBankAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccountTransactions_LinkedBankAccountId",
                table: "BankAccountTransactions",
                column: "LinkedBankAccountId");
        }
    }
}
