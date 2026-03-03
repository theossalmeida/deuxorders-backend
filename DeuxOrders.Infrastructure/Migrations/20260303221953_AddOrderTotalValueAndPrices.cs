using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxOrders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTotalValueAndPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "order_items",
                newName: "TotalValue");

            migrationBuilder.AddColumn<int>(
                name: "Price",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalValue",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BaseUnitPrice",
                table: "order_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaidUnitPrice",
                table: "order_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "products");

            migrationBuilder.DropColumn(
                name: "TotalValue",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "BaseUnitPrice",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "PaidUnitPrice",
                table: "order_items");

            migrationBuilder.RenameColumn(
                name: "TotalValue",
                table: "order_items",
                newName: "UnitPrice");
        }
    }
}
