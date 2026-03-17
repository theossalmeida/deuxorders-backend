# GUIA CLAUDE CODE — deuxorders-backend
## Infraestrutura de Pagamento (EF Core + Migrations + Domínio)

> **Escopo:** Este arquivo guia APENAS alterações de infraestrutura no projeto `deuxorders-backend`.
> Banco de dados, migrations, entidades de domínio e repositório.
> NÃO alterar controllers, services, APIs existentes ou funcionalidades atuais.

---

## Documentação de referência

- AbacatePay API: https://www.abacatepay.com/llms.txt
- AbacatePay Docs: https://docs.abacatepay.com
- AbacatePay Billing: https://docs.abacatepay.com/pages/payment/create
- AbacatePay Webhook: https://docs.abacatepay.com/pages/webhook

---

## Contexto da arquitetura

- **Clean Architecture:** Domain → Application → Infrastructure → API
- **ORM:** Entity Framework Core com PostgreSQL (Npgsql)
- **Migrations:** executar sempre com `--project DeuxOrders.Infrastructure --startup-project DeuxOrders.API`
- **Preços:** sempre em centavos (`int`/`long`), nunca `float`/`decimal`
- **Entidades:** private setters + construtor privado vazio para EF Core
- **IDs:** `Guid.CreateVersion7()` para PaymentTransaction e Order; `Guid.NewGuid()` para os demais
- **Padrão:** UnitOfWork para commits

O projeto `deuxcerie-ecomm-backend` (EcommerceApi) consome estas tabelas via **Dapper direto no PostgreSQL** — mesmo banco, mesma connection string. Portanto toda evolução de schema acontece **apenas aqui**.

---

## O que NÃO deve ser alterado

```
❌ Controllers existentes (Auth, Client, Product, Order, Dashboard)
❌ OrderService, DashboardService
❌ Repositórios existentes
❌ Lógica de negócio existente das Orders
❌ Fluxo de autenticação JWT
❌ ExportService, StorageService
❌ Testes existentes
```

---

## PASSO 1 — Enum PaymentStatus

**Criar:** `DeuxOrders.Domain/Enums/PaymentStatus.cs`

```csharp
namespace DeuxOrders.Domain.Enums
{
    public enum PaymentStatus
    {
        Pending  = 1,
        Paid     = 2,
        Failed   = 3,
        Refunded = 4,
        Disputed = 5
    }
}
```

---

## PASSO 2 — Entidade PaymentTransaction

**Criar:** `DeuxOrders.Domain/Entities/PaymentTransaction.cs`

Regras:
- Private setters em todos os campos
- Construtor privado vazio para EF Core
- `Guid.CreateVersion7()` no construtor público
- State machine: transições de status só pelos métodos listados abaixo

