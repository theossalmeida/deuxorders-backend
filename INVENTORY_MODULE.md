# Inventory Module — Task Plan

## Overview

A live inventory system that tracks raw materials and their stock quantities. Products can have a **recipe** (bill of materials) linking them to one or more materials. When an order transitions to **Preparing** status, the system automatically deducts all materials used by the order's products. The system is **non-restrictive**: insufficient stock produces a warning but never blocks the operation.

---

## Data Model

### 1. `InventoryMaterial` (new entity)

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `Guid.NewGuid()` |
| `Name` | `string` | Required, max 200 chars |
| `Quantity` | `int` | Current stock in **smallest unit** (grams for Kg, mL for L, count for Unit). Can go negative. |
| `UnitCost` | `long` | Weighted average cost in **cents per smallest unit** (cents/g, cents/mL, cents/unit). |
| `MeasureUnit` | `enum MeasureUnit` | `Kg = 1`, `L = 2`, `Unit = 3`. Indicates the display/base unit. |
| `Status` | `bool` | Active/inactive, default `true` |
| `CreatedAt` | `DateTime` | UTC |
| `UpdatedAt` | `DateTime` | UTC |

**Table:** `inventory_materials` in schema `inventory`.

**Integer storage rationale:** Same pattern as prices stored in cents. `Quantity` is always in the smallest unit:
- `MeasureUnit.Kg` → quantity in **grams**. 2500 = 2.5 Kg.
- `MeasureUnit.L` → quantity in **milliliters**. 500 = 0.5 L.
- `MeasureUnit.Unit` → quantity as **count**. 10 = 10 units.

The frontend handles all display formatting (dividing by 1000 for Kg/L). The API sends and receives raw integers — no unit conversion in the backend.

### 2. `ProductRecipeItem` (new junction entity)

| Column | Type | Notes |
|---|---|---|
| `ProductId` | `Guid` | PK part 1, FK → `products.Id` |
| `MaterialId` | `Guid` | PK part 2, FK → `inventory_materials.Id` |
| `QuantityNeeded` | `int` | Amount of material consumed **per 1 unit** of product sold, in the material's smallest unit (g/mL/units). |

**Table:** `product_recipe_items` in schema `inventory`.

**Example:** Product "Bolo de Chocolate" has recipe:
- 500 (grams) of "Farinha" per unit → `QuantityNeeded = 500`
- 200 (mL) of "Leite" per unit → `QuantityNeeded = 200`
- 3 (units) of "Ovos" per unit → `QuantityNeeded = 3`

If an order has 4x "Bolo de Chocolate", deduct: 2000g Farinha, 800mL Leite, 12 Ovos.

### 3. `Product` entity changes

Add:

| Column | Type | Notes |
|---|---|---|
| `HasRecipe` | `bool` | Default `false`. When `true`, the product has linked recipe items. Products that ARE the material themselves (e.g. toppings sold directly) keep this as `false`. |

Add navigation property (read-only collection):
```
IReadOnlyCollection<ProductRecipeItem> RecipeItems
```

### 4. `MeasureUnit` enum (domain)

```csharp
public enum MeasureUnit
{
    Kg   = 1,
    L    = 2,
    Unit = 3
}
```

Only 3 values. The frontend sends quantity in the smallest corresponding unit (g/mL/units) as integers. No conversion logic in the backend.

---

## Weighted Average Cost Calculation

`UnitCost` stores the running weighted average cost per smallest unit (cents/g, cents/mL, cents/unit).

### On Create

User provides `quantity` (int, smallest unit) and `totalCost` (long, cents).

```
unitCost = totalCost / quantity
```

Example: 10g of X for R$100.00 (10000 cents) → `unitCost = 10000 / 10 = 1000` cents/g.

### On Restock

User provides `addedQuantity` (int) and `addedTotalCost` (long, cents).

```
existingTotalCost = (long)currentQuantity * currentUnitCost
newTotalQuantity  = currentQuantity + addedQuantity
newUnitCost       = (existingTotalCost + addedTotalCost) / newTotalQuantity
newQuantity       = newTotalQuantity
```

Example:
- Current: 15g, unitCost=1333 → existingTotal = 15 * 1333 = 19995
- Adding: 10g for 10000 cents
- New: qty=25, unitCost = (19995 + 10000) / 25 = 1199 cents/g

