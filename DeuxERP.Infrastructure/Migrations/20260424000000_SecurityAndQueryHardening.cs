using System;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxERP.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260424000000_SecurityAndQueryHardening")]
    public partial class SecurityAndQueryHardening : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateTable(
                name: "order_reference_uploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_reference_uploads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_reference_uploads_ObjectKey",
                table: "order_reference_uploads",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_reference_uploads_UserId_OrderId_ConsumedAt",
                table: "order_reference_uploads",
                columns: new[] { "UserId", "OrderId", "ConsumedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatedAt",
                table: "orders",
                column: "CreatedAt",
                descending: new[] { true });

            migrationBuilder.CreateIndex(
                name: "IX_orders_DeliveryDate",
                table: "orders",
                column: "DeliveryDate");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_CreatedAt",
                table: "orders",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_orders_ClientId_CreatedAt",
                table: "orders",
                columns: new[] { "ClientId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_clients_Name_trgm\" ON clients USING gin (\"Name\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_products_Name_trgm\" ON products USING gin (\"Name\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_inventory_materials_Name_trgm\" ON inventory.inventory_materials USING gin (\"Name\" gin_trgm_ops);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS inventory.\"IX_inventory_materials_Name_trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_products_Name_trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_clients_Name_trgm\";");

            migrationBuilder.DropIndex(
                name: "IX_orders_ClientId_CreatedAt",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_Status_CreatedAt",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_DeliveryDate",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_CreatedAt",
                table: "orders");

            migrationBuilder.DropTable(
                name: "order_reference_uploads");
        }
    }
}