```csharp
using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Domain.Entities
{
    public class PaymentTransaction
    {
        public Guid Id { get; private set; }
        public Guid OrderId { get; private set; }
        public string AbacateBillingId { get; private set; } = null!;
        public string? AbacateCustomerId { get; private set; }
        public string PaymentMethod { get; private set; } = null!;
        public PaymentStatus Status { get; private set; }
        public long AmountCents { get; private set; }
        public long? PaidAmountCents { get; private set; }
        public long? PlatformFeeCents { get; private set; }
        public string? CheckoutUrl { get; private set; }
        public string? ReceiptUrl { get; private set; }
        public string? PayerName { get; private set; }
        public string? PayerTaxIdMasked { get; private set; }
        public string? CardLastFour { get; private set; }
        public string? CardBrand { get; private set; }
        public DateTime? WebhookReceivedAt { get; private set; }
        public string? WebhookEventType { get; private set; }
        public string IdempotencyKey { get; private set; } = null!;
        public string? FailureReason { get; private set; }
        public bool DevMode { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        public PaymentTransaction(
            Guid orderId,
            string abacateBillingId,
            string paymentMethod,
            long amountCents,
            string? checkoutUrl,
            string idempotencyKey,
            bool devMode)
        {
            Id = Guid.CreateVersion7();
            OrderId = orderId;
            AbacateBillingId = abacateBillingId;
            PaymentMethod = paymentMethod;
            Status = PaymentStatus.Pending;
            AmountCents = amountCents;
            CheckoutUrl = checkoutUrl;
            IdempotencyKey = idempotencyKey;
            DevMode = devMode;
            CreatedAt = DateTime.UtcNow;
        }

        private PaymentTransaction() { }

        // Pending → Paid. Se já Paid, ignora (idempotência).
        public void ConfirmPayment(
            long paidAmount, long? platformFee,
            string? payerName, string? payerTaxId,
            string? cardLastFour, string? cardBrand,
            string? receiptUrl, string? eventType)
        {
            if (Status == PaymentStatus.Paid) return;
            if (Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Só é possível confirmar pagamentos Pending.");
            if (paidAmount <= 0)
                throw new InvalidOperationException("paidAmount deve ser maior que zero.");

            Status = PaymentStatus.Paid;
            PaidAmountCents = paidAmount;
            PlatformFeeCents = platformFee;
            PayerName = payerName;
            PayerTaxIdMasked = payerTaxId;
            CardLastFour = cardLastFour;
            CardBrand = cardBrand;
            ReceiptUrl = receiptUrl;
            WebhookReceivedAt = DateTime.UtcNow;
            WebhookEventType = eventType;
            UpdatedAt = DateTime.UtcNow;
        }

        // Pending → Failed
        public void MarkAsFailed(string reason)
        {
            if (Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Só é possível marcar como Failed a partir de Pending.");
            Status = PaymentStatus.Failed;
            FailureReason = reason;
            UpdatedAt = DateTime.UtcNow;
        }

        // Paid → Refunded
        public void MarkAsRefunded(string reason, string? eventType)
        {
            if (Status != PaymentStatus.Paid)
                throw new InvalidOperationException("Só é possível estornar pagamentos Paid.");
            Status = PaymentStatus.Refunded;
            FailureReason = reason;
            WebhookReceivedAt = DateTime.UtcNow;
            WebhookEventType = eventType;
            UpdatedAt = DateTime.UtcNow;
        }

        // Paid → Disputed
        public void MarkAsDisputed(string reason, string? eventType)
        {
            if (Status != PaymentStatus.Paid)
                throw new InvalidOperationException("Só é possível disputar pagamentos Paid.");
            Status = PaymentStatus.Disputed;
            FailureReason = reason;
            WebhookReceivedAt = DateTime.UtcNow;
            WebhookEventType = eventType;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetAbacateCustomerId(string customerId)
        {
            AbacateCustomerId = customerId;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

---

## PASSO 3 — Entidade WebhookEventLog

**Criar:** `DeuxOrders.Domain/Entities/WebhookEventLog.cs`

Regras:
- Entidade **imutável** — apenas construtor, sem métodos de mutação
- Private setters em todos os campos
- Construtor privado para EF Core
- **Sem FK** para nenhuma outra tabela (é log puro)

```csharp
namespace DeuxOrders.Domain.Entities
{
    public class WebhookEventLog
    {
        public Guid Id { get; private set; }
        public DateTime ReceivedAt { get; private set; }
        public string? EventType { get; private set; }
        public string RawPayload { get; private set; } = null!;
        public string? SignatureHeader { get; private set; }
        public bool SignatureValid { get; private set; }
        public bool SecretValid { get; private set; }
        public string? ProcessingResult { get; private set; }
        public string? ErrorMessage { get; private set; }
        public string? AbacateBillingId { get; private set; }
        public int HttpStatusReturned { get; private set; }

        public WebhookEventLog(
            Guid id,
            DateTime receivedAt,
            string? eventType,
            string rawPayload,
            string? signatureHeader,
            bool signatureValid,
            bool secretValid,
            string? processingResult,
            string? errorMessage,
            string? abacateBillingId,
            int httpStatusReturned)
        {
            Id = id;
            ReceivedAt = receivedAt;
            EventType = eventType;
            RawPayload = rawPayload;
            SignatureHeader = signatureHeader;
            SignatureValid = signatureValid;
            SecretValid = secretValid;
            ProcessingResult = processingResult;
            ErrorMessage = errorMessage;
            AbacateBillingId = abacateBillingId;
            HttpStatusReturned = httpStatusReturned;
        }

        private WebhookEventLog() { }
    }
}
```

---

## PASSO 4 — Alterar entidade Order

**Alterar:** `DeuxOrders.Domain/Entities/Order.cs`

Adicionar a propriedade e o método abaixo à classe existente. Não alterar mais nada.

```csharp
// Adicionar junto às outras propriedades:
public string? PaymentSource { get; private set; }

