# CoffeeNChill Canteen Management System

## Part 2: Queue Triggers and Docker Compose Orchestration

CoffeeNChill is a cloud-enabled canteen management system developed for the CLDV6212 Portfolio of Evidence.

Part 2 introduces asynchronous order processing. Orders are validated by an HTTP producer, placed in Azure Queue Storage, processed by a Queue-triggered Azure Function, and stored in the `Orders` Azure Table. Docker Compose starts the Azure Functions host and Azurite together.

## Team Members

| Team member | Student number | Primary responsibility |
|---|---|---|
| Tahir Ismail | ST10471483 | Docker Compose and integration |
| Douglass | ST10473980 | Order queue producer |
| Neha Heeralal | ST10478910 | Queue processor and order tracking |

## Part 2 Architecture

```text
Postman / Client
       |
       | POST /api/orders/queue
       v
Order Queue Producer
       |
       | Validated JSON message
       v
order-processing-queue
       |
       | Queue trigger
       v
ProcessOrderQueue
       |
       v
Orders Azure Table
       |
       | PUT /api/orders/{orderId}/status
       v
Received -> Preparing -> Ready -> Collected

Repeated processing failure
       |
       v
order-processing-queue-poison
```

## Main Components

| Component | Purpose |
|---|---|
| `OrderRequest` | Represents the order submitted to the producer endpoint |
| `OrderMessage` | Represents the JSON message placed in the queue |
| `OrderEntity` | Represents an order stored in the `Orders` Azure Table |
| `OrderStatus` | Defines `Received`, `Preparing`, `Ready`, and `Collected` |
| Order queue producer | Validates orders, calculates totals, and sends queue messages |
| `OrderStorageService` | Handles order creation, retrieval, and status updates |
| `ProcessOrderQueue` | Processes messages from `order-processing-queue` |
| `ProcessOrderQueuePoison` | Logs messages that repeatedly fail processing |
| Order status functions | Retrieve orders and update their lifecycle status |
| `docker-compose.yml` | Runs Azurite and the Azure Functions host together |

> Replace component names with the exact filenames used in the final repository.

## Order Queue Producer

The producer exposes:

```http
POST /api/orders/queue
```

It performs the following operations:

1. Validates the order ID and customer name.
2. Confirms that at least one item SKU was supplied.
3. Retrieves the selected items from the `MenuItems` table.
4. Calculates the total using the stored menu prices.
5. Generates or validates the order timestamp.
6. Serialises the validated order into JSON.
7. Places the message in `order-processing-queue`.

Example request:

```json
{
  "customerName": "Jane Smith",
  "selectedItemSKUs": [
    "COF-001",
    "PAS-104"
  ]
}
```

## Queue Processor

`ProcessOrderQueue` runs automatically when a message arrives in `order-processing-queue`.

The function:

1. Reads the queue message.
2. Deserialises the JSON into an `OrderMessage`.
3. Validates the required fields.
4. Maps the message to an `OrderEntity`.
5. Uses the order date as the `PartitionKey`.
6. Uses the order ID as the `RowKey`.
7. Saves the order with the initial status `Received`.
8. Records processing information in the Functions logs.

If processing fails, the function throws an exception so that Azure Functions can retry the message.

## Queue Message Format

The producer and processor share the following information:

| Property | Description |
|---|---|
| `OrderId` | Unique order identifier |
| `CustomerName` | Name of the customer |
| `SelectedItemSKUs` | Identifiers of the ordered menu items |
| `TotalPrice` | Total calculated by the producer |
| `OrderTimestamp` | Time at which the order was submitted |

The producer sends camel-case JSON. The processor uses case-insensitive JSON deserialisation so that fields such as `orderId` map correctly to C# properties such as `OrderId`.

The producer and processor must also use compatible queue-message encoding. Docker and local configurations should use the same encoding setting.

## Orders Table Design

