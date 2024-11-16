using Microsoft.Extensions.Logging;

namespace AgeDigitalTwins;

public class AgeDigitalTwinsOptions
{
    public LoggerFactory? LoggerFactory { get; set; }
    public bool SuperUser { get; set; } = false;
    public string GraphName { get; set; } = "digitaltwins";
}