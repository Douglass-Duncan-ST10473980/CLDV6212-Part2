# CoffeeNChill
CoffeeNChill is a CLDV6212 group project that replaces paper-based menu management and locally stored staff documents with HTTP-triggered Azure Functions and Azure Storage. Part 1 demonstrates the Functions application and Azurite as independently started Docker containers without Docker Compose.

## Architecture overview

```text
Postman / Client
       |
       | HTTP requests
       v
Azure Functions API
       |
       +--> Menu Functions
       |        |
       |        v
       |   MenuStorageService
       |        |
       |        v
       |   Azure Table Storage
       |   Table: MenuItems
       |
       +--> Document Functions
                |
                v
          DocumentStorageService
                |
                v
          Azure File Share
          Share: staff-docs

Azurite container emulates Blob, Queue, and Table Storage locally.
```

### Main components

- **Menu HTTP Functions:** Create, list, filter, update, and delete menu items.
- **Document HTTP Functions:** Upload, list, and download staff documents.
- **MenuStorageService:** Keeps Table Storage operations separate from HTTP handling.
- **DocumentStorageService:** Keeps File Share operations separate from HTTP handling.
- **Postman suite:** Contains environment-driven requests, sample data, positive tests, negative tests, and cleanup requests.

## Required software

- Git
- Docker Desktop
- .NET 8 SDK
- JetBrains Rider, Visual Studio, or Visual Studio Code
- Postman Desktop

## Docker Hub repositories

The CoffeeNChill Functions application and Azurite images are published as version-tagged images on Docker Hub.

### CoffeeNChill Functions

