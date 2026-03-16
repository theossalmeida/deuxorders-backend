using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxOrders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE products
                SET
                    ""Size"" = CASE
                        WHEN ""Name"" ~* '15/20\s*cm'  THEN '15/20cm'
                        WHEN ""Name"" ~* 'Mini'         THEN 'Mini'
                        WHEN ""Name"" ~* '15\s*cm'      THEN '15cm'
                        WHEN ""Name"" ~* '20\s*cm'      THEN '20cm'
                        WHEN ""Name"" ~* '25\s*cm'      THEN '25cm'
                        ELSE 'U'
                    END,
                    ""Name"" = TRIM(CASE
                        WHEN ""Name"" ~* '15/20\s*cm'  THEN regexp_replace(""Name"", '\s*15/20\s*cm', '', 'gi')
                        WHEN ""Name"" ~* 'Mini'         THEN regexp_replace(""Name"", '\s*Mini',       '', 'gi')
                        WHEN ""Name"" ~* '15\s*cm'      THEN regexp_replace(""Name"", '\s*15\s*cm',    '', 'gi')
                        WHEN ""Name"" ~* '20\s*cm'      THEN regexp_replace(""Name"", '\s*20\s*cm',    '', 'gi')
                        WHEN ""Name"" ~* '25\s*cm'      THEN regexp_replace(""Name"", '\s*25\s*cm',    '', 'gi')
                        ELSE ""Name""
                    END);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Size",
                table: "products");
        }
    }
}