| Property | Value or purpose | Example |
|---|---|---|
| `PartitionKey` | Order date in `yyyy-MM-dd` format | `2026-10-12` |
| `RowKey` | Unique order ID | `ORD-2026-8801` |
| `OrderId` | Public order identifier | `ORD-2026-8801` |
| `CustomerName` | Customer who submitted the order | `Jane Smith` |
| `SelectedItemSKUs` | Selected menu-item identifiers | `COF-001,PAS-104` |
| `TotalPrice` | Calculated order total | `65.00` |
| `Status` | Current lifecycle status | `Received` |
| `OrderTimestamp` | Original submission time | `2026-10-12T10:15:00Z` |
| `ReceivedAt` | Time processing began | `<UTC date and time>` |
| `LastUpdatedAt` | Time of the latest update | `<UTC date and time>` |

Using the order date as the partition key groups orders by day, while the order ID uniquely identifies each order within that partition.

## Order Lifecycle

Orders progress through the following sequence:

```text
Received -> Preparing -> Ready -> Collected
```

| Status | Meaning |
|---|---|
| `Received` | The order was processed from the queue and stored |
| `Preparing` | Staff are preparing the order |
| `Ready` | The order is complete and waiting for collection |
| `Collected` | The customer has collected the order |

The status endpoints must reject invalid status values and invalid transitions.

## Order Endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/orders/queue` | Validate and place an order in the queue |
| `GET` | `/api/orders` | Retrieve all stored orders |
| `GET` | `/api/orders/{orderId}` | Retrieve a particular order |
| `PUT` | `/api/orders/{orderId}/status` | Advance or update an order’s status |

> Confirm these routes against the final function attributes before submission.

Example status request:

```http
PUT /api/orders/ORD-2026-8801/status
Content-Type: application/json
```

```json
{
  "status": "Preparing"
}
```

A successful request should return `200 OK` with the updated order. A missing order should return `404 Not Found`, while an invalid status request should return `400 Bad Request`.

## Validation and Failure Handling

The implementation validates:

- `OrderId` is present.
- `CustomerName` is present.
- At least one SKU is supplied.
- Blank or unknown SKUs are rejected.
- The referenced menu items exist.
- The total price can be calculated.
- The order timestamp is valid.
- Status values are supported.
- Status transitions follow the correct sequence.

A message that repeatedly fails processing is moved to:

```text
order-processing-queue-poison
```

`ProcessOrderQueuePoison` records details such as the message ID, dequeue count, and relevant failure information so that the team can investigate the failure.

## Running with Docker Compose

Start Docker Desktop and wait until it reports that the engine is running.

From the repository root, run:

```bash
docker compose up
```

This starts:

- Azurite
- The Azure Functions host
- The shared Docker network
- Persistent Azurite storage

The Functions service must use the published Docker Hub image:

```yaml
image: <dockerhub-username>/coffeenchill-functions:v2.0
```

To stop the application:

```bash
docker compose down
```

## Part 2 Postman Testing Guide

The Postman collection is located at:

```text
docs/postman/CoffeeNchill API Tests.postman_collection.json
```

Run its folders in this order:

1. `01 - Menu`: Creates the drinks required by the order tests.
2. `02 - Documents`: Tests staff-document functionality.
3. `03 - Negative Tests`: Tests menu, document, and order validation.
4. `04 - Orders`: Places orders and tests every status from `Received` to `Collected`.
5. `05 - Clean Up`: Deletes the temporary drinks created during testing.

### Document Upload

Reattach `Test File.pdf` before sending the document-upload request.

Send this request separately because the Postman Collection Runner does not reliably attach local files.

## Checking the Orders Table

In Azure Storage Explorer, open:

```text
Emulator & Attached
└── Storage Accounts
    └── (Emulator - Default Ports)
        └── Tables
            └── Orders
```

Confirm that:

- The order was created.
- Its customer and item information is correct.
- Its calculated total is correct.
- Its initial status is `Received`.
- Its status advances to `Preparing`, `Ready`, and `Collected`.
- `LastUpdatedAt` changes after each update.

## Part 2 Test Results

### Full Collection Run

The following collection run includes every request except the local file upload:

<img width="1581" height="1032" alt="Part 2 Postman collection run" src="https://github.com/user-attachments/assets/1c81b447-ec2d-4068-ad5f-101554c9b6da" />

### Staff-Document Upload

The upload request was sent separately after `Test File.pdf` was attached:

<img width="1578" height="1034" alt="Upload Staff Document test" src="https://github.com/user-attachments/assets/39ee115b-b4b4-47fb-8eb0-57929c91b70a" />

