# TIH Sentence Builder

## Live demo

**[Open the application](https://jolly-smoke-062fc2203.1.azurestaticapps.net)**

Angular 21 on Azure Static Web Apps Free, ASP.NET Core 10 on Windows Azure App Service Free F1, and Azure SQL Database. Vocabulary retrieval, sentence creation, editing and SQL persistence have been verified in Azure. This is functional assessment evidence, not a production-readiness or exhaustive-testing claim.

## Features and architecture

- Browse nine database-backed word categories; tap/click or drag reusable words into a sentence.
- Reorder/remove individual occurrences, including repeated words, using drag handles or keyboard-accessible buttons.
- Save, page through previous sentences, recall and edit them. Historical word text follows the agreed per-WordId FIFO snapshot policy.
- Loading/error states, discard confirmation and failed-save draft preservation; responsive desktop/mobile layouts.

```text
Angular 21 SPA -> ASP.NET Core 10 REST API -> EF Core 10 -> SQL Server
```

Angular handles the UI and draft, calling the API through HttpClient. API controllers handle HTTP; services validate selections and build sentence text; repositories use EF Core to read/write SQL Server. Only the backend accesses the database. One SaveChangesAsync commits the sentence and its words together; earlier reads are outside that transaction.

WordType and Word hold vocabulary. SentenceWord stores order and historical text; Sentence stores the assembled text for listing. The solution contains one API project, one xUnit project and an Angular standalone/strict TypeScript frontend with CDK drag-and-drop.

**Choose a local route:** [Docker](#docker-quick-start) includes SQL Server; [manual Windows development](#manual-windows-development) uses an existing local SQL Server. All commands start in the checkout directory containing SentenceBuilder.slnx unless stated otherwise. Run commands one at a time and stop on any failure.

## Docker quick-start

Requires Docker with Compose v2, Linux AMD64/x86-64 container support, registry access and sufficient SQL Server memory (at least 2 GB plus application/build overhead). SQL Server Developer licensing is for non-production use. Host .NET/Node installations are not required to build these images; they are needed for the host-run tests below.

Local execution was verified on Windows with Docker Desktop 4.96.0, Engine 29.9.1 and WSL 2: images built, SQL became healthy, explicit initialization exited 0, and creation/retrieval/editing worked with persistence after refresh and stop/start. Other operating systems, ARM, automated container testing and persistence after container recreation were not verified.

### Supply local secrets

Compose requires two variables. Enter one local password below and derive the connection string from it, or supply both through a private ignored .env at the solution root. Neither has a default:

| Variable | Required value |
| --- | --- |
| MSSQL_SA_PASSWORD | Strong local SQL administrator password meeting SQL Server policy |
| TIH_CONTAINER_CONNECTION | SQL-auth connection to Server=sql and Database=TIH_SentenceBuilder_Dev, using the configured sa password, Encrypt=True and TrustServerCertificate=True for this local stack |

From the solution root, enter a strong local-only password once (PowerShell). The prompt keeps the literal password out of command history:

```powershell
$env:MSSQL_SA_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Local SQL administrator password' -AsSecureString)).Password
if ($env:MSSQL_SA_PASSWORD -match '[;''"]' -or $env:MSSQL_SA_PASSWORD -ne $env:MSSQL_SA_PASSWORD.Trim()) {
    throw 'For this shortcut, use a password without semicolons, quotes or surrounding whitespace.'
}
$env:TIH_CONTAINER_CONNECTION = "Server=sql;Database=TIH_SentenceBuilder_Dev;User ID=sa;Password=$($env:MSSQL_SA_PASSWORD);Encrypt=True;TrustServerCertificate=True"
```

The shortcut rejects semicolons, quotes and surrounding whitespace rather than escaping them. To use such a password, construct TIH_CONTAINER_CONNECTION with a SQL connection-string builder and the same password instead. Do not print, commit or log either value; environment/Compose inspection can expose them. Initialization/health checks use SQLCMDPASSWORD, not a password command-line argument. Shared/production use needs a least-privilege runtime login and proper secret management. Changing the variable does not rotate a login already stored in the SQL volume.

### Build and initialize explicitly

Review [compose.yaml](compose.yaml), [initialization job](deploy/initialize-container.sh), [database creation](deploy/create-container-database.sql), [migration SQL](backend/SentenceBuilder.Api/Persistence/Scripts/initial-persistence.sql) and [vocabulary seed](backend/SentenceBuilder.Api/Persistence/Scripts/seed-vocabulary.sql). Execute initialization only for the approved dedicated container database; inspect any existing data volume before reuse.

```sh
docker compose config --quiet
docker compose build
docker compose up -d sql
docker compose --profile initialize run --rm initialize
docker compose up -d api frontend
```

The opt-in job waits for SQL health, creates TIH_SentenceBuilder_Dev only if absent, applies the existing idempotent migration SQL and runs the transactional/repeatable seed. Scripts are mounted read-only; the API never creates, migrates or seeds on startup. The SQL hostname is the isolated service sql, not the host's Windows SQL instance. For existing initialized services, skip the job.

Open **http://localhost:8080**. Nginx serves Angular and proxies /api to the internal API. Only the frontend is published, at 127.0.0.1:8080; API and SQL have no host-published ports. The sql-data named volume stores SQL data. This topology uses local HTTP with Https__Redirect=false; do not expose it publicly or copy its TLS concessions into production.

### Stop and restart without resetting data

```sh
docker compose stop
docker compose up -d sql api frontend
```

The previously saved sentence remained available after this verified sequence. **Do not use down -v, delete volumes or drop databases as routine setup/troubleshooting.** Normal docker compose down preserves the named volume, but subsequent container recreation, volume migration and backup/restore were not verified. Do not rerun initialization merely to restart services. Review docker compose logs api frontend sql without exposing secrets when diagnosing failures.

## Manual Windows development

### Prerequisites and build

- .NET SDK **10.0.401**, pinned by [global.json](global.json), allowing its latest feature-band patch.
- Node.js **22.23.2** and npm **10.9.8**, the verified versions. Angular CLI is project-local; dotnet-ef **10.0.13** comes from [dotnet-tools.json](dotnet-tools.json).
- SQL Server 2022 or compatible SQL Server, Windows Authentication, and sqlcmd or SSMS. Your Windows account needs access to the dedicated database; creation/migration needs DDL permissions.

```powershell
dotnet tool restore
dotnet restore SentenceBuilder.slnx
dotnet build SentenceBuilder.slnx
```

### Local connection and database initialization

[appsettings.Development.json](backend/SentenceBuilder.Api/appsettings.Development.json) supplies this password-free default when the HTTPS Development profile is used:

```text
Server=localhost;Database=TIH_SentenceBuilder_Dev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True
```

For a different instance, override ConnectionStrings__SentenceBuilder or use dotnet user-secrets set 'ConnectionStrings:SentenceBuilder' with --project backend/SentenceBuilder.Api. Update sqlcmd -S targets consistently. Precedence is command line -> environment -> Development User Secrets -> environment-specific JSON -> base JSON. No C# connection fallback exists. Review overrides before initialization; missing/invalid configuration fails startup, while SQL availability is checked when used. Production credentials belong in protected external settings, never source/browser assets; local certificate trust is not production TLS configuration.

**Existing database:** inspect the target, ownership, InitialPersistence migration history and vocabulary. If already initialized, skip creation/migration/seeding. Do not reset an unrelated or existing database.

**Fresh dedicated development database only:** after confirming the intended instance/target and approving SQL writes, run from the solution root. These commands are setup operations, not startup or test prerequisites for an already migrated database:

```powershell
sqlcmd -S localhost -E -C -b -Q "IF DB_ID(N'TIH_SentenceBuilder_Dev') IS NOT NULL THROW 50001, 'Database already exists; inspect before reuse.', 1; CREATE DATABASE [TIH_SentenceBuilder_Dev];"
if ($LASTEXITCODE -ne 0) { throw 'Database creation failed; stop setup.' }
dotnet ef database update --project backend/SentenceBuilder.Api --connection 'Server=localhost;Database=TIH_SentenceBuilder_Dev;Integrated Security=True;Encrypt=True;TrustServerCertificate=True' -- --environment Development
if ($LASTEXITCODE -ne 0) { throw 'Migration failed; do not seed.' }
sqlcmd -S localhost -d TIH_SentenceBuilder_Dev -E -C -b -i backend/SentenceBuilder.Api/Persistence/Scripts/seed-vocabulary.sql
if ($LASTEXITCODE -ne 0) { throw 'Vocabulary initialization failed.' }
```

Alternatively apply the reviewed initial-persistence.sql instead of EF update; they are not two required migration steps. The seed defines nine categories/27 words, adds missing records without overwriting matching data and can be rerun to check repeatability. Verify initial counts with:

```powershell
sqlcmd -S localhost -d TIH_SentenceBuilder_Dev -E -C -b -Q "SELECT COUNT(*) AS Categories FROM dbo.WordType; SELECT COUNT(*) AS Words FROM dbo.Word;"
```

Its allowlist is TIH_SentenceBuilder_Dev, TIH_SentenceBuilder_Test and sqldb-sentence-builder-dev. Always verify the server as well as the name. No database initialization occurs on API startup.

### Start backend and frontend

Terminal 1, solution root:

```powershell
dotnet run --project backend/SentenceBuilder.Api --launch-profile https
```

Terminal 2, initially at the solution root:

```powershell
cd frontend/sentence-builder-ui
npm ci
npm start
```

Open **http://localhost:4200**. The proxy forwards /api to **https://localhost:7115**; API HTTP also binds **http://localhost:5293**. Development OpenAPI is https://localhost:7115/openapi/v1.json. Check http://localhost:4200/api/word-types for vocabulary connectivity. TIH_API_TARGET can override the proxy before npm start. If required, trust the local certificate with dotnet dev-certs https --trust.

## REST API

These are the implemented controller routes. JSON is camelCase; DTOs, not EF entities, cross the HTTP boundary.

| Method | Route | Behaviour |
| --- | --- | --- |
| GET | /api/word-types | Persisted categories |
| GET | /api/words?wordTypeId={id} | Persisted words belonging to a positive type ID |
| POST | /api/sentences | Create; returns 201, Location and detail |
| GET | /api/sentences?page=1&pageSize=20 | Paginated saved sentences; page defaults to 1, pageSize to 20 |
| GET | /api/sentences/{id} | Stored text, timestamps and ordered word snapshots |
| PUT | /api/sentences/{id} | Replace the ordered composition; returns 200 detail |

POST/PUT accept a JSON object with wordIds, an ordered array of 1..100 existing positive IDs; repeated IDs are allowed. Discover IDs through GET rather than assume seed identities. The backend generates text/snapshots; clients do not submit authoritative word text. Page size is 1..100. Validation/malformed requests return 400, missing resources 404, detected mid-request concurrency conflicts 409, and unexpected failures safe 500 Problem Details.

## Automated tests and production build

Host prerequisites above apply even if the application is run in Docker. From the solution root, disable SQL opt-in for the non-destructive backend checks:

```powershell
Remove-Item Env:TIH_SQL_TESTS_APPROVED,Env:TIH_TEST_CONNECTION -ErrorAction SilentlyContinue
dotnet build SentenceBuilder.slnx --artifacts-path .verification/backend
dotnet test SentenceBuilder.slnx --artifacts-path .verification/backend --no-build
cd frontend/sentence-builder-ui
npm ci
npm test -- --watch=false
npm run build
```

SQL tests intentionally skip without approval. Recorded non-destructive verification passed 21 backend cases, with 21 SQL test definitions skipped, plus all 57 Angular cases. The previously approved full SQL-enabled backend run passed 74 expanded cases. These historical results are not a new execution or exhaustive coverage claim.

Angular production output is frontend/sentence-builder-ui/dist/sentence-builder-ui/browser. Public API_BASE_URL defaults to /api; supply an absolute HTTPS API URL ending in /api for separate hosting, or use npm run configure:deployment -- <browser-output-directory> before uploading. The API URL is public configuration, not a secret. Cross-origin deployment requires explicit Cors__AllowedOrigins__0 etc.; CORS is not authentication.

### SQL integration tests - explicit approval required

Fixtures write to **TIH_SentenceBuilder_Test** only, roll back domain records and can advance identity/rowversion counters. They do not create/migrate databases. The Compose stack initializes its development catalog only; it does not set up the separate host test database or represent containerized integration-test coverage.

If an approved dedicated test database is new, create/migrate it separately (solution root). Existing migrated test databases skip these steps:

```powershell
sqlcmd -S localhost -E -C -b -Q "IF DB_ID(N'TIH_SentenceBuilder_Test') IS NOT NULL THROW 50001, 'Database already exists; inspect before reuse.', 1; CREATE DATABASE [TIH_SentenceBuilder_Test];"
if ($LASTEXITCODE -ne 0) { throw 'Test database creation failed; stop setup.' }
dotnet ef database update --project backend/SentenceBuilder.Api --connection 'Server=localhost;Database=TIH_SentenceBuilder_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True' -- --environment Development
if ($LASTEXITCODE -ne 0) { throw 'Test database migration failed.' }
```

Fixtures seed their own rollback transactions. After explicit approval, from the solution root:

```powershell
$env:TIH_TEST_CONNECTION = 'Server=localhost;Database=TIH_SentenceBuilder_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
$env:TIH_SQL_TESTS_APPROVED = '1'
try {
    dotnet build SentenceBuilder.slnx --artifacts-path .verification/backend
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
    dotnet test SentenceBuilder.slnx --artifacts-path .verification/backend --no-build
} finally {
    Remove-Item Env:TIH_SQL_TESTS_APPROVED,Env:TIH_TEST_CONNECTION -ErrorAction SilentlyContinue
}
```

Fixtures reject other catalog names before connecting; API tests inject the explicit transaction-bound test connection. Never point them at Azure, development or unrelated data.

## Troubleshooting and limitations

- For SQL failures, inspect instance availability, target/history, account permissions and configuration overrides. For missing vocabulary, verify explicit seed setup. Do not reset databases or add runtime fallback words.
- For HTTPS/proxy failures, confirm the 7115 profile, local certificate trust and TIH_API_TARGET. For Docker, inspect service health/logs; do not remove volumes. Registry access is required for restore/build.
- On Windows, stop only the workspace API before a normal build if apphost is locked, or use the isolated artifacts commands above. Stop the frontend before npm ci if esbuild is locked; do not kill unrelated processes.

Implementation limits:

- Snapshots follow FIFO per WordId, not exact duplicate identity. There is no client version token, so stale edits can overwrite completed changes; vocabulary changes can also affect new snapshots at save time.
- Drafts survive failed saves/409 but not page refresh. Check the saved list before retrying an ambiguous failure. There is no automatic write retry, authentication, grammar validation or delete endpoint.
- Functional testing included mobile Safari on a physical device, alongside viewport checks at 1440/1024/768/425/375px. This is not comprehensive device or accessibility certification. Azure and Docker checks establish the recorded workflows, not production readiness or exhaustive coverage. Previously reported development-tool advisories remain; the recorded production dependency audit was clean.

Categories, vocabulary and saved sentences come from SQL through REST. Labels, validation limits and configuration defaults remain technical constants. This is the domain-data interpretation of the assessment's no-hardcoded-values statement, not a claim to satisfy every literal reading.
