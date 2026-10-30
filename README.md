# AssetDesk

An IT equipment tracker: check laptops, monitors and phones out to employees, track due dates and maintenance, and attach receipts and warranty documents.

**C# / .NET 8 · ASP.NET Core Web API · EF Core · MySQL · JWT auth · React + TypeScript · Amazon S3 · Docker · GitHub Actions · Azure DevOps**

## What it does

- **Employees** browse and search assets, check available ones out to themselves, and return what they hold ("My assets").
- **Admins** also create/edit/delete assets, assign them to anyone, send them to maintenance, retire them, upload attachments, and see a dashboard with overdue returns.
- Concurrent edits are detected: every asset has a `Version`, and saving a stale copy returns **409**; the UI explains the conflict and offers to reload.

## Architecture

```
src/
  AssetDesk.Domain          Entities + business rules (asset lifecycle, assignments)
  AssetDesk.Application     Business logic layer: services, DTOs, repository/storage abstractions
  AssetDesk.Infrastructure  EF Core (MySQL), migrations, PBKDF2 hashing, JWT, file stores (local / S3)
  AssetDesk.Api             ASP.NET Core controllers, auth, rate limiting; also serves the React build
tests/AssetDesk.Tests       xUnit unit + WebApplicationFactory integration tests
web/                        React + TypeScript (Vite) single-page app
infra/aws-storage.yaml      CloudFormation: S3 bucket + IAM role
.github/workflows/ci.yml    GitHub Actions
azure-pipelines.yml         Azure DevOps pipeline
```

The React UI talks to the business logic layer only through the REST API. Services depend on interfaces
(`IAssetRepository`, `IFileStore`, `IClock`, ...), so they are unit-tested without a database.

### Business rules
- Status moves `Available <-> CheckedOut`, `Available <-> InMaintenance`, and an asset can be retired from `Available` or `InMaintenance`; `Retired` is final and read-only.
- Only available assets can be checked out; only checked-out assets can be returned; a checked-out asset can't be retired.
- Assets with any assignment history can't be deleted (retire them instead).
- Due dates must be in the future and at most 90 days out.
- Employees can only check out to, and return, their own assets (enforced server-side, not just hidden in the UI).

Domain exceptions map to RFC 7807 problem responses (`404`, `409`, `403`, validation `400`).

### Security
- Passwords hashed with PBKDF2 (per-user salt); JWT bearer tokens with `Admin` / `Employee` roles.
- Login and register are rate limited (429 after repeated attempts).
- `Jwt:Key` has no default in production config; the app refuses to start without a key of at least 32 characters.
- Uploads are limited to PDF / PNG / JPEG / text, max 5 MB.

## Run it

**Quickest (no database)**, with the UI built once:
```bash
cd web && npm ci && npm run build && cd ..
Database__Provider=InMemory dotnet run --project src/AssetDesk.Api
```
PowerShell: `$env:Database__Provider='InMemory'; dotnet run --project src/AssetDesk.Api`

Open http://localhost:5090 (see `launchSettings.json` for the port). `/swagger` has the API docs, `/health` the health check.

**Front-end development** (hot reload, proxies `/api` to the API on port 5090): `cd web && npm run dev`.

**Docker (API + MySQL):**
```bash
cp .env.example .env     # set JWT_KEY to a random 32+ character string
docker compose up --build   # http://localhost:8080
```

Demo accounts (seeded when `Database:Seed` is true, the default):

| Role | Email | Password |
|---|---|---|
| Admin | `admin@assetdesk.local` | `Admin#12345` (override with `Seed__AdminPassword`) |
| Employee | `sara@assetdesk.local` / `omar@assetdesk.local` | `Employee#123` |

**Set `Database__Seed=false` and a real admin password for anything other than a demo.**

## Configuration

| Setting | Purpose |
|---|---|
| `Database:Provider` | `MySql` (default) or `InMemory` |
| `ConnectionStrings:Default` | MySQL connection string |
| `Jwt:Key` | Signing key, 32+ characters (required) |
| `Storage:Provider` | `Local` (default, `Storage:LocalPath`) or `S3` |
| `Storage:Bucket`, `Storage:Region` | S3 bucket and region; credentials come from the default AWS chain (IAM role) |
| `Storage:ServiceUrl` | Optional custom endpoint (MinIO / LocalStack) |

Migrations are applied on startup (`Database.MigrateAsync`). Add one with
`dotnet tool restore && dotnet ef migrations add <Name> -p src/AssetDesk.Infrastructure -s src/AssetDesk.Api`.

## API overview

| Method | Route | Who |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | anyone (rate limited) |
| GET | `/api/auth/users` | admin |
| GET | `/api/assets?search=&status=&category=&page=&pageSize=` | signed in |
| GET | `/api/assets/{id}` · `/mine` · `/stats` | signed in |
| GET | `/api/assets/overdue` | admin |
| POST/PUT/DELETE | `/api/assets`, `/api/assets/{id}` | admin |
| POST | `/api/assets/{id}/checkout` · `/checkin` | signed in (employees: own assets only) |
| POST | `/api/assets/{id}/maintenance` · `/maintenance/complete` · `/retire` | admin |
| POST/GET/DELETE | `/api/assets/{id}/attachments[/{attachmentId}]` | upload/delete: admin, download: signed in |

## Tests

```bash
dotnet test                       # 52 API tests (domain, services, HTTP pipeline) on InMemory
cd web && npm test                # 8 React tests (routing guard, login, filters, conflict handling, check-out)
```

To run the API tests against a real MySQL instance:
```bash
ASSETDESK_TEST_MYSQL='Server=127.0.0.1;Port=3306;Database=assetdesk_test;User=root;Password=...' dotnet test
```
The suite was run this way against MySQL 8.0 while building the project, and CI does the same with a MySQL service container.

## CI/CD

- [`.github/workflows/ci.yml`](.github/workflows/ci.yml): API tests, API tests against a MySQL service, React tests + build, Docker image build.
- [`azure-pipelines.yml`](azure-pipelines.yml): Azure DevOps pipeline that tests, builds the React app, publishes the API and an EF Core migration bundle, and deploys to an App Service behind an environment approval. Variables: `azureServiceConnection`, `webAppName`, `mysqlConnectionString` (secret).

## AWS

[`infra/aws-storage.yaml`](infra/aws-storage.yaml) creates a private, encrypted, versioned S3 bucket and an IAM role limited to that bucket:
```bash
aws cloudformation deploy --stack-name assetdesk-storage --template-file infra/aws-storage.yaml \
  --parameter-overrides BucketName=assetdesk-attachments-<account-id> --capabilities CAPABILITY_IAM
```
Then run the API with `Storage__Provider=S3`, `Storage__Bucket=<name>`, `Storage__Region=<region>`.

## Verification status

Honest notes on what has and has not been exercised:

- **Verified:** build with warnings as errors; 52 API tests on InMemory and on a real MySQL 8.0 server; the EF migration applied to MySQL; manual API checks against MySQL (roles, 409/404, rate limit); 8 React tests; production build served by the API (SPA, deep links, `/api` 404s).
- **Not verified:** the Docker image and compose file (no Docker available when this was written), the GitHub Actions and Azure DevOps pipelines, the CloudFormation template (only linted), the S3 file store against real AWS, and any live deployment. Nothing here has been deployed to Azure or AWS.
