using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AgeDigitalTwins.Jobs.Models;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Example usage of the Import Job functionality.
/// </summary>
public class ImportJobExample
{
    /// <summary>
    /// Demonstrates how to use the ImportAsync method with sample ND-JSON data.
    /// </summary>
    /// <param name="client">The AgeDigitalTwinsClient instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task<ImportJobResult> RunImportExampleAsync(AgeDigitalTwinsClient client)
    {
        // Sample ND-JSON data matching the format from the documentation
        var sampleData =
            @"{""Section"": ""Header""}
{""fileVersion"": ""1.0.0"", ""author"": ""example"", ""organization"": ""contoso""}
{""Section"": ""Models""}
{""@id"":""dtmi:com:microsoft:azure:iot:model0;1"",""@type"":""Interface"",""contents"":[{""@type"":""Property"",""name"":""property00"",""schema"":""integer""},{""@type"":""Property"",""name"":""property01"",""schema"":{""@type"":""Map"",""mapKey"":{""name"":""subPropertyName"",""schema"":""string""},""mapValue"":{""name"":""subPropertyValue"",""schema"":""string""}}},{""@type"":""Relationship"",""name"":""has"",""target"":""dtmi:com:microsoft:azure:iot:model1;1"",""properties"":[{""@type"":""Property"",""name"":""relationshipproperty1"",""schema"":""string""},{""@type"":""Property"",""name"":""relationshipproperty2"",""schema"":""integer""}]}],""description"":{""en"":""This is the description of model""},""displayName"":{""en"":""This is the display name""},""@context"":""dtmi:dtdl:context;2""}
{""@id"":""dtmi:com:microsoft:azure:iot:model1;1"",""@type"":""Interface"",""contents"":[{""@type"":""Property"",""name"":""property10"",""schema"":""string""},{""@type"":""Property"",""name"":""property11"",""schema"":{""@type"":""Map"",""mapKey"":{""name"":""subPropertyName"",""schema"":""string""},""mapValue"":{""name"":""subPropertyValue"",""schema"":""string""}}}],""description"":{""en"":""This is the description of model""},""displayName"":{""en"":""This is the display name""},""@context"":""dtmi:dtdl:context;2""}
{""Section"": ""Twins""}
{""$dtId"":""twin0"",""$metadata"":{""$model"":""dtmi:com:microsoft:azure:iot:model0;1""},""property00"":10,""property01"":{""subProperty1"":""subProperty1Value"",""subProperty2"":""subProperty2Value""}}
{""$dtId"":""twin1"",""$metadata"":{""$model"":""dtmi:com:microsoft:azure:iot:model1;1""},""property10"":""propertyValue1"",""property11"":{""subProperty1"":""subProperty1Value"",""subProperty2"":""subProperty2Value""}}
{""Section"": ""Relationships""}
{""$dtId"":""twin0"",""$relationshipId"":""relationship"",""$targetId"":""twin1"",""$relationshipName"":""has"",""relationshipProperty1"":""propertyValue1"",""relationshipProperty2"":10}";

        // Create input stream from sample data
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(sampleData));

        // Create output stream to capture logs
        using var outputStream = new MemoryStream();

        // Configure import options
        var options = new ImportJobOptions
        {
            ModelBatchSize = 50,
            ContinueOnFailure = true,
            OperationTimeout = TimeSpan.FromSeconds(30),
        };

        // Execute the import
        var result = await client.ImportAsync(inputStream, outputStream, options);

        // Read the log output
        outputStream.Position = 0;
        using var reader = new StreamReader(outputStream);
        var logOutput = await reader.ReadToEndAsync();

        Console.WriteLine("Import Job Result:");
        Console.WriteLine($"Job ID: {result.JobId}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Start Time: {result.StartTime}");
        Console.WriteLine($"End Time: {result.EndTime}");
        Console.WriteLine(
            $"Models - Total: {result.ModelsStats.TotalProcessed}, Succeeded: {result.ModelsStats.Succeeded}, Failed: {result.ModelsStats.Failed}"
        );
        Console.WriteLine(
            $"Twins - Total: {result.TwinsStats.TotalProcessed}, Succeeded: {result.TwinsStats.Succeeded}, Failed: {result.TwinsStats.Failed}"
        );
        Console.WriteLine(
            $"Relationships - Total: {result.RelationshipsStats.TotalProcessed}, Succeeded: {result.RelationshipsStats.Succeeded}, Failed: {result.RelationshipsStats.Failed}"
        );
        Console.WriteLine();
        Console.WriteLine("Log Output:");
        Console.WriteLine(logOutput);

        return result;
    }
}
