using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopPOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6B_ReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_IsCancelled_PaymentDate_Method",
                table: "SupplierPayments",
                columns: new[] { "IsCancelled", "PaymentDate", "Method" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Status_Reason_AdjustmentDate",
                table: "StockAdjustments",
                columns: new[] { "Status", "Reason", "AdjustmentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_Status_ReturnDate",
                table: "SaleReturns",
                columns: new[] { "Status", "ReturnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_Method_PaidAt",
                table: "SalePayments",
                columns: new[] { "Method", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_Status_PurchaseDate",
                table: "Purchases",
                columns: new[] { "Status", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_IsCancelled_ExpenseDate_ExpenseCategoryId",
                table: "Expenses",
                columns: new[] { "IsCancelled", "ExpenseDate", "ExpenseCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp_EntityName_Action",
                table: "AuditLogs",
                columns: new[] { "Timestamp", "EntityName", "Action" });

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_IsCancelled_PaymentDate_Method",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_Status_Reason_AdjustmentDate",
                table: "StockAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_SaleReturns_Status_ReturnDate",
                table: "SaleReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalePayments_Method_PaidAt",
                table: "SalePayments");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_Status_PurchaseDate",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_IsCancelled_ExpenseDate_ExpenseCategoryId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp_EntityName_Action",
                table: "AuditLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