- Repository: [st10473980/coffeenchill-functions](https://hub.docker.com/r/st10473980/coffeenchill-functions)
- Version: `1.0.0`

```bash
docker pull --platform linux/amd64 st10473980/coffeenchill-functions:1.0.0
```

### CoffeeNChill Azurite

- Repository: [st10473980/coffeenchill-azurite](https://hub.docker.com/r/st10473980/coffeenchill-azurite)
- Version: `1.0.0`

```bash
docker pull --platform linux/amd64 st10473980/coffeenchill-azurite:1.0.0
```

Both images are publicly accessible and use explicit version tags to support consistent, repeatable deployments.

Confirm that Docker Desktop is running before executing Docker commands.

## Part 1 startup guide: standalone containers

Part 1 uses individual `docker run` commands rather than Docker Compose. Start Docker Desktop, then run these commands in order.

### 1. Pull the Azurite image

```bash
docker pull --platform linux/amd64 st10473980/coffeenchill-azurite:1.0.0
```

### 2. Pull the Functions image

```bash
docker pull --platform linux/amd64 st10473980/coffeenchill-functions:1.0.0
```

### 3. Confirm both images exist

```bash
docker images
```

You should see:

```text
st10473980/coffeenchill-azurite
st10473980/coffeenchill-functions
```

### 4. Create the Azurite container

```bash
docker run -d --platform linux/amd64 \
  --name coffeenchill-azurite \
  -p 10000:10000 \
  -p 10001:10001 \
  -p 10002:10002 \
  st10473980/coffeenchill-azurite:1.0.0
```

### 5. Check that Azurite is running

```bash
docker ps
```

You should see `coffeenchill-azurite`.

### 6. Create the Functions container

```bash
docker run -d \
  --platform linux/amd64 \
  --name coffeenchill-functions \
  -p 7071:80 \
  -e FUNCTIONS_WORKER_RUNTIME="dotnet-isolated" \
  -e AzureWebJobsStorage="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://host.docker.internal:10000/devstoreaccount1;QueueEndpoint=http://host.docker.internal:10001/devstoreaccount1;TableEndpoint=http://host.docker.internal:10002/devstoreaccount1;" \
  -e FileShareStorage="<your-file-share-connection-string>" \
  st10473980/coffeenchill-functions:1.0.0
```

### 7. Confirm both containers are running

```bash
docker ps
```

You should see:

```text
coffeenchill-azurite
coffeenchill-functions
```

## API endpoints

### Menu

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/menu` | Create a menu item |
| GET | `/api/menu` | List all menu items |
| GET | `/api/menu/category/{category}` | Filter by category |
| GET | `/api/menu/item/{category}/{sku}` | Retrieve one item |
| PUT | `/api/menu/{category}/{sku}` | Update an item |
| DELETE | `/api/menu/{category}/{sku}` | Delete an item |

### Documents

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/documents/upload` | Upload using multipart form data |
| GET | `/api/documents` | List documents and metadata |
| GET | `/api/documents/download/{fileName}` | Stream a document download |

## Postman testing guide

Commit the exported assets under `/docs`:

```text
docs/
├── CoffeeNChill.postman_collection.json
├── CoffeeNChill.postman_environment.json
└── test-files/
    └── sample-document.pdf
```

### Run and export results

1. Open **CoffeeNChill API Tests** and click **Run**.
2. Select **CoffeeNChill Local**.
3. Run requests in their saved order: create, read/filter/update, negative scenarios, documents, and cleanup.
4. Confirm every saved automated test passes.
5. Open the completed run report.

##Test Results
<img width="1405" height="839" alt="image" src="https://github.com/user-attachments/assets/087314ae-c789-4c4c-9a44-e095731f4e97" />


## Team contributions

### Neha — ST10478910

- Created the menu-item entity model.
- Implemented the Azure Table Storage service.
- Implemented menu creation, retrieval, category filtering, updating, and deletion.
- Connected the functions to the `MenuItems` table.

### Tahir Ismail — ST10471483

- Created the staff-document metadata model.
- Implemented document upload, listing, and downloading.
- Implemented the `staff-docs` Azure File Share service.
- Added MIME-type validation and document endpoint tests.

### Douglass — ST10473980

- Led integration and coordinated the group's Part 1 work.
- Improved menu DTO validation and HTTP error handling.
- Resolved route, configuration, build, and integration issues.
- Created the structured Postman menu suite, negative tests, environment, and runner evidence.
- Assisted with Azurite, Docker, and local testing.

Each member must review this section and correct any inaccurate details before submission.

## Verified Git commit history
| Neha — ST10478910 | nehaheeralal | 22 | [Neha Commits](https://github.com/EMGPSD/cldv6212-g2-2026-poe-part1-douglass-duncan-st10473980/commits/main/?author=nehaheeralal) |
| Tahir — ST10471483 | tahir2404 | 14 | [Tahirs Commits](https://github.com/EMGPSD/cldv6212-g2-2026-poe-part1-douglass-duncan-st10473980/commits/main/?author=tahir2404) |
| Douglass — ST10473980 | Douglass-Duncan-ST10473980 | 7 | [Douglass' Commits](https://github.com/EMGPSD/cldv6212-g2-2026-poe-part1-douglass-duncan-st10473980/commits/main/?author=Douglass-Duncan-ST10473980) |

The POE requires at least five meaningful commits per student. GitHub's **Commits** and **Contributors** views are the source of truth.

Generate a local verification log:

```bash
git shortlog -sne --all
git log --all --date=short --pretty=format:"%h | %ad | %an | %s"
```

Record only verified results:

| Team member | GitHub username | Meaningful commits | Evidence |
|---|---|---:|---|
| Neha — ST10478910 | nehaheeralal | 11 | [Neha's commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=nehaheeralal) |
| Tahir — ST10471483 | tahir-ismail | 15 | [Tahir's commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=tahir-ismail) |
| Douglass — ST10473980 | Douglass-Duncan-ST10473980 | 11 | [Douglass' commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=Douglass-Duncan-ST10473980) |

Do not enter a count until it has been checked. Generic messages such as `fix` or `update` may not count as meaningful commits.

## Video walkthrough

Unlisted YouTube video: `<ADD_UNLISTED_YOUTUBE_URL>`



## Technologies

- C# and .NET 8
- Azure Functions isolated worker
- Azure Table Storage and Azure File Share
- Azurite
- Docker
- Postman
- Git and GitHub


---

# Part 2: Queue Triggers & Docker Compose

Part 2 adds asynchronous order processing. Instead of handling an order inside the HTTP request, `POST /api/orders/queue` validates the order, places it on an Azure Storage Queue and replies straight away with `202 Accepted`. A queue-triggered function then saves the order to an `Orders` table in the background, and staff move it through its lifecycle. The whole stack now starts with a single `docker-compose up` command.

## Changelog

### Added
- **Order queue producer:** `POST /api/orders/queue` validates the customer name and SKUs, calculates the total from the `MenuItems` table and places the order on `order-processing-queue`.
- **Queue processor:** `ProcessOrderQueue` triggers automatically for each queue message, parses the JSON and saves the order to the `Orders` table with status `Received`.
- **Poison-queue handling:** messages that fail five times move to `order-processing-queue-poison`, where `ProcessOrderQueuePoison` logs them.
- **Order tracking endpoints:** `GET /api/orders`, `GET /api/orders/{orderId}`, `PUT /api/orders/status` (move one step forward) and `PUT /api/orders/{orderId}/status` (set a specific status).
- **Order lifecycle:** Received → Preparing → Ready → Collected.
- **Docker Compose:** `docker-compose.yml` in the repository root orchestrates Azurite and the Functions host, pulls `tahirismail/coffeenchill-functions:v2.0` from Docker Hub, and uses a custom bridge network and a persistent Azurite volume.
- **`.env.example`:** documents the `FileShareStorage` setting so secrets stay out of the repository.
- **Postman:** a `04 - Orders` folder covering the full order flow, and order validation tests in `03 - Negative Tests`.

### Changed
- Postman menu requests now use the `GET /api/menu/category/{category}` and `GET /api/menu/item/{category}/{sku}` routes.
- The Postman `Clean Up` folder is now `05 - Clean Up`.
- The real `FileShareStorage` key was removed from this README and replaced with a placeholder. Secrets are supplied through `.env` (Docker) or `local.settings.json` (local development), both excluded by `.gitignore`.

## Part 2 architecture

```text
Postman / Client
       |
       | POST /api/orders/queue
       v
QueueOrder  ----reads prices---->  Table: MenuItems
       |
       | JSON message (202 Accepted returned to the client)
       v
Queue: order-processing-queue
       |
       | triggers automatically
       v
ProcessOrderQueue  ------------->  Table: Orders  (Status: Received)
       |                                 ^
       | fails 5 times                   | PUT /api/orders/status
       v                                 | Received → Preparing → Ready → Collected
Queue: order-processing-queue-poison     |
       |                           Order Status Functions
       v                           (GET /api/orders, GET /api/orders/{orderId})
ProcessOrderQueuePoison (logs the failed message)
```

### Why a queue?

Processing orders inside the HTTP request means a busy period or a storage problem makes the customer wait, or fail. With a queue, the customer gets an immediate reply, and the order is processed in the background. If the processor is offline, orders wait safely in the queue and are processed as soon as it starts again.

### Orders table design

| Property | Example | Notes |
|---|---|---|
| PartitionKey | `2026-10-10` | Order date (`yyyy-MM-dd`), groups each day's orders |
| RowKey | `ORD-2026-1286` | Order ID generated by the producer |
| CustomerName | `Jane Smith` | |
| SelectedItemSKUs | `COF-001,CLD-001` | Stored as a comma-separated string |
| TotalPrice | `87.49` | Calculated by the server from menu prices |
| Status | `Received` | Received → Preparing → Ready → Collected |

## Part 2 Docker Hub image

- Repository: [tahirismail/coffeenchill-functions](https://hub.docker.com/r/tahirismail/coffeenchill-functions)
- Version: `v2.0`

Part 2 uses Microsoft's official `mcr.microsoft.com/azure-storage/azurite` image in Docker Compose, started with `--skipApiVersionCheck` (see Troubleshooting).

## Part 2 startup guide: Docker Compose (single command)

1. Make sure Docker Desktop is running and shows **Engine running**.
2. In the repository root, copy `.env.example` to `.env` and fill in the `FileShareStorage` connection string. Ask a team member for it privately. **Never commit `.env`.**
3. Start the full stack:

   ```bash
   docker-compose up
   ```

   Docker pulls `tahirismail/coffeenchill-functions:v2.0` from Docker Hub, starts Azurite, and connects both containers on the `coffeenchill-network` bridge network.
4. Set Postman's `baseUrl` to `http://localhost:7071/api`.
5. Stop everything:

   ```bash
   docker-compose down
   ```

Azurite's data is kept in the `azurite-data` volume, so tables and queues survive restarts. Use `docker-compose down -v` to wipe it.

### Local development (Rider / Visual Studio)

Use this to run code that hasn't been published to Docker Hub yet.

1. Start only Azurite: `docker-compose up -d azurite`
2. Create `CoffeeNChill.Functions/local.settings.json` (excluded by `.gitignore`):

   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "FileShareStorage": "<your-file-share-connection-string>"
     }
   }
   ```

3. Run the `CoffeeNChill.Functions` project. Rider uses `http://localhost:7055/api`.

## Part 2 API endpoints

### Orders

| Method / Trigger | Route / Queue | Purpose |
|---|---|---|
| POST | `/api/orders/queue` | Validate an order and place it on the queue (`202 Accepted`) |
| Queue trigger | `order-processing-queue` | `ProcessOrderQueue` saves the order to the `Orders` table |
| Queue trigger | `order-processing-queue-poison` | `ProcessOrderQueuePoison` logs failed messages |
| GET | `/api/orders` | List all orders |
| GET | `/api/orders/{orderId}` | Get one order and its status |
| PUT | `/api/orders/status` | Move an order one step forward. Body: `{ "orderId": "ORD-2026-1286" }` |
| PUT | `/api/orders/{orderId}/status` | Set a specific status. Body: `{ "status": "Ready" }` |

### Health

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/health` | Confirms the Functions host is running |

Sample order request:

```json
{
  "customerName": "Jane Smith",
  "selectedItemSKUs": ["COF-001", "CLD-001"]
}
```

The server generates the order ID and timestamp and calculates the total from the `MenuItems` table, so customers cannot set their own price.

## Part 2 Postman testing guide

The collection is `docs/postman/CoffeeNchill API Tests.postman_collection.json`. Run the folders in order:

1. `01 - Menu`: creates the drinks that orders need
2. `02 - Documents`: re-attach `Test File.pdf` to the upload request first
3. `03 - Negative Tests`: menu, document and order validation errors
4. `04 - Orders`: places orders, waits for the queue trigger, then checks each status change from Received to Collected
5. `05 - Clean Up`: deletes the test drinks

Send the document upload request on its own, because the Collection Runner does not attach local files reliably.

### Checking the Orders table

Open Azure Storage Explorer → **Emulator & Attached → Storage Accounts → (Emulator - Default Ports) → Tables → Orders**. Each order appears with its current status.

### Part 2 test results

Full collection run (all requests except the file upload):

<img width="1581" height="1032" alt="Part 2 Postman collection run" src="https://github.com/user-attachments/assets/1c81b447-ec2d-4068-ad5f-101554c9b6da" />

Upload Staff Document sent on its own:

<img width="1578" height="1034" alt="Upload Staff Document test" src="https://github.com/user-attachments/assets/39ee115b-b4b4-47fb-8eb0-57929c91b70a" />

## Troubleshooting

**`POST /api/orders/queue` returns 503 "The API version 2026-06-06 is not supported by Azurite"**
The queue client uses a newer storage API version than Azurite supports. Our Compose file starts Azurite with `--skipApiVersionCheck`, which fixes this. If you still see it, another Azurite is answering on `localhost`. JetBrains Rider's Azure Toolkit can start an npm-installed Azurite (`node.exe`) on `127.0.0.1`, which takes priority over Docker's.

- Check with `netstat -ano | findstr :10001`, then `tasklist /fi "PID eq <pid>"`. Only `com.docker.backend.exe` and `wslrelay.exe` should be listening.
- Fix: `npm uninstall -g azurite`, and use the Azurite from `docker-compose.yml`.

**Containers can't reach storage**
`UseDevelopmentStorage=true` points at the container itself, not at Azurite. The Compose file uses a full connection string that points at the `azurite` service name instead.

**"failed to connect to the docker API"**
Docker Desktop isn't running. Start it and wait for **Engine running**.

**Create Drink returns 400 when re-running the collection**
The drinks already exist from a previous run. Run `05 - Clean Up` first.

## Part 2 team contributions

### Tahir Ismail — ST10471483 (Docker Compose and integration)

- Created the root `docker-compose.yml` (Azurite and Functions host, custom bridge network, Azurite data volume, Docker Hub `v2.0` image).
- Added `.env.example` and kept secrets out of the repository.
- Integrated the producer and processor branches and resolved merge conflicts.
- Diagnosed the Azurite API-version issue caused by a hidden local Azurite.
- Updated the Postman collection with the order flow, status lifecycle and order validation tests.
- Updated the README and coordinated the video.

### Douglass — ST10473980 (Order queue producer)

- Created the order request model and the `POST /api/orders/queue` endpoint.
- Validated order fields and calculated totals from the `MenuItems` table.
- Serialised orders to JSON and sent them to `order-processing-queue`.

### Neha — ST10478910 (Queue processor and order tracking)

- Created the `OrderEntity` table entity and `OrderStorageService`.
- Implemented the `ProcessOrderQueue` queue-triggered function and poison-queue handling.
- Implemented the order status endpoints and the Received → Preparing → Ready → Collected lifecycle.

Each member must review this section and correct any inaccurate details before submission.

## Part 2 verified Git commit history

| Team member | GitHub username | Part 2 meaningful commits | Evidence |
| Neha — ST10478910 | nehaheeralal | 11 | [Neha's commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=nehaheeralal) |
| Tahir — ST10471483 | tahir-ismail | 15 | [Tahir's commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=tahir-ismail) |
| Douglass — ST10473980 | Douglass-Duncan-ST10473980 | 11 | [Douglass' commits](https://github.com/Douglass-Duncan-ST10473980/CLDV6212-Part2/commits/main/?author=Douglass-Duncan-ST10473980) |

## Part 2 video walkthrough

Unlisted YouTube video: `<ADD_PART_2_UNLISTED_YOUTUBE_URL>`

## Part 2 technologies

- Azure Queue Storage and Queue-triggered Azure Functions
- Docker Compose
- Azure Storage Explorer