**Important:** Use `long` for intermediate multiplication to avoid overflow. Cast: `(long)currentQuantity * currentUnitCost`.

### On Deduction (order → Preparing)

Only `Quantity` decreases. `UnitCost` stays unchanged. This is standard weighted average costing — consuming inventory does not change the average cost.

### On Restore (order canceled)

Only `Quantity` increases back. `UnitCost` stays unchanged.

### When Quantity is Zero or Negative

`UnitCost` is preserved as the last known average. When new stock is added via restock to a zero-quantity material, the weighted average formula still works: `(0 * oldUnitCost + addedTotalCost) / addedQty = addedTotalCost / addedQty`.

---

## Inventory Deduction Rules

### Trigger: Order status changes to `Preparing`

When `OrderService.UpdateOrderAsync` detects a status change **to Preparing** (and the previous status was NOT already Preparing or later):

1. Load all **non-canceled** order items.
2. For each order item, load the product's recipe (skip products where `HasRecipe == false`).
3. For each recipe item: `material.AdjustQuantity(-(orderItem.Quantity * recipeItem.QuantityNeeded))`
4. If any material's `Quantity` goes below zero, collect a warning string: `"Material '{name}' ficou com estoque negativo: {quantity} {unit}"`.
5. Save all material changes.
6. Return warnings in the API response alongside the order data.

### Trigger: Order canceled (after it was Preparing or later)

When `Order.MarkAsCanceled()` is called and the order's **current status** is `Preparing`, `WaitingPickupOrDelivery`, or `Completed`:

1. **Restore** inventory for all non-canceled items using the same formula (add back instead of subtract).
2. No warnings needed on restore.

### Trigger: Order item canceled (after order is Preparing or later)

When an order item is canceled on an order that is already in `Preparing` or later status:

1. **Restore** inventory for that single item's recipe materials.
2. Requires relaxing the current domain restriction in `Order.CancelItem()` that only allows cancellation when `Status == Received`. Change it to also allow when `Status == Preparing` or `WaitingPickupOrDelivery`.

### Trigger: Order item quantity changed (after order is Preparing or later)

When an item's quantity changes on an order that is already `Preparing` or later:

1. Compute the delta: `newQuantity - oldQuantity`.
2. If delta > 0 → deduct additional materials. If delta < 0 → restore materials.
3. Collect warnings if any material goes negative.
4. Requires relaxing the current domain restriction in `Order.UpdateItemQuantity()` that only allows changes when `Status == Received`.

### Warning Response Format

When warnings exist, the API response includes a `warnings` array alongside the normal order data:

```json
{
  "id": "...",
  "status": "Preparing",
  "items": [...],
  "warnings": [
    "Material 'Farinha' ficou com estoque negativo: -500 g (Kg)",
    "Material 'Leite' ficou com estoque negativo: -200 mL (L)"
  ]
}
```

When no warnings, the `warnings` field is either absent or an empty array.

---

## API Endpoints

### Inventory Materials — `api/v1/inventory`

| Method | Route | Body | Description |
|---|---|---|---|
| `POST` | `/inventory/new` | `{ name, quantity, totalCost, measureUnit }` | Create material with initial stock. `quantity` is int (smallest unit), `totalCost` is long (cents). System calculates `unitCost = totalCost / quantity`. |
| `GET` | `/inventory/all?search=&status=&page=&size=` | — | Paginated list, searchable by name, filterable by active/inactive. |
| `GET` | `/inventory/{id}` | — | Get single material. |
| `PUT` | `/inventory/{id}` | `{ name, measureUnit }` | Update name/unit metadata only. Does NOT change quantity or cost. |
| `POST` | `/inventory/{id}/restock` | `{ quantity, totalCost }` | Add stock. Recalculates weighted average `unitCost`. |
| `PATCH` | `/inventory/{id}/active` | — | Activate material. |
| `PATCH` | `/inventory/{id}/inactive` | — | Deactivate material. |
| `GET` | `/inventory/dropdown?status=` | — | Returns `{ id, name, measureUnit }` list for UI dropdowns (recipe builder). |

**Response format for material:**
```json
{
  "id": "...",
  "name": "Farinha",
  "quantity": 2500,
  "unitCost": 1200,
  "measureUnit": "Kg",
  "status": true,
  "createdAt": "...",
  "updatedAt": "..."
}
```

