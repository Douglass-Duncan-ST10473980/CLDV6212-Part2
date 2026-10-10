# Order Queue Processor — CoffeeNChill

## Overview

The **Order Queue Processor** is a queue-triggered Azure Function that handles incoming orders from the `order-processing-queue`. It validates the JSON payload, persists orders to the Azure `Orders` table, and manages their lifecycle through four statuses.

**Author:** Neha Heeralal — ST10478910

---

## Architecture

The processor is made up of five key components:

| File | Purpose |
|------|---------|
| `Models/OrderEntity.cs` | Entity stored in the Azure `Orders` table. Uses `PartitionKey = OrderDate (yyyy-MM-dd)` and `RowKey = OrderId` as the composite key. |
| `Models/OrderStatus.cs` | Enum defining the four lifecycle stages: `Received`, `Preparing`, `Ready`, `Collected`. |
| `DTO/OrderMessage.cs` | Data Transfer Object representing the queue message payload (matches the Producer's `Order` class exactly: `OrderId`, `CustomerName`, `SelectedItemSKUs`, `TotalPrice`, `OrderTimestamp`). |
| `Services/OrderStorageService.cs` | Data access layer. Handles create, read, and status-update operations against Table Storage. |
| `Functions/OrderQueueFunctions.cs` | Queue-triggered functions: `ProcessOrderQueue` and `ProcessOrderQueuePoison`. |
| `Functions/OrderStatusFunctions.cs` | HTTP-triggered functions for viewing and updating order status via REST. |

---

## Message Flow

```
Order Queue Producer (Role 1)
         │
         ▼
  POST /api/orders/queue
         │
         ▼
  order-processing-queue (Base64-encoded JSON)
         │
         ▼
  ProcessOrderQueue (this role)
         │
         ▼
    Orders Table
         │
         ▼
  Status transitions via PUT /api/orders/{id}/status
```

1. The Producer places a Base64-encoded JSON message on the `order-processing-queue`.
2. `ProcessOrderQueue` fires automatically — the `QueueMessage` binding decodes Base64 transparently.
3. The payload is deserialized into an `OrderMessage` DTO.
4. If valid, the order is saved to the `Orders` table with status **Received**.
5. If processing fails 5 times in a row, the message moves to the **poison queue** and is handled by `ProcessOrderQueuePoison`.

---

## Table Design

The `Orders` table follows the brief's specification:

| Key | Value | Example |
|-----|-------|---------|
| **PartitionKey** | OrderDate (`yyyy-MM-dd`) | `2026-10-09` |
| **RowKey** | OrderId | `ORD-2026-8801` |

This groups all orders placed on the same day into a single partition, which is efficient for daily reporting and dashboard queries.

---

## Status Lifecycle

Orders progress through exactly four states:

| Status | Meaning |
|--------|---------|
| `Received` | The order has been created and stored. Initial state for every new order. |
| `Preparing` | Kitchen staff are actively preparing the order. |
| `Ready` | The order is complete and waiting for customer collection. |
| `Collected` | The customer has picked up their order. Final state. |

Status updates are applied via `PUT /api/orders/{orderId}/status`.

---

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/orders` | Retrieve all orders |
| `GET` | `/api/orders/{orderId}` | Retrieve a single order by its ID |
| `PUT` | `/api/orders/{orderId}/status` | Update an order's status |

### Example: Update Order Status

```http
PUT /api/orders/ORD-2026-8801/status
Content-Type: application/json

{
  "status": "Preparing"
}
```

**Response:** `200 OK` with the updated order object.

---

## JSON Deserialization Notes

Two important details for correctly reading the Producer's message:

1. **Base64 encoding** — Azure Queue Storage stores messages as Base64 by default. The `QueueMessage` binding decodes this automatically before deserialization, so no manual decoding is required.
2. **camelCase JSON** — The Producer sends camelCase field names (`orderId`, `customerName`, `selectedItemSKUs`, etc.). We set `PropertyNameCaseInsensitive = true` in `JsonSerializerOptions` so these map correctly to the PascalCase C# properties. Without this, all fields would be empty and every message would end up in the poison queue.

---

## Queue Triggers

### Main Queue: `order-processing-queue`
Fires `ProcessOrderQueue` when a message arrives. It:
1. Deserializes the message body into an `OrderMessage` DTO.
2. Validates that `OrderId`, `CustomerName`, and `SelectedItemSKUs` are present.
3. Maps the message to an `OrderEntity` (using `OrderTimestamp` for PartitionKey and `OrderId` for RowKey).
4. Saves it to the `Orders` table with status `Received`.

If any step fails, an exception is thrown. Azure Functions retries up to 5 times, then moves the message to the poison queue.

### Poison Queue: `order-processing-queue-poison`
Fires `ProcessOrderQueuePoison` for messages that repeatedly failed. It logs the failure details (`MessageId`, `DequeueCount`, `Body`) so the team can investigate and replay if needed.

---

## Validation Rules

- `OrderId`, `CustomerName`, and `SelectedItemSKUs` must not be empty.
- `SelectedItemSKUs` must contain at least one SKU.
- Status updates must be one of the four enum values (`Received`, `Preparing`, `Ready`, `Collected`).

Invalid messages throw an exception, triggering the poison-queue flow.

---

## Testing

A Postman collection is provided at:
```
docs/postman/CoffeeNChill - Order Processor.postman_collection.json
```

It includes:
- **Get All Orders**
- **Get Order by ID** (uses the `{{orderId}}` collection variable)
- **Update Order Status**

Import the collection into Postman, set the `orderId` variable to a valid order, and run the requests.

---

## Running Locally

1. Ensure **Azurite** is running (provides the storage emulator for queues and tables).
2. From the `CoffeeNChill.Functions` folder, run:
   ```
   func start
   ```
3. The queue trigger fires automatically when messages are added to `order-processing-queue`.

---

## Technology Stack

- **.NET 8** (Isolated worker model)
- **Azure Functions v4**
- **Azure Table Storage** (`Azure.Data.Tables`)
- **Azure Queue Storage** (`Azure.Storage.Queues`)
- **Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues** (queue trigger binding)
- **System.Text.Json** for message serialization