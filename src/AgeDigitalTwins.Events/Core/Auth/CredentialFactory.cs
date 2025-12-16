using Azure.Core;
using Azure.Identity;

namespace AgeDigitalTwins.Events.Core.Auth;

/// <summary>
/// Factory for creating token credentials based on configuration.
/// </summary>
public static class CredentialFactory
{
    public static TokenCredential CreateCredential(string? tenantId, string? clientId, string? clientSecret)
    {
        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            var options = new ClientSecretCredentialOptions();
            if(!string.IsNullOrWhiteSpace(tenantId))
            {
               options.AuthorityHost = AzureAuthorityHosts.AzurePublicCloud;
            }

            return new ClientSecretCredential(
                tenantId ?? Environment.GetEnvironmentVariable("AZURE_TENANT_ID"), 
                clientId, 
                clientSecret, 
                options);
        }

        return new DefaultAzureCredential();
    }
}