### Product Recipe — on existing `api/v1/products`

| Method | Route | Body | Description |
|---|---|---|---|
| `PUT` | `/products/{id}/recipe` | `{ items: [{ materialId, quantity }] }` | Set/replace full recipe. `quantity` is int (smallest unit per 1 product unit). Sets `HasRecipe = true`. Empty items array clears recipe (`HasRecipe = false`). |
| `GET` | `/products/{id}/recipe` | — | Get recipe items with material names. |

**Recipe response:**
```json
{
  "hasRecipe": true,
  "items": [
    { "materialId": "...", "materialName": "Farinha", "quantity": 500, "measureUnit": "Kg" },
    { "materialId": "...", "materialName": "Leite", "quantity": 200, "measureUnit": "L" }
  ]
}
```

---

## File Structure

```
DeuxERP.Domain/
  Inventory/
    InventoryMaterial.cs          -- Entity
    ProductRecipeItem.cs          -- Junction entity
    Enums/
      MeasureUnit.cs              -- Kg, L, Unit
  Interfaces/
    IInventoryMaterialRepository.cs

DeuxERP.Application/
  DTOs/
    InventoryDtos.cs              -- Response DTOs for materials
    ProductRecipeDtos.cs          -- Response DTOs for recipe
  Services/
    InventoryService.cs           -- Stock deduction/restoration logic + restock cost calc

DeuxERP.Infrastructure/
  Repositories/
    InventoryMaterialRepository.cs
  Migrations/
    YYYYMMDDHHMMSS_AddInventoryModule.cs          -- materials + recipe tables
    YYYYMMDDHHMMSS_AddProductHasRecipe.cs          -- add has_recipe to products

DeuxERP.API/
  Controllers/
    InventoryController.cs        -- Material CRUD + restock
  Models/
    InventoryRequests.cs          -- CreateMaterialRequest, UpdateMaterialRequest, RestockRequest
    ProductRecipeRequests.cs      -- SetRecipeRequest
  Validations/
    InventoryValidators.cs        -- FluentValidation for all inventory/recipe requests
```

---

## Tasks

### Task 1 — Domain: MeasureUnit enum

**File:** `DeuxERP.Domain/Inventory/Enums/MeasureUnit.cs`

Create the enum with 3 values: `Kg = 1`, `L = 2`, `Unit = 3`.  
Namespace: `DeuxERP.Domain.Inventory`.

---

### Task 2 — Domain: InventoryMaterial entity

**File:** `DeuxERP.Domain/Inventory/InventoryMaterial.cs`

Follow the same rich entity pattern as `Client` and `Product` (private setters, private parameterless EF constructor).

**Constructor:**
```csharp
public InventoryMaterial(string name, int quantity, long totalCost, MeasureUnit measureUnit)
```
- Validates: name not empty, quantity > 0, totalCost > 0.
- Calculates: `UnitCost = totalCost / quantity`.
- Sets `Id = Guid.NewGuid()`, `Status = true`, `CreatedAt = UpdatedAt = DateTime.UtcNow`.

**Properties:**
- `Guid Id`
- `string Name`
- `int Quantity` — smallest unit (g/mL/units)
- `long UnitCost` — cents per smallest unit
- `MeasureUnit MeasureUnit`
- `bool Status`
- `DateTime CreatedAt`
- `DateTime UpdatedAt`

**Methods:**

- `Update(string name, MeasureUnit measureUnit)` — updates metadata only, sets `UpdatedAt`.

- `Restock(int addedQuantity, long addedTotalCost)`:
  - Validates: addedQuantity > 0, addedTotalCost > 0.
  - `long existingTotal = (long)Quantity * UnitCost;`
  - `Quantity += addedQuantity;`
  - `UnitCost = (existingTotal + addedTotalCost) / Quantity;`
  - Sets `UpdatedAt`.

- `AdjustQuantity(int delta)` — adds delta to `Quantity` (can result in negative). Sets `UpdatedAt`. Returns new `Quantity`. Does **not** change `UnitCost`.

- `ChangeStatus()` — toggles `Status`, sets `UpdatedAt`.

No inheritance from `Entity` base class (no domain events), same pattern as `Product` and `Client`.

