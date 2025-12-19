namespace AgeDigitalTwins.MCPServerHttp.Configuration;

/// <summary>
/// Configuration options for OAuth 2.1 Protected Resource Metadata (RFC 9728).
/// </summary>
public class OAuthMetadataOptions
{
    /// <summary>
    /// Gets or sets the MCP server name.
    /// </summary>
    public string ServerName { get; set; } = "Konnektr Graph MCP Server";

    /// <summary>
    /// Gets or sets the MCP server version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the resource server URL (this MCP server's URL).
    /// Example: "https://{resource-id}.mcp.graph.konnektr.io"
    /// </summary>
    public string ResourceServerUrl { get; set; } = "";
}
