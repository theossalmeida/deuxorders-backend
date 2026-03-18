using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxOrders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMassaAndSaborToOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Massa",
                table: "order_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sabor",
                table: "order_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Massa",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "Sabor",
                table: "order_items");
        }
    }
}
