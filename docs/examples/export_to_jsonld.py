from azure.identity import AzureCliCredential
from azure.digitaltwins.core import DigitalTwinsClient
import json

source_uri = "https://sourceadt.api.weu.digitaltwins.azure.net/"
credential = AzureCliCredential()
source_client = DigitalTwinsClient(source_uri, credential)

with open("export.jsonld", "w", encoding="utf-8") as f:
    # Write header
    f.write(json.dumps({"Section": "Header"}) + "\n")
    f.write(json.dumps({"fileVersion": "1.0.0"}) + "\n")

    # Export models
    for model in source_client.list_models(include_model_definition=True):
        f.write(json.dumps({"Section": "Models"}) + "\n")
        f.write(json.dumps(model.model) + "\n")

    # Export twins
    for twin in source_client.query_twins("SELECT * FROM digitaltwins"):
        f.write(json.dumps({"Section": "Twins"}) + "\n")
        f.write(json.dumps(twin) + "\n")

    # Export relationships
    for rel in source_client.query_twins("SELECT * FROM relationships"):
        f.write(json.dumps({"Section": "Relationships"}) + "\n")
        f.write(json.dumps(rel) + "\n")
