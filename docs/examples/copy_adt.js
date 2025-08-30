const { DefaultAzureCredential } = require("@azure/identity");
const { DigitalTwinsClient } = require("@azure/digital-twins-core");

const sourceUrl = "https://sourceadt.api.weu.digitaltwins.azure.net/";
const targetUrl = "https://targetadt.api.digitaltwins.konnektr.io/";

async function main() {
  const credential = new DefaultAzureCredential();
  const sourceClient = new DigitalTwinsClient(sourceUrl, credential);
  const targetClient = new DigitalTwinsClient(targetUrl, credential);

  // Copy Models
  const models = [];
  for await (const model of sourceClient.listModels({ includeModelDefinition: true })) {
    models.push(model.model);
  }
  console.log(`Pushing ${models.length} models`);
  await targetClient.createModels(models);

  // Copy Digital Twins
  for await (const twin of sourceClient.queryTwins("SELECT * FROM digitaltwins")) {
    try {
      console.log(`Pushing twin: ${twin.$dtId}`);
      await targetClient.upsertDigitalTwin(twin.$dtId, twin);
    } catch (e) {
      console.error(`Error pushing twin ${twin.$dtId}: ${e.message}`);
    }
  }

  // Copy Relationships
  for await (const rel of sourceClient.queryTwins("SELECT * FROM relationships")) {
    try {
      console.log(`Pushing rel: ${rel.$sourceId} - ${rel.$targetId} - ${rel.$relationshipId}`);
      await targetClient.upsertRelationship(rel.$sourceId, rel.$relationshipId, rel);
    } catch (e) {
      console.error(`Error pushing rel ${rel.$sourceId} - ${rel.$targetId} - ${rel.$relationshipId}: ${e.message}`);
    }
  }
}

main().catch(console.error);
