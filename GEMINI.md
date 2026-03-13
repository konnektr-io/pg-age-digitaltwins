# Gemini Code Intelligence File

This file provides context for Gemini to understand the `pg-age-digitaltwins` (Konnektr Graph) repository.

## Project Overview

This repository contains the source code for **Konnektr Graph**, an open-source, high-performance digital twin platform that is compatible with the Azure Digital Twins (ADT) API. It is built on C#/.NET, PostgreSQL, and the Apache AGE graph extension.

The core purpose of this project is to provide a powerful, self-hostable alternative to Azure Digital Twins, enabling developers to build and manage digital twin solutions with graph-native queries (Cypher) and real-time eventing.

The solution is structured as a .NET application with several key projects:
-   **AgeDigitalTwins.ApiService**: The main ASP.NET Core web API that exposes the ADT-compatible endpoints.
-   **AgeDigitalTwins.Events**: A project for handling real-time event streaming and routing.
-   **AgeDigitalTwins**: The core class library containing the client logic and data models.
-   **AgeDigitalTwins.AppHost**: A .NET Aspire project used to orchestrate and run the various services during local development.
-   **Test Projects**: Several projects dedicated to unit and integration tests.

## Building and Running

The project uses the standard `dotnet` CLI for building, testing, and running.

### Building the Project

To build the entire solution, run the following command from the root directory:

```bash
dotnet build ./src/AgeDigitalTwins.sln
```

### Running the Application (Local Development)

This project uses .NET Aspire to manage local development. To run the API service and its dependencies, execute the `AppHost` project:

```bash
dotnet run --project src/AgeDigitalTwins.AppHost
```

This will start the `ApiService` and any other services configured in the `AppHost`. The Aspire dashboard will be available at a local URL to monitor service logs, endpoints, and environment variables. Note that a database connection string for PostgreSQL/AGE is required.

### Running Tests

The solution contains unit and integration tests. To run the unit tests (excluding integration tests, which require a running database), use the following command:

```bash
dotnet test './src' --filter 'Category!=Integration'
```

## Development Conventions

The project follows a set of established conventions to ensure code quality and consistency.

-   **Architecture**:
    -   Follow **SOLID principles** and idiomatic C#/.NET practices.
    -   Maintain a **decoupled architecture**. Do not introduce direct dependencies on other platform applications; use APIs or asynchronous messaging.
    -   All authentication/authorization is handled by a central **Control Plane (KtrlPlane)**. The application validates JWTs from this service.

-   **Code Quality & Naming**:
    -   Use `AgeDigitalTwins` for open-source code artifacts (namespaces, packages).
    -   Reference the commercial name `Konnektr Graph` in documentation and comments where relevant.
    -   Implement consistent error handling and surface errors clearly in API responses.

-   **Testing**:
    -   Tests should be reliable and reproducible.
    -   Most tests require a running PostgreSQL instance with the Apache AGE extension.
    -   CI pipelines on GitHub Actions automatically provision the required database environment.
    -   Keep test coverage high for all public APIs and core logic.

-   **Documentation**:
    -   Keep all documentation in the `/docs` folder up-to-date with any changes.
    -   Public APIs should have XML comments.
    -   The `.github/PLATFORM_SCOPE.md` file defines the architectural boundaries of this application within the broader Konnektr ecosystem.