// Adicionar junto aos outros métodos públicos:
public void SetPaymentSource(string source)
{
    if (source != "ADMIN" && source != "ECOMMERCE")
        throw new InvalidOperationException("PaymentSource deve ser 'ADMIN' ou 'ECOMMERCE'.");
    PaymentSource = source;
}
```

> Pedidos existentes terão `PaymentSource = NULL`, o que é correto — tratados como ADMIN.

---

## PASSO 5 — Alterar entidade Product

**Alterar:** `DeuxOrders.Domain/Entities/Product.cs`

Adicionar a propriedade e o método abaixo à classe existente. Não alterar mais nada.

```csharp
// Adicionar junto às outras propriedades:
public string? AbacateStoreProductId { get; private set; }

// Adicionar junto aos outros métodos públicos:
public void SetAbacateStoreProductId(string abacateId)
{
    AbacateStoreProductId = abacateId;
    UpdatedAt = DateTime.UtcNow;
}
```

> **O que é este campo:** Quando um billing é criado na AbacatePay com um produto,
> a resposta retorna `products[].id` (ex: `"prod_123456"`). Esse é o ID interno
> da AbacatePay para aquele produto na loja. Armazena-se aqui para referência futura.
> O `externalId` enviado no billing deve ser o `Id` (Guid) do produto no banco local.
> Referência: https://docs.abacatepay.com/pages/payment/create

---

## PASSO 6 — Interface IPaymentTransactionRepository

**Criar:** `DeuxOrders.Domain/Interfaces/IPaymentTransactionRepository.cs`

```csharp
using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IPaymentTransactionRepository
    {
        Task<PaymentTransaction?> GetByAbacateBillingIdAsync(string billingId);
        Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey);
        Task<PaymentTransaction?> GetByOrderIdAsync(Guid orderId);
        void Add(PaymentTransaction transaction);
    }
}
```

---

## PASSO 7 — Implementação PaymentTransactionRepository

**Criar:** `DeuxOrders.Infrastructure/Repositories/PaymentTransactionRepository.cs`

```csharp
using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Repositories
{
    public class PaymentTransactionRepository : IPaymentTransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentTransactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentTransaction?> GetByAbacateBillingIdAsync(string billingId)
            => await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.AbacateBillingId == billingId);

        public async Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey)
            => await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);

        public async Task<PaymentTransaction?> GetByOrderIdAsync(Guid orderId)
            => await _context.PaymentTransactions
                .Where(t => t.OrderId == orderId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

        public void Add(PaymentTransaction transaction)
            => _context.PaymentTransactions.Add(transaction);
    }
}
```

---

## PASSO 8 — Atualizar ApplicationDbContext

**Alterar:** `DeuxOrders.Infrastructure/Data/ApplicationDBContext.cs`

**8.1 — Adicionar DbSets** (junto aos existentes):
```csharp
public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
public DbSet<WebhookEventLog> WebhookEventLogs { get; set; }
```

**8.2 — Adicionar mapeamentos no `OnModelCreating`** (após os mapeamentos existentes):

```csharp
// PaymentTransaction
modelBuilder.Entity<PaymentTransaction>(entity =>
{
    entity.ToTable("payment_transactions");
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.AbacateBillingId);
    entity.HasIndex(e => e.IdempotencyKey).IsUnique();
    entity.HasIndex(e => e.OrderId);
    entity.HasOne<Order>()
          .WithMany()
          .HasForeignKey(e => e.OrderId)
          .OnDelete(DeleteBehavior.Restrict);
    entity.Property(e => e.AbacateBillingId).HasMaxLength(100).IsRequired();
    entity.Property(e => e.IdempotencyKey).HasMaxLength(100).IsRequired();
    entity.Property(e => e.PaymentMethod).HasMaxLength(20).IsRequired();
    entity.Property(e => e.CheckoutUrl).HasMaxLength(500);
    entity.Property(e => e.ReceiptUrl).HasMaxLength(500);
    entity.Property(e => e.PayerName).HasMaxLength(200);
    entity.Property(e => e.PayerTaxIdMasked).HasMaxLength(30);
    entity.Property(e => e.CardLastFour).HasMaxLength(4);
    entity.Property(e => e.CardBrand).HasMaxLength(30);
    entity.Property(e => e.WebhookEventType).HasMaxLength(50);
    entity.Property(e => e.FailureReason).HasMaxLength(500);
    entity.Property(e => e.AbacateCustomerId).HasMaxLength(100);
});

