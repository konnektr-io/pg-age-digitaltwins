# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Konnektr Graph** (code name: `AgeDigitalTwins`) is an open-source, drop-in replacement for Azure Digital Twins built on PostgreSQL with the Apache AGE graph extension. It exposes an ADT-compatible REST API and supports real-time event streaming. Use `AgeDigitalTwins` for namespaces/packages; use `Konnektr Graph` in documentation and comments for commercial context.

## Commands

### Build
```bash
dotnet build ./src/AgeDigitalTwins.sln
```

### Run locally (via .NET Aspire)
```bash
dotnet run --project src/AgeDigitalTwins.AppHost
```
This starts the API service with its dependencies. Requires a PostgreSQL+AGE connection string.

### Run unit tests (no database required)
```bash
dotnet test './src' --filter 'Category!=Integration'
```

### Run all integration tests (requires PostgreSQL+AGE)
```bash
dotnet test './src'
```

### Run a single test project
```bash
dotnet test './src/AgeDigitalTwins.Test'
dotnet test './src/AgeDigitalTwins.ApiService.Test'
dotnet test './src/AgeDigitalTwins.Events.Test'
```

### Run a specific test by name filter
```bash
dotnet test './src' --filter 'FullyQualifiedName~MyTestName'
```

### Local test database setup
Integration tests expect PostgreSQL+AGE at `localhost:5432` with credentials `app/app`, database `app`. Configure in `src/AgeDigitalTwins.Test/appsettings.Development.json` (or the equivalent for each test project). Set `CNPG_TEST=true` when using CNPG-flavor AGE images.

For CI, the database is provisioned automatically via Docker in GitHub Actions (see `.github/workflows/tests.yml`). Two AGE image flavors are tested: `apache/age` and `ghcr.io/konnektr-io/age` (CNPG).

## Architecture

### Projects

- **`AgeDigitalTwins`** — Core SDK/class library (multi-targets net8/9/10). Published as NuGet package `Konnektr.AgeDigitalTwins`. Contains `AgeDigitalTwinsClient` (partial class split across files by domain), ADT-to-Cypher query translation (`AdtQueryHelpers`), graph schema initialization, DTDL validation, and the jobs system.

- **`AgeDigitalTwins.ApiService`** — ASP.NET Core Minimal API exposing the ADT-compatible REST surface. Endpoints are registered via extension methods in `Extensions/` (one file per resource: `DigitalTwinsEndpoints`, `RelationshipsEndpoints`, `ModelsEndpoints`, `ComponentsEndpoints`, `QueryEndpoints`, `TelemetryEndpoints`, `ImportJobEndpoints`, `GraphEndpoints`). Authentication (JWT Bearer) and authorization are optional and toggled via configuration.

- **`AgeDigitalTwins.Events`** — Standalone ASP.NET Core web service for real-time event streaming. Uses PostgreSQL logical replication (`AgeDigitalTwinsReplication`) to capture WAL changes and routes them as CloudEvents to pluggable sinks: Kafka, MQTT, Azure Data Explorer (Kusto), and Webhook. Sink implementations live in `Sinks/` and implement `IEventSink`.

- **`AgeDigitalTwins.AppHost`** — .NET Aspire orchestrator for local development.

- **`AgeDigitalTwins.ServiceDefaults`** — Shared service configuration (Aspire defaults, authorization policies, permission providers).

### Core data model in Apache AGE

The graph schema (created by `GraphInitialization`) uses:
- **`Twin` vertex label** — stores digital twins; indexed on `$dtId` (unique) and `$metadata.$model`
- **`Model` vertex label** — stores DTDL model definitions; indexed on `id`, with a `descendants` array for efficient inheritance queries
- **`_extends` edge label** — model inheritance relationships
- **`_hasComponent` edge label** — component containment relationships
- User-defined edge labels — represent twin relationships

