using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransactionCategoryId",
                table: "BankAccountTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TransactionCategories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("f47e9b1a-0f2e-4a3c-9a1d-000000000001"), "car" },
                    { new Guid("f47e9b1a-0f2e-4a3c-9a1d-000000000002"), "gift" },
                    { new Guid("f47e9b1a-0f2e-4a3c-9a1d-000000000003"), "luxury" },
                    { new Guid("f47e9b1a-0f2e-4a3c-9a1d-000000000004"), "groceries" },
                    { new Guid("f47e9b1a-0f2e-4a3c-9a1d-000000000005"), "subscriptions" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccountTransactions_TransactionCategoryId",
                table: "BankAccountTransactions",
                column: "TransactionCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccountTransactions_TransactionCategories_TransactionCategoryId",
                table: "BankAccountTransactions",
                column: "TransactionCategoryId",
                principalTable: "TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccountTransactions_TransactionCategories_TransactionCategoryId",
                table: "BankAccountTransactions");

            migrationBuilder.DropTable(
                name: "TransactionCategories");

            migrationBuilder.DropIndex(
                name: "IX_BankAccountTransactions_TransactionCategoryId",
                table: "BankAccountTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionCategoryId",
                table: "BankAccountTransactions");
        }
    }
}
