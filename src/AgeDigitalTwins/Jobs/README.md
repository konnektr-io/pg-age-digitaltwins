# Import Job Functionality

The Import Job functionality allows you to import models, digital twins, and relationships from ND-JSON (Newline Delimited JSON) streams into your Azure Digital Twins graph.

## Features

- **Streaming Import**: Process large ND-JSON files without loading everything into memory
- **Section-based Processing**: Handle Header, Models, Twins, and Relationships in order
- **Structured Logging**: Get detailed progress logs in JSON format
- **Error Handling**: Continue processing on individual failures with detailed error reporting
- **Cancellation Support**: Gracefully cancel operations using CancellationToken
- **Configurable Options**: Customize batch sizes, timeouts, and error handling behavior

## Usage

### Basic Usage

```csharp
using var inputStream = File.OpenRead("data.ndjson");
using var outputStream = File.Create("import-log.jsonl");

var result = await client.ImportAsync(inputStream, outputStream);

Console.WriteLine($"Import completed with status: {result.Status}");
Console.WriteLine($"Models: {result.ModelsStats.Succeeded} succeeded, {result.ModelsStats.Failed} failed");
Console.WriteLine($"Twins: {result.TwinsStats.Succeeded} succeeded, {result.TwinsStats.Failed} failed");
Console.WriteLine($"Relationships: {result.RelationshipsStats.Succeeded} succeeded, {result.RelationshipsStats.Failed} failed");
```

### With Custom Options

```csharp
var options = new ImportJobOptions
{
    ModelBatchSize = 100,
    ContinueOnFailure = true,
    OperationTimeout = TimeSpan.FromSeconds(30)
};

var result = await client.ImportAsync(inputStream, outputStream, options, cancellationToken);
```

## ND-JSON Format

The input file should follow this structure:

```jsonl
{"Section": "Header"}
{"fileVersion": "1.0.0", "author": "user", "organization": "company"}
{"Section": "Models"}
{"@id":"dtmi:example:model;1","@type":"Interface",...}
{"Section": "Twins"}
{"$dtId":"twin1","$metadata":{"$model":"dtmi:example:model;1"},...}
{"Section": "Relationships"}
{"$dtId":"twin1","$relationshipId":"rel1","$targetId":"twin2",...}
```

### Sections

1. **Header** (optional): Contains metadata like file version, author, organization
2. **Models** (optional): Contains DTDL model definitions
3. **Twins** (optional): Contains digital twin instances
4. **Relationships** (optional): Contains relationships between twins

## Configuration Options

### ImportJobOptions

- `ModelBatchSize` (default: 100): Number of models to process in a single batch
- `ContinueOnFailure` (default: true): Whether to continue processing on individual item failures
- `OperationTimeout` (default: 30 seconds): Timeout for individual operations

## Log Output

The output stream receives structured log entries documenting the import progress:

```json
{"timestamp":"2024-07-04T10:30:00.000Z","jobId":"abc12345","jobType":"Import","logType":"Info","details":{"status":"Started"}}
{"timestamp":"2024-07-04T10:30:01.000Z","jobId":"abc12345","jobType":"Import","logType":"Info","details":{"section":"Models","status":"Started"}}
{"timestamp":"2024-07-04T10:30:02.000Z","jobId":"abc12345","jobType":"Import","logType":"Info","details":{"section":"Models","status":"Succeeded"}}
```

## Error Handling

- Individual item failures are logged but don't stop the entire job (when `ContinueOnFailure` is true)
- Detailed error information is provided in the log output
- Summary statistics show total processed, succeeded, and failed counts for each section
- The overall job status indicates success, failure, or cancellation

## Performance Considerations

- Models are processed in batches for better performance
- Twins and relationships are processed individually (will be optimized in future versions)
- Memory usage is minimized by streaming the input file
- Database connections are reused where possible

## Future Enhancements

- **API Integration**: Support for Azure Blob storage streams
- **Performance Improvements**:
  - Batch processing for twins and relationships
  - Connection pooling and reuse
  - Parallel processing where safe
- **Enhanced Validation**: DTDL validation for relationships
- **Resume Capability**: Ability to resume interrupted imports
- **Transformation Support**: Data transformation during import