---

### Task 3 — Domain: ProductRecipeItem entity

**File:** `DeuxERP.Domain/Inventory/ProductRecipeItem.cs`

Simple entity with composite PK `(ProductId, MaterialId)`:
- `Guid ProductId` (private set)
- `Guid MaterialId` (private set)
- `int QuantityNeeded` (private set) — smallest unit per 1 product unit, always > 0
- `virtual Product Product` (navigation, private set)
- `virtual InventoryMaterial Material` (navigation, private set)

**Constructor:** `ProductRecipeItem(Guid productId, Guid materialId, int quantityNeeded)`
- Validates: quantityNeeded > 0.

**Private parameterless constructor** for EF Core.

**Method:** `UpdateQuantity(int quantityNeeded)` — validates > 0, updates value.

---

### Task 4 — Domain: Modify Product entity

**File:** `DeuxERP.Domain/Sales/Product.cs`

Add `using DeuxERP.Domain.Inventory;` at the top.

Add property:
```csharp
public bool HasRecipe { get; private set; }
```
Default `false` (no change to existing constructor needed — `bool` defaults to `false`).

Add backing field + read-only collection:
```csharp
private readonly List<ProductRecipeItem> _recipeItems = new();
public IReadOnlyCollection<ProductRecipeItem> RecipeItems => _recipeItems.AsReadOnly();
```

Add methods:

- `SetRecipe(List<ProductRecipeItem> items)`:
  - `_recipeItems.Clear();`
  - Adds all new items.
  - `HasRecipe = items.Count > 0;`
  - Sets `UpdatedAt`.

- `ClearRecipe()`:
  - `_recipeItems.Clear();`
  - `HasRecipe = false;`
  - Sets `UpdatedAt`.

---

### Task 5 — Domain: Relax order item restrictions

**File:** `DeuxERP.Domain/Sales/Order.cs`

Currently `CancelItem()` and `UpdateItemQuantity()` block any status that isn't `Received`. Change them to block only `Completed` and `Canceled`:

**`CancelItem()`** — change guard:
```csharp
// FROM:
if (Status != OrderStatus.Received)
    throw new InvalidOperationException("Apenas pedidos recebidos podem ter itens cancelados.");

// TO:
if (Status == OrderStatus.Completed || Status == OrderStatus.Canceled)
    throw new InvalidOperationException("Não é possível cancelar itens de um pedido finalizado ou cancelado.");
```

**`UpdateItemQuantity()`** — change guard:
```csharp
// FROM:
if (Status != OrderStatus.Received)
    throw new InvalidOperationException("Não é possível alterar quantidades de um pedido não recebido.");

// TO:
if (Status == OrderStatus.Completed || Status == OrderStatus.Canceled)
    throw new InvalidOperationException("Não é possível alterar quantidades de um pedido finalizado ou cancelado.");
```

These changes allow item modifications during `Preparing` and `WaitingPickupOrDelivery`, which is required for inventory adjustments.

---

### Task 6 — Domain: IInventoryMaterialRepository interface

**File:** `DeuxERP.Domain/Interfaces/IInventoryMaterialRepository.cs`

```csharp
public interface IInventoryMaterialRepository
{
    Task<InventoryMaterial?> GetByIdAsync(Guid id);
    Task<IEnumerable<InventoryMaterial>> GetByManyIdsAsync(IEnumerable<Guid> ids);
    Task<PagedResult<InventoryMaterial>> GetAllAsync(string? search, bool? status, int page = 1, int size = 20);
    void Add(InventoryMaterial material);
    void Update(InventoryMaterial material);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<InventoryDropdownModel>> GetForDropdownAsync(bool? status);
}
```

Also create `InventoryDropdownModel` in `DeuxERP.Domain/Models/`:

```csharp
public record InventoryDropdownModel(Guid Id, string Name, string MeasureUnit);
```

---

### Task 7 — Infrastructure: Migration for inventory tables

**File:** `DeuxERP.Infrastructure/Migrations/YYYYMMDDHHMMSS_AddInventoryModule.cs`

Write manually (no dotnet ef — WDAC blocks it). Follow the pattern in `20260415140000_AddCashSchema.cs`.

Create:

