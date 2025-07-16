using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs;
using AgeDigitalTwins.Models;
using Xunit.Abstractions;

namespace AgeDigitalTwins.Test;

[Trait("Category", "Integration")]
public class ImportJobTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public ImportJobTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ImportAsync_WithValidNdJsonData_ShouldImportSuccessfully()
    {
        // Arrange - Sample ND-JSON data matching the format from the documentation
        var sampleData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""contoso""}
{""Section"": ""Models""}
{""@id"":""dtmi:com:microsoft:azure:iot:model0;1"",""@type"":""Interface"",""contents"":[{""@type"":""Property"",""name"":""property00"",""schema"":""integer""},{""@type"":""Property"",""name"":""property01"",""schema"":{""@type"":""Map"",""mapKey"":{""name"":""subPropertyName"",""schema"":""string""},""mapValue"":{""name"":""subPropertyValue"",""schema"":""string""}}},{""@type"":""Relationship"",""name"":""has"",""target"":""dtmi:com:microsoft:azure:iot:model1;1"",""properties"":[{""@type"":""Property"",""name"":""relationshipproperty1"",""schema"":""string""},{""@type"":""Property"",""name"":""relationshipproperty2"",""schema"":""integer""}]}],""description"":{""en"":""This is the description of model""},""displayName"":{""en"":""This is the display name""},""@context"":""dtmi:dtdl:context;2""}
{""@id"":""dtmi:com:microsoft:azure:iot:model1;1"",""@type"":""Interface"",""contents"":[{""@type"":""Property"",""name"":""property10"",""schema"":""string""},{""@type"":""Property"",""name"":""property11"",""schema"":{""@type"":""Map"",""mapKey"":{""name"":""subPropertyName"",""schema"":""string""},""mapValue"":{""name"":""subPropertyValue"",""schema"":""string""}}}],""description"":{""en"":""This is the description of model""},""displayName"":{""en"":""This is the display name""},""@context"":""dtmi:dtdl:context;2""}
{""Section"": ""Twins""}
{""$dtId"":""twin0"",""$metadata"":{""$model"":""dtmi:com:microsoft:azure:iot:model0;1""},""property00"":10,""property01"":{""subProperty1"":""subProperty1Value"",""subProperty2"":""subProperty2Value""}}
{""$dtId"":""twin1"",""$metadata"":{""$model"":""dtmi:com:microsoft:azure:iot:model1;1""},""property10"":""propertyValue1"",""property11"":{""subProperty1"":""subProperty1Value"",""subProperty2"":""subProperty2Value""}}
{""Section"": ""Relationships""}
{""$dtId"":""twin0"",""$relationshipId"":""relationship"",""$targetId"":""twin1"",""$relationshipName"":""has"",""relationshipProperty1"":""propertyValue1"",""relationshipProperty2"":10}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(sampleData));
        using var outputStream = new MemoryStream();

        var options = new ImportJobOptions
        {
            ContinueOnFailure = true,
            OperationTimeout = TimeSpan.FromSeconds(30),
        };

        // Act
        var jobId = $"test-import-{Guid.NewGuid().ToString("N")[..8]}";
        var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Id);
        Assert.Equal(JobStatus.Succeeded, result.Status);

        // Verify models were created
        Assert.Equal(2, result.ModelsCreated);

        // Verify twins were created
        Assert.Equal(2, result.TwinsCreated);

        // Verify relationships were created
        Assert.Equal(1, result.RelationshipsCreated);

        // Verify no errors
        Assert.Equal(0, result.ErrorCount);

        // Verify log output was generated
        outputStream.Position = 0;
        using var reader = new StreamReader(outputStream);
        var logOutput = await reader.ReadToEndAsync();
        Assert.NotEmpty(logOutput);

        _output.WriteLine("Import Job Result:");
        _output.WriteLine($"Job ID: {result.Id}");
        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Start Time: {result.CreatedDateTime}");
        _output.WriteLine($"End Time: {result.FinishedDateTime}");
        _output.WriteLine($"Models Created: {result.ModelsCreated}");
        _output.WriteLine($"Twins Created: {result.TwinsCreated}");
        _output.WriteLine($"Relationships Created: {result.RelationshipsCreated}");
        _output.WriteLine($"Error Count: {result.ErrorCount}");
        _output.WriteLine("Log Output:");
        _output.WriteLine(logOutput);
    }

    [Fact]
    public async Task ImportAsync_WithInvalidFileVersion_ShouldFail()
    {
        // Arrange - Invalid file version
        var invalidData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""2.0.0"", ""author"": ""test"", ""organization"": ""contoso""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(invalidData));
        using var outputStream = new MemoryStream();

        var options = new ImportJobOptions();

        // Act & Assert
        var jobId = $"test-import-{Guid.NewGuid().ToString("N")[..8]}";
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportGraphAsync(jobId, inputStream, outputStream, options)
        );

        Assert.Contains("Unsupported file version", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_WithMissingHeader_ShouldFail()
    {
        // Arrange - Missing header section
        var invalidData =
            @"{""Section"": ""Models""}
{""@id"":""dtmi:com:test;1"",""@type"":""Interface"",""@context"":""dtmi:dtdl:context;2""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(invalidData));
        using var outputStream = new MemoryStream();

        var options = new ImportJobOptions();

        // Act & Assert
        var jobId = $"test-import-{Guid.NewGuid().ToString("N")[..8]}";
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportGraphAsync(jobId, inputStream, outputStream, options)
        );

        Assert.Contains("First section must be 'Header'", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_WithEmptyStream_ShouldFail()
    {
        // Arrange - Empty stream
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        var options = new ImportJobOptions();

        // Act & Assert
        var jobId = $"test-import-{Guid.NewGuid().ToString("N")[..8]}";
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportGraphAsync(jobId, inputStream, outputStream, options)
        );

        Assert.Contains("Empty input stream", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_WithContinueOnFailureEnabled_ShouldContinueProcessing()
    {
        // Arrange - Data with intentional errors in twins section
        var dataWithErrors =
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""contoso""}
{""Section"": ""Models""}
{""@id"":""dtmi:com:test:validmodel;1"",""@type"":""Interface"",""contents"":[{""@type"":""Property"",""name"":""testProperty"",""schema"":""string""}],""@context"":""dtmi:dtdl:context;2""}
{""Section"": ""Twins""}
{""$dtId"":""validTwin"",""$metadata"":{""$model"":""dtmi:com:test:validmodel;1""},""testProperty"":""value""}
{""$dtId"":""invalidTwin"",""$metadata"":{""$model"":""dtmi:com:test:nonexistentmodel;1""},""testProperty"":""value""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(dataWithErrors));
        using var outputStream = new MemoryStream();

        var options = new ImportJobOptions { ContinueOnFailure = true };

        // Act
        var jobId = $"test-import-{Guid.NewGuid().ToString("N")[..8]}";
        var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(JobStatus.PartiallySucceeded, result.Status);

        // Verify models were created successfully
        Assert.Equal(1, result.ModelsCreated);

        // Verify one twin succeeded and one failed (only 1 twin created due to the failure)
        Assert.Equal(1, result.TwinsCreated);
        Assert.True(result.ErrorCount > 0);

        _output.WriteLine($"Import completed with status: {result.Status}");
        _output.WriteLine($"Twins Created: {result.TwinsCreated}, Errors: {result.ErrorCount}");
    }

    [Fact]
    public async Task ImportAsync_WithOnlyModelsSection_ShouldSucceed()
    {
        // Arrange - Only models section
        var modelsOnlyData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""contoso""}
{""Section"": ""Models""}
{""@id"":""dtmi:com:test:model1;1"",""@type"":""Interface"",""contents"":[{""@type"":""Property"",""name"":""prop1"",""schema"":""string""}],""@context"":""dtmi:dtdl:context;2""}
{""@id"":""dtmi:com:test:model2;1"",""@type"":""Interface"",""contents"":[{""@type"":""Property"",""name"":""prop2"",""schema"":""integer""}],""@context"":""dtmi:dtdl:context;2""}";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(modelsOnlyData));
        using var outputStream = new MemoryStream();

        var options = new ImportJobOptions();

        // Act
        var jobId = $"test-import-{Guid.NewGuid().ToString("N")[..8]}";
        var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(JobStatus.Succeeded, result.Status);

        // Verify models were created
        Assert.Equal(2, result.ModelsCreated);

        // Verify no twins or relationships were processed
        Assert.Equal(0, result.TwinsCreated);
        Assert.Equal(0, result.RelationshipsCreated);
        Assert.Equal(0, result.ErrorCount);
    }

    // ===== REFACTORED IMPORT JOB TESTS =====
    // Tests for the refactored import job system using the new persistent job service
    // Tests use the client methods which internally use the new PostgreSQL job service

    [Fact]
    public async Task Client_ImportGraphAsync_WithStreams_ShouldSucceed()
    {
        // Arrange
        var jobId = "test-job-" + Guid.NewGuid().ToString("N")[..8];
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Add some test data to input stream with proper ND-JSON format
        var testData = Encoding.UTF8.GetBytes(
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""test""}
{""Section"": ""Models""}
{""@id"":""dtmi:example:Model;1"",""@type"":""Interface"",""@context"":""dtmi:dtdl:context;2""}
"
        );
        inputStream.Write(testData);
        inputStream.Position = 0;

        // Act
        var result = await Client.ImportGraphAsync(jobId, inputStream, outputStream, (object?)null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.True(
            result.Status == JobStatus.Succeeded || result.Status == JobStatus.PartiallySucceeded
        );
        Assert.True(result.CreatedDateTime > DateTime.MinValue);
        Assert.True(result.LastActionDateTime > DateTime.MinValue);

        _output.WriteLine($"Executed import job with ID: {result.Id}");
        _output.WriteLine($"Job status: {result.Status}");
    }

    [Fact]
    public async Task Client_ImportGraphAsync_WithNullInputStream_ShouldThrow()
    {
        // Arrange
        var jobId = "test-job-" + Guid.NewGuid().ToString("N")[..8];
        using var outputStream = new MemoryStream();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Client.ImportGraphAsync(jobId, null!, outputStream, (object?)null)
        );

        Assert.Equal("inputStream", exception.ParamName);
        _output.WriteLine($"Expected ArgumentNullException thrown: {exception.Message}");
    }

    [Fact]
    public async Task Client_ImportGraphAsync_WithNullOutputStream_ShouldThrow()
    {
        // Arrange
        var jobId = "test-job-" + Guid.NewGuid().ToString("N")[..8];
        using var inputStream = new MemoryStream();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Client.ImportGraphAsync(jobId, inputStream, null!, (object?)null)
        );

        Assert.Equal("outputStream", exception.ParamName);
        _output.WriteLine($"Expected ArgumentNullException thrown: {exception.Message}");
    }

    [Fact]
    public async Task Client_ImportGraphAsync_WithDuplicateJobId_ShouldThrow()
    {
        // Arrange
        var jobId = "test-job-" + Guid.NewGuid().ToString("N")[..8];
        using var inputStream1 = new MemoryStream();
        using var outputStream1 = new MemoryStream();
        using var inputStream2 = new MemoryStream();
        using var outputStream2 = new MemoryStream();

        // Add minimal test data
        var testData = Encoding.UTF8.GetBytes(
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""test""}"
        );
        inputStream1.Write(testData);
        inputStream1.Position = 0;
        inputStream2.Write(testData);
        inputStream2.Position = 0;

        // Act
        var result1 = await Client.ImportGraphAsync(
            jobId,
            inputStream1,
            outputStream1,
            (object?)null
        );
        _output.WriteLine($"First job executed with ID: {result1.Id}");

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Client.ImportGraphAsync(jobId, inputStream2, outputStream2, (object?)null)
        );

        Assert.Contains($"already exists", exception.Message);
        _output.WriteLine($"Expected InvalidOperationException thrown: {exception.Message}");
    }

    [Fact]
    public async Task Client_GetImportJob_ShouldReturnCorrectJob()
    {
        // Arrange
        var jobId = "test-job-" + Guid.NewGuid().ToString("N")[..8];
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Add minimal test data
        var testData = Encoding.UTF8.GetBytes(
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""test""}"
        );
        inputStream.Write(testData);
        inputStream.Position = 0;

        // Act
        var executedJob = await Client.ImportGraphAsync(
            jobId,
            inputStream,
            outputStream,
            (object?)null
        );
        var retrievedJob = Client.GetImportJob(jobId);

        // Assert
        Assert.NotNull(retrievedJob);
        Assert.Equal(executedJob.Id, retrievedJob.Id);
        Assert.Equal(executedJob.Status, retrievedJob.Status);
        Assert.Equal(executedJob.CreatedDateTime, retrievedJob.CreatedDateTime);

        _output.WriteLine($"Retrieved job: {retrievedJob.Id} with status: {retrievedJob.Status}");
    }

    [Fact]
    public void Client_GetImportJob_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        // No setup needed since JobService is auto-initialized

        // Act
        var result = Client.GetImportJob("non-existent-job-id");

        // Assert
        Assert.Null(result);
        _output.WriteLine("GetImportJob returned null for non-existent job ID as expected");
    }

    [Fact]
    public async Task Client_ListImportJobs_ShouldReturnAllJobs()
    {
        // Arrange
        var jobId1 = "test-job-1-" + Guid.NewGuid().ToString("N")[..8];
        var jobId2 = "test-job-2-" + Guid.NewGuid().ToString("N")[..8];

        using var inputStream1 = new MemoryStream();
        using var outputStream1 = new MemoryStream();
        using var inputStream2 = new MemoryStream();
        using var outputStream2 = new MemoryStream();

        // Add minimal test data
        var testData = Encoding.UTF8.GetBytes(
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""test"", ""organization"": ""test""}"
        );
        inputStream1.Write(testData);
        inputStream1.Position = 0;
        inputStream2.Write(testData);
        inputStream2.Position = 0;

        // Act
        await Client.ImportGraphAsync(jobId1, inputStream1, outputStream1, (object?)null);
        await Client.ImportGraphAsync(jobId2, inputStream2, outputStream2, (object?)null);
        var jobs = (await Client.GetImportJobsAsync()).ToList();

        // Assert
        Assert.Contains(jobs, j => j.Id == jobId1);
        Assert.Contains(jobs, j => j.Id == jobId2);
        Assert.True(jobs.Count >= 2);

        _output.WriteLine($"Listed {jobs.Count} import jobs");
        foreach (var job in jobs)
        {
            _output.WriteLine($"  - Job ID: {job.Id}, Status: {job.Status}");
        }
    }
}