## Troubleshooting

### Unsupported Azurite API Version

If `POST /api/orders/queue` returns:

```text
The API version 2026-06-06 is not supported by Azurite
```

the queue client is using a newer Storage API version than Azurite supports.

The Compose configuration starts Azurite with:

```text
--skipApiVersionCheck
```

If the error remains, another Azurite instance may already be listening on the local storage ports. JetBrains Rider's Azure Toolkit can start a globally installed npm version of Azurite.

On Windows, inspect port `10001`:

```powershell
netstat -ano | findstr :10001
tasklist /fi "PID eq <pid>"
```

If a global npm installation is responsible, remove it:

```bash
npm uninstall -g azurite
```

Then restart the Azurite service through Docker Compose.

### Containers Cannot Reach Azurite

Inside a container, `UseDevelopmentStorage=true` points to that same container—not to the Azurite container.

The Compose configuration therefore uses a complete connection string containing the `azurite` service name.

### Docker API Connection Failure

If Docker reports that it failed to connect to the Docker API:

1. Start Docker Desktop.
2. Wait until it displays `Engine running`.
3. Run `docker compose up` again.

### Create Drink Returns 400

The test drinks probably remain from an earlier collection run.

Run `05 - Clean Up`, and then restart the collection with `01 - Menu`.

## Part 2 Team Contributions

### Tahir Ismail — ST10471483

**Docker Compose and integration**

- Created the root `docker-compose.yml`.
- Configured Azurite and the Functions host.
- Added the custom bridge network and persistent Azurite volume.
- Configured the Docker Hub `v2.0` image.
- Added `.env.example` and protected secrets.
- Integrated the producer and processor branches.
- Resolved integration conflicts.
- Diagnosed the Azurite API-version issue.
- Updated the Postman collection and README.
- Coordinated the demonstration video.

### Douglass — ST10473980

**Order queue producer**

- Created the order-request model.
- Created `POST /api/orders/queue`.
- Implemented order validation.
- Retrieved item information from the `MenuItems` table.
- Calculated order totals from stored menu prices.
- Serialised valid orders into JSON.
- Sent messages to `order-processing-queue`.
- Added producer error handling.

### Neha Heeralal — ST10478910

**Queue processor and order tracking**

- Created `OrderEntity` and `OrderStatus`.
- Created `OrderStorageService`.
- Implemented `ProcessOrderQueue`.
- Implemented poison-queue handling.
- Created the order retrieval and status endpoints.
- Implemented the lifecycle from `Received` to `Collected`.
- Added queue-processing and status-update logging.

Each team member must confirm that their listed contributions are accurate before submission.

## Verified Git Commit History

| Team member | GitHub username | Non-merge commits | Evidence |
|---|---|---:|---|
| Neha — ST10478910 | `nehaheeralal` | 11 | [Neha's commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=nehaheeralal) |
| Tahir — ST10471483 | `tahir-ismail` | 32 | [Tahir's commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=tahir-ismail) |
| Douglass — ST10473980 | `Douglass-Duncan-ST10473980` | 19 | [Douglass' commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=Douglass-Duncan-ST10473980) |

## Part 2 Video Walkthrough

Unlisted YouTube video:

```text
https://youtu.be/msGuSvtPuLs
```

The video demonstrates:

- `docker compose up`
- The Docker Hub image being pulled
- Both containers running
- Order submission through Postman
- Queue-triggered processing
- The stored order in Azure Storage Explorer
- Status updates from `Received` to `Collected`
- Postman test results
- Each team member’s contribution

## Part 2 Changelog

### Version 2.0

#### Added

- Azure Queue producer.
- Queue-triggered order processor.
- `Orders` Azure Table integration.
- Complete order-status lifecycle.
- Poison-queue handling.
- Docker Compose orchestration.
- Docker Hub image deployment.
- Updated Postman collection.
- Negative tests and clean-up requests.
- Part 2 documentation and video evidence.

#### Fixed

- Azurite API-version compatibility.
- Container-to-container storage connections.
- Repeatable Postman runs through test-data clean-up.

## Authors

- Tahir Ismail — ST10471483
- Douglass — ST10473980
- Neha Heeralal — ST10478910
