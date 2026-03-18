using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxOrders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "checkout_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbacateBillingId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AbacateCustomerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientMobile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    ReferencesJson = table.Column<string>(type: "text", nullable: true),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    CheckoutUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DevMode = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkout_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checkout_sessions_AbacateBillingId",
                table: "checkout_sessions",
                column: "AbacateBillingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkout_sessions");
        }
    }
}
