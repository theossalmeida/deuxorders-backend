# DeuxOrders API Reference

**Base URL (production):** `https://api-orders.deuxcerie.com.br`
**Base URL (local):** `http://localhost:5062`
**All routes prefixed with:** `/api/v1/`

---

## Critical Conventions

- **Prices are always integers in cents.** `1999` = R$19,99. Never use decimals.
- **Enums are serialized as strings.** Send `"Received"`, not `1`.
- **Dates are ISO 8601 UTC.** Example: `"2026-03-18T10:30:00Z"`.
- **Product endpoints use `multipart/form-data`.** All others use `application/json`.
- **JWT Bearer token required on all endpoints except `POST /auth/login`.**

---

## Authentication

### POST /auth/login
```json
// Request
{ "email": "user@example.com", "password": "secret" }

// Response 200
{ "token": "<jwt>" }
```
Rate-limited to 10 req/min. Store the token and send it as `Authorization: Bearer <token>` on every subsequent request.

### POST /auth/register *(requires JWT)*
```json
// Request
{ "name": "João", "username": "joao", "email": "joao@example.com", "password": "min6chars" }

// Response 200
{ "message": "Usuário registrado com sucesso!" }
```

---

## Orders

Default status on creation: `"Received"`.

### POST /orders/new
```json
{
  "clientId": "guid",
  "deliveryDate": "2026-04-01T00:00:00Z",
  "items": [
    {
      "productId": "guid",
      "quantity": 2,
      "unitPrice": 1500,
      "observation": "sem açúcar",   // optional
      "massa": "integral",           // optional
      "sabor": "chocolate"           // optional
    }
  ],
  "references": ["order-references/abc.jpg"]  // optional, max 3
}
```
Response: `201 Created` → OrderResponse (see shape below).

### GET /orders/{id}
Response: `200 OK` → OrderResponse.

### GET /orders/all
Query params: `page` (default 1), `size` (default 10, max 100), `status` (OrderStatus string, optional).
```json
// Response
{ "items": [OrderResponse], "totalCount": 42, "pageNumber": 1, "pageSize": 10 }
```

### PUT /orders/{id}
All fields optional. Items are upserted (created if new, updated if existing).
```json
{
  "deliveryDate": "2026-04-05T00:00:00Z",
  "status": 4,                    // int value of OrderStatus enum
  "items": [
    {
      "productId": "guid",
      "quantity": 3,              // optional
      "paidUnitPrice": 1400,      // optional, overrides base price
      "observation": "gelado",    // optional
      "massa": "tradicional",     // optional
      "sabor": "baunilha"         // optional
    }
  ],
  "references": ["order-references/new.jpg"]  // appended, not replaced
}
```
Note: Items can only be added/updated on orders with status `"Received"`.

### PATCH /orders/{id}/complete
No body. Marks order as `"Completed"`. Cannot complete a canceled order.

### PATCH /orders/{id}/cancel
No body. Marks order as `"Canceled"`. Cannot cancel a completed order.

### PATCH /orders/{id}/items/{productId}/cancel
No body. Cancels a single item. Order must be `"Received"`.

### PATCH /orders/{id}/items/{productId}/quantity
```json
{ "increment": 2 }   // negative to decrement; cannot reach 0 or below
```
Order must be `"Received"`. Item cannot be already canceled.

### DELETE /orders/{id}
Response: `204 No Content`.

### POST /orders/references/presigned-url
```json
// Request
{ "fileName": "photo.jpg", "contentType": "image/jpeg" }

// Response
{ "uploadUrl": "https://r2.../...", "objectKey": "order-references/{guid}.jpg" }
```
Workflow: call this → PUT the file to `uploadUrl` → include `objectKey` in order create/update.

### DELETE /orders/{id}/references
```json
{ "objectKey": "order-references/abc.jpg" }
```
Response: `200 OK` → updated OrderResponse.

---