1. Schema `inventory` via `migrationBuilder.EnsureSchema("inventory")`.
2. Table `inventory.inventory_materials`:
   - `Id` uuid PK
   - `Name` varchar(200) NOT NULL
   - `Quantity` integer NOT NULL
   - `UnitCost` bigint NOT NULL
   - `MeasureUnit` integer NOT NULL
   - `Status` boolean NOT NULL DEFAULT true
   - `CreatedAt` timestamptz NOT NULL
   - `UpdatedAt` timestamptz NOT NULL
3. Table `inventory.product_recipe_items`:
   - `ProductId` uuid NOT NULL, FK → `products.Id` (Restrict)
   - `MaterialId` uuid NOT NULL, FK → `inventory.inventory_materials.Id` (Restrict)
   - `QuantityNeeded` integer NOT NULL
   - PK: (`ProductId`, `MaterialId`)

---

### Task 8 — Infrastructure: Migration for Product.HasRecipe

**File:** `DeuxERP.Infrastructure/Migrations/YYYYMMDDHHMMSS_AddProductHasRecipe.cs`

Add column `HasRecipe` (boolean, NOT NULL, default `false`) to table `products`.

---

### Task 9 — Infrastructure: DbContext mappings

**File:** `DeuxERP.Infrastructure/Data/ApplicationDbContext.cs`

Add `using DeuxERP.Domain.Inventory;` at the top.

Add DbSets:
```csharp
public DbSet<InventoryMaterial> InventoryMaterials { get; set; }
public DbSet<ProductRecipeItem> ProductRecipeItems { get; set; }
```

Add to `OnModelCreating`:

```csharp
// InventoryMaterial mapping
modelBuilder.Entity<InventoryMaterial>(entity =>
{
    entity.ToTable("inventory_materials", "inventory");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
    entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid")
        .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
});

// ProductRecipeItem mapping
modelBuilder.Entity<ProductRecipeItem>(entity =>
{
    entity.ToTable("product_recipe_items", "inventory");
    entity.HasKey(e => new { e.ProductId, e.MaterialId });
    entity.HasOne(e => e.Product)
          .WithMany(p => p.RecipeItems)
          .HasForeignKey(e => e.ProductId)
          .OnDelete(DeleteBehavior.Restrict);
    entity.HasOne(e => e.Material)
          .WithMany()
          .HasForeignKey(e => e.MaterialId)
          .OnDelete(DeleteBehavior.Restrict);
});
```

Update the existing Product mapping section — add:
```csharp
entity.Property(p => p.HasRecipe).HasDefaultValue(false);
entity.HasMany(p => p.RecipeItems)
      .WithOne(r => r.Product)
      .HasForeignKey(r => r.ProductId);
```

---

### Task 10 — Infrastructure: InventoryMaterialRepository

**File:** `DeuxERP.Infrastructure/Repositories/InventoryMaterialRepository.cs`

Follow the same pattern as `ProductRepository`:
- Constructor receives `ApplicationDbContext`.
- `GetAllAsync` with search (name contains, case-insensitive) and status filter, paginated.
- `GetByManyIdsAsync` for batch loading by IDs.
- `GetForDropdownAsync` returns `InventoryDropdownModel` projection with `AsNoTracking()`.
- Standard `Add`/`Update`/`Delete`.

---

### Task 11 — Application: Inventory DTOs

**File:** `DeuxERP.Application/DTOs/InventoryDtos.cs`

```csharp
public record InventoryMaterialResponse(
    Guid Id, string Name, int Quantity, long UnitCost, string MeasureUnit,
    bool Status, DateTime CreatedAt, DateTime UpdatedAt);
```

---

### Task 12 — Application: Product Recipe DTOs

**File:** `DeuxERP.Application/DTOs/ProductRecipeDtos.cs`

```csharp
public record RecipeItemResponse(Guid MaterialId, string MaterialName, int Quantity, string MeasureUnit);
public record ProductRecipeResponse(bool HasRecipe, List<RecipeItemResponse> Items);
```

---

### Task 13 — Application: InventoryService

**File:** `DeuxERP.Application/Services/InventoryService.cs`

This service handles stock deduction and restoration. It does NOT handle material CRUD (controller does that directly via repository).

**Constructor dependencies:** `IInventoryMaterialRepository`, `ApplicationDbContext` (for loading recipe items via `ProductRecipeItems` DbSet).

