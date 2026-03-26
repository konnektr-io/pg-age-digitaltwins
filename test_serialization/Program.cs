using System;
using System.Text.Json;
using System.Text.Json.Nodes;

var opts = new JsonSerializerOptions
{
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
var dto = DateTimeOffset.UtcNow;
var obj = new
{
    uploadTime = dto,
    id = "dtmi:test;1",
    descendants = new[] { "dtmi:child1;1", "dtmi:child2;1" },
};

var step1 = JsonSerializer.Serialize(obj, opts);
Console.WriteLine($"Step 1 (relaxed): {step1}");

var node = JsonNode.Parse(step1);
var wrapper = new JsonObject { { "models", new JsonArray(node!) } };
var step3 = JsonSerializer.Serialize(wrapper);
Console.WriteLine($"Step 3 (default): {step3}");
Console.WriteLine($"Contains \\u002B: {step3.Contains("\\u002B")}");

// Compare: what if we use opts for the outer serialize too?
var step3WithOpts = JsonSerializer.Serialize(wrapper, opts);
Console.WriteLine($"Step 3 (relaxed): {step3WithOpts}");
Console.WriteLine($"Match: {step3 == step3WithOpts}");
