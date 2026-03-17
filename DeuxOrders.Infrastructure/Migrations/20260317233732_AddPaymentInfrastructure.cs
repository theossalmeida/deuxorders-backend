using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeuxOrders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AbacateStoreProductId",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentSource",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbacateBillingId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AbacateCustomerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    PaidAmountCents = table.Column<long>(type: "bigint", nullable: true),
                    PlatformFeeCents = table.Column<long>(type: "bigint", nullable: true),
                    CheckoutUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceiptUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PayerTaxIdMasked = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CardLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    CardBrand = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    WebhookReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WebhookEventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DevMode = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhook_event_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: false),
                    SignatureHeader = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: false),
                    SecretValid = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingResult = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AbacateBillingId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HttpStatusReturned = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_event_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_AbacateBillingId",
                table: "payment_transactions",
                column: "AbacateBillingId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_IdempotencyKey",
                table: "payment_transactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_OrderId",
                table: "payment_transactions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_event_log_AbacateBillingId",
                table: "webhook_event_log",
                column: "AbacateBillingId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_event_log_ReceivedAt",
                table: "webhook_event_log",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "webhook_event_log");

            migrationBuilder.DropColumn(
                name: "AbacateStoreProductId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "PaymentSource",
                table: "orders");
        }
    }
}
