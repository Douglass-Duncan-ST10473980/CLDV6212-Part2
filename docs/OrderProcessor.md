# Order Queue Processor — CoffeeNChill

## Overview

The **Order Queue Processor** is a queue-triggered Azure Function that handles incoming orders from the `order-processing-queue`. It validates the JSON payload, persists orders to the Azure `Orders` table, and manages their lifecycle through four statuses.

**Author:** Neha

---

## Architecture

The processor is made up of four key components:

| File | Purpose |
|------|---------|
| `Models/Order.cs` | Entity stored in the Azure `Orders` table. Uses `PartitionKey = "Order"` and `RowKey = Guid` as the composite key. |
| `Models/OrderStatus.cs` | Enum defining the four lifecycle stages: `Received`, `Preparing`, `Ready`, `Collected`. |
| `Services/OrderStorageService.cs` | Data access layer. Handles create, read, and status update operations against Table Storage. |
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
  order-processing-queue
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

1. The Producer places a JSON message on the `order-processing-queue`.
2. `ProcessOrderQueue` fires automatically, deserializes the JSON, and validates the order.
3. If valid, the order is saved to the `Orders` table with status **Received**.
4. If processing fails 5 times in a row, the message is moved to the **poison queue** and picked up by `ProcessOrderQueuePoison`.

---

## Status Lifecycle

Orders progress through exactly four states:

| Status | Meaning |
|--------|---------|
| `Received` | The order has just been created and stored in the database. This is the initial state. |
| `Preparing` | Kitchen staff are actively preparing the order. |
| `Ready` | The order is complete and waiting for the customer to collect it. |
| `Collected` | The customer has picked up their order. This is the final state. |

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
PUT /api/orders/{orderId}/status
Content-Type: application/json

{
  "status": "Preparing"
}
```

**Response:** `200 OK` with the updated order object.

---

## Queue Triggers

### Main Queue: `order-processing-queue`
Fires `ProcessOrderQueue` automatically when a message arrives. It:
1. Deserializes the message body into an `OrderRequest` DTO.
2. Validates that `CustomerName`, `CustomerPhone`, and `Items` are present.
3. Maps the request to an `Order` entity with a generated `OrderId`.
4. Saves it to the `Orders` table with status `Received`.

If any step fails, an exception is thrown, causing Azure Functions to retry the message. After 5 failed attempts, the message is automatically moved to the poison queue.

### Poison Queue: `order-processing-queue-poison`
Fires `ProcessOrderQueuePoison` for messages that repeatedly failed. It logs the failure details (`MessageId`, `DequeueCount`, `Body`) so the team can investigate and manually replay if needed.

---

## Validation Rules

- `CustomerName` and `CustomerPhone` must not be empty.
- `Items` must contain at least one item.
- `Status` must be one of the four enum values.

Invalid messages are rejected and thrown as exceptions, which triggers the poison-queue flow.

---

## Testing

A Postman collection is provided in `docs/postman/CoffeeNChill - Order Processor.postman_collection.json` and includes:
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
3. The queue trigger will automatically fire when messages are added to `order-processing-queue`.

---

## Technology Stack

- **.NET 8** (Isolated worker model)
- **Azure Functions v4**
- **Azure Table Storage** (`Azure.Data.Tables`)
- **Azure Queue Storage** (`Azure.Storage.Queues`)
- **Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues** (queue trigger binding)
- **System.Text.Json** for message serialization