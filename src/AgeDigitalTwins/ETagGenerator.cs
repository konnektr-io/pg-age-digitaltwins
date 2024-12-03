using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AgeDigitalTwins;

public static class ETagGenerator
{
    public static string GenerateEtag(string digitalTwinId, DateTimeOffset lastUpdateTime)
    {
        string input = $"{digitalTwinId}-{lastUpdateTime:o}";
        byte[] hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        string hashGuid = new Guid(hashBytes.Take(16).ToArray()).ToString();
        return @$"W/""{hashGuid}""";
    }
}