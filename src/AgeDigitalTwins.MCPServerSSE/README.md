# AgeDigitalTwins.MCPServerSSE

## Overview

The **AgeDigitalTwins.MCPServerSSE** is a Model Context Protocol (MCP) server implementation built with .NET 9.0. It provides a communication protocol for facilitating interactions between various components in a model-driven system. This server is specifically designed to work with the **AgeDigitalTwins** SDK, enabling seamless integration with PostgreSQL and the Apache AGE extension for managing digital twins.

The server exposes tools that allow users to interact with digital twins, relationships, and models using the MCP protocol. It supports operations such as querying, creating, updating, and deleting digital twins and relationships, as well as managing DTDL (Digital Twins Definition Language) models.

## Features

- **Digital Twin Management**:
  - Fetch, create, update, and delete digital twins.
  - Retrieve incoming relationships and associated metadata for a specific twin.

- **Relationship Management**:
  - Fetch, create, update, and delete relationships between digital twins.
  - Support for filtering relationships by name.

- **Model Management**:
  - Fetch all DTDL models.
  - Create and delete DTDL models.

- **Query Execution**:
  - Execute Cypher queries against the digital twins graph.
  - Support for advanced query generation based on DTDL metadata.

- **Integration with Apache AGE**:
  - Leverages PostgreSQL with the Apache AGE extension for graph database capabilities.

- **MCP Protocol Support**:
  - Exposes tools for interacting with digital twins and relationships via the MCP protocol.
  - Designed for use with LLMs (Large Language Models) to enable intelligent query generation and interaction.

## How It Works

1. **Digital Twin Operations**:
   - The server provides tools to manage digital twins, including creating or replacing twins, updating them with JSON Patch, and deleting them.

2. **Relationship Operations**:
   - Relationships between twins can be created, updated, or deleted. The server also supports fetching relationships and filtering them by name.

3. **Model Operations**:
   - The server allows managing DTDL models, which define the schema and metadata for digital twins and relationships.

4. **Query Execution**:
   - Users can execute Cypher queries directly against the graph database. The server also supports generating queries based on DTDL metadata.

5. **Integration with LLMs**:
   - The server is designed to work with LLMs, enabling them to understand DTDL metadata and generate intelligent Cypher queries.

## Run Locally

To run the project locally:

```bash
dotnet run
```

You can inspect the MCP server using the following command:

```bash
npx @modelcontextprotocol/inspector
```

## Distribute as .NET Tool

To distribute the MCP server as a .NET tool:

1. Pack the project:

   ```bash
   dotnet pack -o Artefacts -c Release
   ```

2. Install the tool globally:

   ```bash
   dotnet tool install --global --add-source ./Artefacts AgeDigitalTwins.MCPServerSSE
   ```

   Once installed, the tool will be available as `AgeDigitalTwins.MCPServerSSE` in your terminal.

3. Run the inspector in production mode:

   ```bash
   npx @modelcontextprotocol/inspector -e DOTNET_ENVIRONMENT=Production AgeDigitalTwins.MCPServerSSE
   ```

## Use Cases

- **Digital Twin Applications**:
  - Manage digital twins and their relationships in a graph database.
  - Use DTDL models to define schemas and metadata for twins and relationships.

- **Query Execution**:
  - Execute complex Cypher queries to analyze relationships and metadata.

- **Integration with AI**:
  - Enable LLMs to interact with digital twins and generate intelligent queries based on DTDL metadata.

## Requirements

- **.NET 9.0**: The server is built using .NET 9.0.
- **PostgreSQL with Apache AGE**: Required for graph database capabilities.
- **AgeDigitalTwins SDK**: Provides the core functionality for managing digital twins and relationships.

## Contributing

Contributions are welcome! Please follow the standard GitHub workflow for submitting issues and pull requests.

## License

This project is licensed under the MIT License. See the LICENSE file for details.
