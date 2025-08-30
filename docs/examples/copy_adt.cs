using System;
using System.Threading.Tasks;
using Azure.DigitalTwins.Core;
using Azure.Identity;

class Program
{
    static async Task Main(string[] args)
    {
        var sourceUri = "https://sourceadt.api.weu.digitaltwins.azure.net/";
        var targetUri = "https://targetadt.api.digitaltwins.konnektr.io/";
        var credential = new AzureCliCredential();
        var sourceClient = new DigitalTwinsClient(new Uri(sourceUri), credential);
        var targetClient = new DigitalTwinsClient(new Uri(targetUri), credential);

        // Copy Models
        var models = await sourceClient.GetModelsAsync(
            new GetModelsOptions { IncludeModelDefinition = true }
        );
        var modelList = new List<string>();
        await foreach (var model in models)
        {
            modelList.Add(model.DtdlModel);
        }
        Console.WriteLine($"Pushing {modelList.Count} models");
        await targetClient.CreateModelsAsync(modelList);

        // Copy Digital Twins
        var twins = sourceClient.QueryAsync("SELECT * FROM digitaltwins");
        await foreach (var twin in twins)
        {
            var id = twin["$dtId"].ToString();
            Console.WriteLine($"Pushing twin: {id}");
            await targetClient.UpsertDigitalTwinAsync(id, twin.ToString());
        }

        // Copy Relationships
        var rels = sourceClient.QueryAsync("SELECT * FROM relationships");
        await foreach (var rel in rels)
        {
            var sourceId = rel["$sourceId"].ToString();
            var relId = rel["$relationshipId"].ToString();
            Console.WriteLine($"Pushing rel: {sourceId} - {rel["$targetId"]} - {relId}");
            await targetClient.UpsertRelationshipAsync(sourceId, relId, rel.ToString());
        }
    }
}