## OrderResponse shape
```json
{
  "id": "guid",
  "deliveryDate": "2026-04-01T00:00:00Z",
  "status": "Received",
  "clientId": "guid",
  "clientName": "Maria",
  "totalPaid": 3000,
  "totalValue": 3000,
  "references": ["order-references/abc.jpg"],
  "items": [
    {
      "productId": "guid",
      "productName": "Bolo de Chocolate",
      "productSize": "P",
      "observation": "sem açúcar",
      "massa": "integral",
      "sabor": "chocolate",
      "quantity": 2,
      "paidUnitPrice": 1500,
      "baseUnitPrice": 1500,
      "itemCanceled": false,
      "totalPaid": 3000,
      "totalValue": 3000
    }
  ]
}
```

---

## OrderStatus enum (send as string)
| Value | String |
|-------|--------|
| 1 | `"Pending"` |
| 2 | `"Completed"` |
| 3 | `"Canceled"` |
| 4 | `"Received"` ← default |
| 5 | `"Preparing"` |
| 6 | `"WaitingPickupOrDelivery"` |

---

## Clients

### POST /clients/new
```json
{ "name": "Maria Silva", "mobile": "11999998888" }
// Response 201 → ClientResponse
```

### GET /clients/{id}
### GET /clients/all
Query params: `search` (name substring), `status` (bool).

### GET /clients/dropdown
Query params: `status` (bool optional). Returns `[{ "id": "guid", "name": "string" }]`.

### PUT /clients/{id}
```json
{ "name": "Maria S.", "mobile": "11999990000", "status": true }
```

### PATCH /clients/{id}/active — PATCH /clients/{id}/inactive
No body. Returns 400 if already in target state.

### DELETE /clients/{id}
Returns 400 if client has existing orders.

---

## Products

**All create/update use `multipart/form-data`, not JSON.**

### POST /products/new *(multipart/form-data)*
| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Name | string | Yes | max 100 chars |
| Price | int | Yes | cents, > 0 |
| Description | string | No | |
| Category | string | No | |
| Size | string | No | |
| Image | file | No | JPEG/PNG/WebP, max 5 MB |

Response: `201 Created` → ProductResponse.

### GET /products/{id}
### GET /products/all
Query params: `search`, `status` (bool).

### GET /products/dropdown
Returns `[{ "id": "guid", "name": "string", "price": number }]`.

### PUT /products/{id} *(multipart/form-data)*
Same fields as create. If a new image is provided, the old one is deleted from storage.

### PATCH /products/{id}/active — PATCH /products/{id}/inactive
No body.

### DELETE /products/{id}/image
No body. Deletes image from storage and clears the field.

### DELETE /products/{id}
Returns 400 if product belongs to an existing order.

---

## ProductResponse shape
```json
{
  "id": "guid",
  "name": "Bolo de Mel",
  "price": 2500,
  "status": true,
  "image": "https://cdn.../products-images/abc.jpg",
  "category": "Bolos",
  "size": "M"
}
```

---

## Dashboard

All accept query params: `startDate`, `endDate` (ISO8601), `status` (OrderStatus string).

### GET /dashboard/summary
```json
{
  "totalRevenue": 150000,
  "totalValue": 160000,
  "totalDiscount": 10000,
  "totalOrders": 12,
  "pendingOrders": 3,
  "completedOrders": 8,
  "canceledOrders": 1,
  "averageRevenuePerOrder": 12500
}
```

### GET /dashboard/revenue-over-time
```json
{
  "dataPoints": [
    { "date": "2026-03-18", "revenue": 45000, "orderCount": 3 }
  ]
}
```

### GET /dashboard/top-products
Extra param: `limit` (default 10).
```json
[{ "productId": "guid", "productName": "Bolo", "totalRevenue": 90000, "totalQuantitySold": 6, "orderCount": 4 }]
```

### GET /dashboard/top-clients
Extra param: `limit` (default 10).
```json
[{ "clientId": "guid", "clientName": "Maria", "totalRevenue": 75000, "orderCount": 5 }]
```

### GET /dashboard/export
Params: `from`, `to`, `status`, `format` (`"csv"` or `"pdf"`).
Returns a file download. Canceled items excluded from export.

---

## Error Handling

| Status | Cause |
|--------|-------|
| 400 | Validation failure (FluentValidation), business rule violation (`InvalidOperationException`), DB constraint |
| 401 | Missing or invalid JWT |
| 404 | Resource not found |
| 500 | Unhandled exception |

Business rule errors return a plain string message body, not a JSON object.