// WebhookEventLog
modelBuilder.Entity<WebhookEventLog>(entity =>
{
    entity.ToTable("webhook_event_log");
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.ReceivedAt);
    entity.HasIndex(e => e.AbacateBillingId);
    entity.Property(e => e.EventType).HasMaxLength(50);
    entity.Property(e => e.SignatureHeader).HasMaxLength(500);
    entity.Property(e => e.ProcessingResult).HasMaxLength(200);
    entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
    entity.Property(e => e.AbacateBillingId).HasMaxLength(100);
});
```

**8.3 — Adicionar no mapeamento existente de `Order`:**
```csharp
entity.Property(o => o.PaymentSource).HasMaxLength(20).IsRequired(false);
```

**8.4 — Adicionar no mapeamento existente de `Product`:**
```csharp
entity.Property(p => p.AbacateStoreProductId).HasMaxLength(100).IsRequired(false);
```

---

## PASSO 9 — Registrar repositório no DI

**Alterar:** `DeuxOrders.API/Program.cs`

Adicionar na seção de repositórios (junto aos `AddScoped` existentes):

```csharp
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
```

---

## PASSO 10 — Gerar e verificar a Migration

```bash
dotnet ef migrations add AddPaymentInfrastructure \
  --project DeuxOrders.Infrastructure \
  --startup-project DeuxOrders.API
```

**Verificar que a migration gerada contém:**

| Operação | Detalhes |
|---|---|
| `CREATE TABLE payment_transactions` | Com todos os campos descritos no Passo 2 |
| `CREATE UNIQUE INDEX` em `IdempotencyKey` | Garante processamento idempotente de webhooks |
| `CREATE INDEX` em `AbacateBillingId` | Lookup rápido nos webhooks |
| `CREATE INDEX` em `OrderId` | Lookup rápido por pedido |
| `FK payment_transactions → orders` | `OnDelete = RESTRICT` |
| `CREATE TABLE webhook_event_log` | Com todos os campos descritos no Passo 3 |
| `CREATE INDEX` em `ReceivedAt` | Consultas por período |
| `CREATE INDEX` em `AbacateBillingId` | Referência cruzada |
| `ALTER TABLE orders ADD "PaymentSource"` | varchar(20), nullable |
| `ALTER TABLE products ADD "AbacateStoreProductId"` | varchar(100), nullable |

**Garantias de segurança que esta infra provê:**

```
✅ IdempotencyKey UNIQUE — mesmo webhook não processa 2 vezes
✅ State machine na entidade — impede Paid → Pending, etc.
✅ webhook_event_log — audita TUDO (incluindo tentativas inválidas)
✅ PaymentSource — separa pedidos admin vs ecommerce
✅ FK para orders.Id — integridade referencial
✅ Sem dados sensíveis em texto claro (taxId sempre mascarado)
✅ Private setters — impede alteração direta de campos críticos
✅ AbacateBillingId indexado — lookup rápido nos webhooks
```

---

## PASSO 11 — Build e testes

```bash
dotnet build --configuration Release
dotnet test --configuration Release
```

Todos os testes existentes devem passar sem alterações.

---

## Checklist de entrega

- [ ] `PaymentStatus.cs` enum criado
- [ ] `PaymentTransaction.cs` entidade criada com state machine completa
- [ ] `WebhookEventLog.cs` entidade imutável criada sem FK
- [ ] `Order.cs` — `PaymentSource` + `SetPaymentSource` adicionados
- [ ] `Product.cs` — `AbacateStoreProductId` + `SetAbacateStoreProductId` adicionados
- [ ] `IPaymentTransactionRepository.cs` interface criada
- [ ] `PaymentTransactionRepository.cs` implementado
- [ ] `ApplicationDBContext.cs` atualizado (DbSets + todos os mapeamentos)
- [ ] `Program.cs` atualizado (registro do repositório no DI)
- [ ] Migration `AddPaymentInfrastructure` gerada com todas as operações listadas
- [ ] `dotnet build` ✅
- [ ] `dotnet test` ✅ (zero regressões)