Custom PostgreSQL functions are installed per-graph at initialization: `is_of_model`, `model_and_descendants`, `agtype_set`, `agtype_delete_key`, `is_object`, `is_number`, `is_primitive`, `is_string`.

### ADT query translation

`AdtQueryHelpers.ConvertAdtQueryToCypher()` translates ADT SQL-like queries (e.g., `SELECT * FROM DIGITALTWINS WHERE ...`) into Cypher queries executed via Apache AGE. This is the primary query path for the `/query` endpoint.

### AgeDigitalTwinsClient (core SDK)

Partial class split by concern:
- `AgeDigitalTwinsClient.cs` — constructor, options, datasource
- `AgeDigitalTwinsClient.DigitalTwins.cs` — twin CRUD
- `AgeDigitalTwinsClient.Relationships.cs` — relationship CRUD
- `AgeDigitalTwinsClient.Models.cs` — DTDL model management + caching
- `AgeDigitalTwinsClient.Components.cs` — component operations
- `AgeDigitalTwinsClient.Query.cs` — ADT query execution
- `AgeDigitalTwinsClient.Telemetry.cs` — telemetry publishing
- `AgeDigitalTwinsClient.Graph.cs` — graph lifecycle (create/drop)
- `AgeDigitalTwinsClient.Jobs.cs` — bulk import/delete job delegation

The client holds an in-memory model cache (`MemoryCache`) to avoid repeated DB lookups during DTDL validation. Cache TTL is configurable via `AgeDigitalTwinsClientOptions.ModelCacheExpiration` (default 10s).

### Jobs system

`JobService` (in `Jobs/`) manages long-running import and delete jobs using a PostgreSQL schema (`{graphName}_jobs`) for persistence and distributed locking. `ImportJob` processes ND-JSON streams (sections: Header → Models → Twins → Relationships) with checkpointing. The API service registers `JobResumptionService` (hosted service) to resume interrupted jobs on startup.

### Events pipeline

`AgeDigitalTwinsReplication` opens a PostgreSQL logical replication connection, reads WAL changes from the `Twin` and `Model` tables, and pushes `EventData` objects into an `IEventQueue`. `SharedEventConsumer` batches and routes events to registered `IEventSink` implementations. Each sink is wrapped in `ResilientEventSinkWrapper` for retry/DLQ behavior. Requires `wal_level=logical`, a replication publication, and a replication slot on the database.

### Configuration keys (ApiService)

Key `Parameters:` prefixed values in configuration:
- `AgeConnectionString` / ConnectionString `agedb` — database connection
- `AgeGraphName` — graph name (default: `digitaltwins`)
- `UseCnpgAge` — `true` for CNPG images, `false` for Apache AGE images (default: `true`)
- `JobsEnabled` — enable/disable import job endpoints and resumption service
- `ModelCacheExpirationSeconds`, `DefaultBatchSize`, `DefaultCheckpointInterval`
- `RateLimitingEnabled`, `MaxPoolSize`, `MinPoolSize`, `ConnectionTimeout`, `CommandTimeout`

Authentication: `Authentication:Enabled`, `Authentication:Authority`, `Authentication:Audience`, `Authentication:Issuer`
Authorization: `Authorization:Enabled`, `Authorization:Provider` (`Claims` or `Api`)

## Key conventions

- All tests inherit from `TestBase`, which creates an isolated temporary graph per test run and tears it down in `DisposeAsync`. This means integration tests are safe to run in parallel.
- Integration tests are tagged `Category=Integration` (or lack of the opposite filter). Unit tests pass with `--filter 'Category!=Integration'`.
- The `CNPG_TEST=true` environment variable switches the Npgsql AGE driver mode for CNPG-flavor images.
- Public API methods on `AgeDigitalTwinsClient` are `virtual` to support mocking/subclassing.
- Branding: code artifacts use `AgeDigitalTwins`; docs/comments may reference `Konnektr Graph`.
