using System;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxERP.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260424180000_AddProductRecipeOptions")]
    public partial class AddProductRecipeOptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_recipe_options",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_recipe_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_recipe_options_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_recipe_option_items",
                schema: "inventory",
                columns: table => new
                {
                    RecipeOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityNeeded = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_recipe_option_items", x => new { x.RecipeOptionId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_product_recipe_option_items_inventory_materials_MaterialId",
                        column: x => x.MaterialId,
                        principalSchema: "inventory",
                        principalTable: "inventory_materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_recipe_option_items_product_recipe_options_RecipeO~",
                        column: x => x.RecipeOptionId,
                        principalSchema: "inventory",
                        principalTable: "product_recipe_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_recipe_option_items_MaterialId",
                schema: "inventory",
                table: "product_recipe_option_items",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_product_recipe_options_ProductId_Type_Name",
                schema: "inventory",
                table: "product_recipe_options",
                columns: new[] { "ProductId", "Type", "Name" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_recipe_option_items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "product_recipe_options",
                schema: "inventory");
        }
    }
}
