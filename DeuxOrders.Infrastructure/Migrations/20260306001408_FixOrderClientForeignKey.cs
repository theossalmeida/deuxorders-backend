using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxOrders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixOrderClientForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_clients_ClientId1",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_ClientId1",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ClientId1",
                table: "orders");

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId1",
                table: "order_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId1",
                table: "order_items",
                column: "OrderId1");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_orders_OrderId1",
                table: "order_items",
                column: "OrderId1",
                principalTable: "orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_items_orders_OrderId1",
                table: "order_items");

            migrationBuilder.DropIndex(
                name: "IX_order_items_OrderId1",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "OrderId1",
                table: "order_items");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId1",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_orders_ClientId1",
                table: "orders",
                column: "ClientId1");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_clients_ClientId1",
                table: "orders",
                column: "ClientId1",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