**Important:** Do NOT call `CommitAsync` inside any of these methods. The caller (OrderService/OrderController) commits the full transaction. All changes are tracked by EF Core and committed together.

**Methods:**

#### `Task<List<string>> DeductForOrderAsync(Order order)`

Called when order transitions to Preparing.

1. Get all non-canceled order items from `order.Items`.
2. Get all product IDs. Load products that have `HasRecipe == true` (query `ProductRecipeItems` with `Include(r => r.Material)` where ProductId is in the list).
3. Collect all unique material IDs from recipe items.
4. Load all materials in one query (`GetByManyIdsAsync`).
5. For each order item that has a recipe:
   - For each recipe item: `material.AdjustQuantity(-(orderItem.Quantity * recipeItem.QuantityNeeded))`
   - If `material.Quantity < 0`, add warning: `"Material '{name}' ficou com estoque negativo: {quantity} {measureUnit}"`
6. Return warnings list.

#### `Task RestoreForOrderAsync(Order order)`

Called when an order that was Preparing+ is canceled. Same logic as deduction but with **positive** delta (add back). No warnings.

#### `Task<List<string>> AdjustForItemAsync(Guid productId, int quantityDelta)`

Called when a single order item's quantity changes or is canceled on a Preparing+ order.

- `quantityDelta` is the change in order item quantity (negative = removed, positive = added).
- Load the product's recipe items with materials.
- If no recipe (`HasRecipe == false`), return empty list.
- For each recipe item: `material.AdjustQuantity(quantityDelta * recipeItem.QuantityNeeded)`
- Return warnings if any material went negative.

---

### Task 14 — Application: Modify OrderService

**File:** `DeuxERP.Application/Services/OrderService.cs`

**Inject `InventoryService`** into constructor.

#### Change return type of `UpdateOrderAsync`

From `Task<Order>` to `Task<(Order Order, List<string> Warnings)>`.

#### Add inventory deduction logic inside `UpdateOrderAsync`

After status is updated and items are processed, **before** `CommitAsync()`:

```
var warnings = new List<string>();

if (request.Status.HasValue)
{
    var newStatus = (OrderStatus)request.Status.Value;
    var oldStatus = // capture BEFORE calling order.UpdateStatus()

    // Deduct when transitioning TO Preparing from a pre-Preparing status
    if (newStatus == OrderStatus.Preparing &&
        oldStatus != OrderStatus.Preparing &&
        oldStatus != OrderStatus.WaitingPickupOrDelivery &&
        oldStatus != OrderStatus.Completed)
    {
        warnings = await _inventoryService.DeductForOrderAsync(order);
    }
}
```

**Important:** Capture `order.Status` BEFORE calling `order.UpdateStatus()` so you can compare old vs new.

#### Handle item changes on Preparing+ orders

