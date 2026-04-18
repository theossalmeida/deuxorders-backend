using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByUserId",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidByUserName",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaidByUserId",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaidByUserName",
                table: "orders");
        }
    }
}
