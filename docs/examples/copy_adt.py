from azure.identity import AzureCliCredential
from azure.core.exceptions import HttpResponseError
from azure.digitaltwins.core import DigitalTwinsClient

source_uri = "https://sourceadt.api.weu.digitaltwins.azure.net/"
target_uri = "https://targetadt.api.digitaltwins.konnektr.io/"

try:
    credential = AzureCliCredential()
    source_service_client = DigitalTwinsClient(source_uri, credential)
    target_service_client = DigitalTwinsClient(target_uri, credential)

    # Copy Models
    models_query_result = source_service_client.list_models(
        include_model_definition=True
    )
    models = []
    for modeldata in models_query_result:
        models.append(modeldata.model)
    print(f"Pushing {len(models)} models")
    target_service_client.create_models(models)

    # Copy Digital Twins
    twins_query_result = source_service_client.query_twins("SELECT * FROM digitaltwins")
    for twin in twins_query_result:
        try:
            print(f"Pushing twin: {twin['$dtId']}")
            target_service_client.upsert_digital_twin(twin["$dtId"], twin)
        except HttpResponseError as e:
            print(f"Error pushing twin {twin['$dtId']}: {e.message}")

    # Copy Relationships
    relationships_query_result = source_service_client.query_twins(
        "SELECT * FROM relationships"
    )
    for rel in relationships_query_result:
        try:
            print(
                f"Pushing rel: {rel['$sourceId']} - {rel['$targetId']} - {rel['$relationshipId']}"
            )
            target_service_client.upsert_relationship(
                rel["$sourceId"], rel["$relationshipId"], rel
            )
        except HttpResponseError as e:
            print(
                f"Error pushing rel {rel['$sourceId']} - {rel['$targetId']} - {rel['$relationshipId']}: {e.message}"
            )

except HttpResponseError as e:
    print(f"\nThis script has caught an error: {e.message}")