When `UpsertItem` is called and the order is already `Preparing` or later (before this request's status change), compute the quantity delta for each modified item and call `_inventoryService.AdjustForItemAsync()`. The delta is:
- New item added → delta = new quantity (positive, deduct)
- Existing item quantity changed → delta = new quantity - old quantity

To detect the delta, capture each item's old quantity before calling `order.UpsertItem()`.

---

### Task 15 — API: Request models

**File:** `DeuxERP.API/Models/InventoryRequests.cs`

```csharp
public record CreateMaterialRequest(string Name, int Quantity, long TotalCost, MeasureUnit MeasureUnit);
public record UpdateMaterialRequest(string Name, MeasureUnit MeasureUnit);
public record RestockRequest(int Quantity, long TotalCost);
```

**File:** `DeuxERP.API/Models/ProductRecipeRequests.cs`

```csharp
public record SetRecipeRequest(List<RecipeItemRequest> Items);
public record RecipeItemRequest(Guid MaterialId, int Quantity);
```

---

### Task 16 — API: InventoryController

**File:** `DeuxERP.API/Controllers/InventoryController.cs`

`[Authorize]`, `[ApiController]`, `[Route("api/v1/inventory")]`.

Follow the same pattern as `ClientController` — direct repository calls, no service layer for CRUD.

**Inject:** `IInventoryMaterialRepository`, `IUnitOfWork`, `ILogger<InventoryController>`.

**Endpoints:**

- **`POST /new`** — Create material. Receive `CreateMaterialRequest`. Construct `InventoryMaterial(name, quantity, totalCost, measureUnit)`. Save and return response.

- **`GET /all?search=&status=&page=&size=`** — Paginated list. Max `size = 100`.

- **`GET /{id}`** — Get by ID.

- **`PUT /{id}`** — Update metadata. Receive `UpdateMaterialRequest`. Call `material.Update(name, measureUnit)`.

- **`POST /{id}/restock`** — Receive `RestockRequest`. Call `material.Restock(quantity, totalCost)`. Save and return updated material.

- **`PATCH /{id}/active`** — Activate. Validate not already active.

- **`PATCH /{id}/inactive`** — Deactivate. Validate not already inactive.

- **`GET /dropdown?status=`** — Dropdown list.

---

### Task 17 — API: Product recipe endpoints

**File:** `DeuxERP.API/Controllers/ProductController.cs`

**Add `IInventoryMaterialRepository` to the constructor** for validating material IDs.

Add two new endpoints:

#### `PUT /products/{id}/recipe`

1. Load product by ID. Return 404 if not found.
2. Receive `SetRecipeRequest`.
3. If `items` is empty → call `product.ClearRecipe()`, save, return response.
4. Validate all `materialId`s exist and are active (batch load via `GetByManyIdsAsync`).
5. Build `List<ProductRecipeItem>` from request items.
6. Call `product.SetRecipe(items)`.
7. Save and return `ProductRecipeResponse`.

#### `GET /products/{id}/recipe`

1. Load product by ID. Return 404 if not found.
2. Load recipe items with material names (Include `Material` navigation).
3. Return `ProductRecipeResponse`.

---

### Task 18 — API: Modify OrderController for warnings

**File:** `DeuxERP.API/Controllers/OrderController.cs`

**Add `InventoryService` to the constructor.**

#### `Update` action (PUT /{id}):

`UpdateOrderAsync` now returns `(Order, List<string>)`. Include warnings in the response:

```csharp
var (order, warnings) = await _orderService.UpdateOrderAsync(id, request);
var signedUrls = _storageService.GetSignedReadUrls(order.References);
var response = order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls);

if (warnings.Count > 0)
    return Ok(new { response, warnings });
return Ok(response);
```

#### `Cancel` action (PATCH /{id}/cancel):

Before calling `order.MarkAsCanceled()`, check if the order is `Preparing` or later. If so, call `_inventoryService.RestoreForOrderAsync(order)` **before** the cancel (because `MarkAsCanceled` changes the status, and we need to read items while the order is still in its current state).

#### `CancelItem` action (PATCH /{id}/items/{productId}/cancel):

If the order is `Preparing`, `WaitingPickupOrDelivery`, or `Completed` (before checking — it's the current status), call `_inventoryService.AdjustForItemAsync(productId, -item.Quantity)` to restore materials for the canceled item. Get the item's current quantity BEFORE calling `order.CancelItem()`.

#### `UpdateItemQuantity` action (PATCH /{id}/items/{productId}/quantity):

If the order is `Preparing` or later, call `_inventoryService.AdjustForItemAsync(productId, request.Increment)`. The `increment` is the same value passed to `order.UpdateItemQuantity()` — positive means more items (deduct materials), negative means fewer items (restore materials). Include warnings in response.

---

### Task 19 — API: FluentValidation validators

**File:** `DeuxERP.API/Validations/InventoryValidators.cs`

```csharp
public class CreateMaterialValidator : AbstractValidator<CreateMaterialRequest>
{
    // Name: not empty, max 200 chars
    // Quantity: > 0
    // TotalCost: > 0
    // MeasureUnit: must be a valid enum value (Kg, L, Unit)
}

public class UpdateMaterialValidator : AbstractValidator<UpdateMaterialRequest>
{
    // Name: not empty, max 200 chars
    // MeasureUnit: must be a valid enum value
}

public class RestockValidator : AbstractValidator<RestockRequest>
{
    // Quantity: > 0
    // TotalCost: > 0
}

public class SetRecipeValidator : AbstractValidator<SetRecipeRequest>
{
    // Items: not null (CAN be empty — means clear recipe)
    // Each item: MaterialId not empty, Quantity > 0
    // No duplicate MaterialId in the list
}
```

---

### Task 20 — API: Register DI services

**File:** `DeuxERP.API/Program.cs`

Add next to existing repository/service registrations:

```csharp
builder.Services.AddScoped<IInventoryMaterialRepository, InventoryMaterialRepository>();
builder.Services.AddScoped<InventoryService>();
```

---

### Task 21 — Application: Mapping extensions

**File:** `DeuxERP.Application/Mapping/DtoMappingExtensions.cs`

Add `ToResponse()` extension method for `InventoryMaterial` → `InventoryMaterialResponse`.

The `MeasureUnit` enum is serialized as string globally via `JsonStringEnumConverter`.

---

### Task 22 — Update ApplicationDbContextModelSnapshot

**File:** `DeuxERP.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

Add the new entity definitions (`InventoryMaterial`, `ProductRecipeItem`) to the model snapshot. Add the `HasRecipe` property to the `Product` section. Follow the existing patterns for schema entities (`CashFlowEntry`) and composite keys (`OrderItem`).

---

### Task 23 — Tests

**File:** `DeuxERP.Tests/InventoryIntegrationTest.cs`

Using the existing `BaseIntegrationTest` pattern (in-memory EF Core, test JWT):

1. **Material CRUD** — Create, read, update, list with search/filter, activate/deactivate.
2. **Restock with cost** — Create material → restock → verify quantity and unitCost updated correctly.
3. **Product recipe** — Set recipe, get recipe, clear recipe, validate material IDs exist.
4. **Inventory deduction on Preparing** — Create materials → set product recipe → create order → PUT status to Preparing → verify material quantities decreased correctly.
5. **Warning on negative stock** — Create material with low stock → order exceeds it → Preparing → verify warning in response and quantity is negative in DB.
6. **Restore on cancel** — After deduction (Preparing), cancel order → verify quantities restored.
7. **Item cancel restores materials** — After Preparing, cancel one item → verify only that item's materials restored.
8. **Item quantity change adjusts materials** — After Preparing, change item qty → verify materials adjusted by delta.
9. **No deduction for products without recipe** — Product with `HasRecipe = false` → status change to Preparing → no inventory change.
10. **Weighted average cost** — Create with 1000g at 5000 cents → restock 500g at 4000 cents → verify `unitCost = (1000*5 + 4000) / 1500 = 6` (integer math).

---

## Implementation Order

Execute in this exact sequence. Each phase depends on the previous.

| Phase | Tasks | What it covers |
|---|---|---|
| **Phase 1: Domain** | 1, 2, 3, 4, 5, 6 | Entities, enums, interfaces, order restriction changes. No external deps. |
| **Phase 2: Infrastructure** | 7, 8, 9, 10, 22 | Migrations, DbContext mappings, repository, model snapshot. |
| **Phase 3: Application** | 11, 12, 13, 14, 21 | DTOs, InventoryService, OrderService changes, mapping extensions. |
| **Phase 4: API** | 15, 16, 17, 18, 19, 20 | Request models, controllers, validators, DI wiring. |
| **Phase 5: Tests** | 23 | Integration tests for all scenarios. |

**Build after each phase.** Phase 1-2 must compile. Phase 3-4 must compile and run. Phase 5 validates behavior.

---

## Edge Cases

- **Order already Preparing when items modified via PUT:** If status is not changing (already Preparing) but items are being modified, still adjust inventory for the deltas. Track old vs new quantity for each upserted item.
- **Product recipe changed after order processed:** No retroactive adjustment. Deductions use the recipe at the moment of the status change. This is intentional.
- **Inactive material in recipe:** Setting a recipe requires active materials. Deducting (order → Preparing) proceeds even if a material was deactivated since — the material still exists and is adjusted.
- **Deleting a material used in recipes:** FK constraint (Restrict) prevents deletion. Return clear error.
- **Overflow in cost calculation:** Use `long` for `(long)Quantity * UnitCost` intermediate to avoid int overflow.
- **Zero-quantity material restock:** Formula works: `(0 * oldUnitCost + addedTotalCost) / addedQty = addedTotalCost / addedQty`.
- **Negative quantity restock:** If material is at -50g and user restocks 100g at cost X, formula: `(-50 * oldUnitCost + totalCost) / 50`. The existing negative cost offsets. This is mathematically correct for weighted averages.
