# AgeDigitalTwins

AgeDigitalTwins is an SDK and API designed to support Digital Twins applications running on PostgreSQL with the Apache AGE extension. It supports DTDL (Digital Twins Definition Language) data models and allows for the validation of instances loaded in the graph database. The API and SDK are designed to make the transition from Azure Digital Twins easy.

## Features

- **DTDL Support**: Full support for DTDL data models.
- **Graph Database**: Utilizes PostgreSQL with the Apache AGE extension for graph database capabilities.
- **Validation**: Validate instances loaded in the graph database.
- **Easy Transition**: Designed to make the transition from Azure Digital Twins seamless.

## Roadmap

- [ ] SDK:
  - [ ] Digital Twins CRUD operations
  - [ ] Models CRUD operations
  - [ ] Relationships CRUD operations
  - [ ] Model Validation with DTDLParser
  - [ ] Twin Validation with DTDLParser
- [ ] API:
  - [ ] Digital Twins CRUD operations
  - [ ] Models CRUD operations
  - [ ] Relationships CRUD operations
- [] Deployment:
  - [ ] Dockerize the API
  - [ ] Helm chart for deployment
- [ ] Event routing: <https://event-driven.io/en/push_based_outbox_pattern_with_postgres_logical_replication/?utm_source=github_outbox_cdc>
  - [ ] CDC connection with AgType parser (either in C# or leverage Debezium)
  - [ ] Data History output (same format as Azure Digital Twins)
  - [ ] CloudEvents output
  - [ ] Kafka route
  - [ ] ADX/Fabric Real-time route
  - [ ] MQTT route (IoT Operations)

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [Apache AGE extension](https://age.apache.org/)

### Installation

1. Clone the repository:

    ```sh
    git clone https://github.com/your-repo/age-digitaltwins.git
    cd age-digitaltwins
    ```

2. Build the solution:

    ```sh
    dotnet build
    ```

3. Set up PostgreSQL and install the Apache AGE extension. Follow the instructions [here](https://age.apache.org/age-manual/master/intro.html).

### Configuration

Update the `appsettings.json` file in the `AgeDigitalTwins.Api` project with your PostgreSQL connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=your_db;Username=your_user;Password=your_password"
  }
}
```

### Running the API

Navigate to the AgeDigitalTwins.Api directory and run the API:

```sh
dotnet run
```

The API will be available at <http://localhost:5000>.

## Usage

### SDK

To use the SDK, add a reference to the AgeDigitalTwins project in your .NET application:

```xml
<ProjectReference Include="..\path\to\AgeDigitalTwins\AgeDigitalTwins.csproj" />
```

### API Endpoints

Get Digital Twin

```http
GET /api/digitaltwins/{id}
```

Put Digital Twin

```http
PUT /api/digitaltwins/{id}
```

Example
Here is an example of how to use the SDK to interact with the API:

```csharp
using AgeDigitalTwins.Api.Models;
using ApacheAGE;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        var client = new DigitalTwinsController(configuration);

        string twinId = "exampleTwin";
        string twinData = "{\"_dtId\":\"exampleTwin\",\"name\":\"Example Twin\"}";

        await client.PutTwin(twinId, twinData, CancellationToken.None);
        var twin = await client.GetTwin(twinId, CancellationToken.None);

        Console.WriteLine(twin);
    }
}
```

## Contributing

Contributions are welcome! Please read our [contributing guidelines](CONTRIBUTING.md) for more information.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgements

- [Apache AGE](https://age.apache.org/)
- [Apache AGE C# client](https://github.com/Allison-E/pg-age)
- [Azure Digital Twins](https://azure.microsoft.com/en-us/services/digital-twins/)
