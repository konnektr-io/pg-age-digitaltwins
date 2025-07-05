using System.IO;
using System.Text;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs.Models;
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
        var result = await Client.ImportAsync(inputStream, outputStream, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.JobId);
        Assert.Equal(ImportJobStatus.Succeeded, result.Status);

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
        _output.WriteLine($"Job ID: {result.JobId}");
        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Start Time: {result.StartTime}");
        _output.WriteLine($"End Time: {result.EndTime}");
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
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportAsync(inputStream, outputStream, options)
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
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportAsync(inputStream, outputStream, options)
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
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await Client.ImportAsync(inputStream, outputStream, options)
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
        var result = await Client.ImportAsync(inputStream, outputStream, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ImportJobStatus.PartiallySucceeded, result.Status);

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
        var result = await Client.ImportAsync(inputStream, outputStream, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ImportJobStatus.Succeeded, result.Status);

        // Verify models were created
        Assert.Equal(2, result.ModelsCreated);

        // Verify no twins or relationships were processed
        Assert.Equal(0, result.TwinsCreated);
        Assert.Equal(0, result.RelationshipsCreated);
        Assert.Equal(0, result.ErrorCount);
    }
}
