using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopPOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_CurrencySearchAndFilteredIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Units_Name",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name_Phone",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductBarcodes_Barcode",
                table: "ProductBarcodes");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Brands_Name",
                table: "Brands");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencySymbol",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "Rs",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimals",
                table: "ShopSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Products",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE ShopSettings SET CurrencySymbol = 'Rs' WHERE CurrencySymbol = '$';");

            migrationBuilder.Sql(@"
                UPDATE Products 
                SET SearchText = lower(
                    trim(
                        Name || ' ' || 
                        Sku || ' ' || 
                        coalesce(Barcode, '') || ' ' || 
                        coalesce((SELECT group_concat(Barcode, ' ') FROM ProductBarcodes WHERE ProductBarcodes.ProductId = Products.Id AND ProductBarcodes.IsDeleted = 0), '')
                    )
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                table: "Units",
                column: "Name",
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name_Phone",
                table: "Suppliers",
                columns: new[] { "Name", "Phone" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "Barcode IS NOT NULL AND IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SearchText",
                table: "Products",
                column: "SearchText");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBarcodes_Barcode",
                table: "ProductBarcodes",
                column: "Barcode",
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId_Name",
                table: "Categories",
                columns: new[] { "ParentCategoryId", "Name" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Name",
                table: "Brands",
                column: "Name",
                unique: true,
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Units_Name",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name_Phone",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SearchText",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductBarcodes_Barcode",
                table: "ProductBarcodes");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Brands_Name",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "CurrencyDecimals",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Products");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencySymbol",
                table: "ShopSettings",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldDefaultValue: "Rs");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                table: "Units",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name_Phone",
                table: "Suppliers",
                columns: new[] { "Name", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBarcodes_Barcode",
                table: "ProductBarcodes",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId_Name",
                table: "Categories",
                columns: new[] { "ParentCategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Name",
                table: "Brands",
                column: "Name",
                unique: true);
        }
    }
}
