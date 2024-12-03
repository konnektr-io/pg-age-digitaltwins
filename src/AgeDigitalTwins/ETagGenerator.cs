using System;
using System.Security.Cryptography;
using System.Text;

namespace AgeDigitalTwins;

public static class ETagGenerator
{
    public static string GenerateEtag(string digitalTwinId, DateTimeOffset lastUpdateTime)
    {
        string input = $"{digitalTwinId}-{lastUpdateTime:o}";
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        string hashString = Convert.ToHexStringLower(hashBytes);
        return $"W/\"{hashString}\"";
    }
}