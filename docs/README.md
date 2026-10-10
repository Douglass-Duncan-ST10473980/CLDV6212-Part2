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
  -e FileShareStorage="DefaultEndpointsProtocol=https;AccountName=coffeenchillst10473980;AccountKey=M44368nKyIrwCod6MKE+Ut8kkM4xJFW1Zf/YZPcIJx2eYnbxuBzCArxG5N6zrv/YZAUC0v8bIlMb+AStP86F9A==;EndpointSuffix=core.windows.net" \
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

