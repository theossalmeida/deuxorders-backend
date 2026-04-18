using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS cash;");

            migrationBuilder.CreateTable(
                name: "cash_flow_entries",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BillingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Counterparty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeletionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_flow_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cash_flow_audit_log",
                schema: "cash",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousSnapshotJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_flow_audit_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_flow_entries_AuthorUserId",
                schema: "cash",
                table: "cash_flow_entries",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_flow_entries_BillingDate",
                schema: "cash",
                table: "cash_flow_entries",
                column: "BillingDate");

            migrationBuilder.CreateIndex(
                name: "IX_cash_flow_entries_Category_BillingDate",
                schema: "cash",
                table: "cash_flow_entries",
                columns: new[] { "Category", "BillingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_flow_entries_Type_BillingDate",
                schema: "cash",
                table: "cash_flow_entries",
                columns: new[] { "Type", "BillingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_flow_entries_Source_SourceId",
                schema: "cash",
                table: "cash_flow_entries",
                columns: new[] { "Source", "SourceId" },
                unique: true,
                filter: "\"Source\" <> 1");

            migrationBuilder.CreateIndex(
                name: "IX_cash_flow_audit_log_EntryId",
                schema: "cash",
                table: "cash_flow_audit_log",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_flow_audit_log_OccurredAt",
                schema: "cash",
                table: "cash_flow_audit_log",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cash_flow_audit_log", schema: "cash");
            migrationBuilder.DropTable(name: "cash_flow_entries", schema: "cash");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS cash;");
        }
    }
}
