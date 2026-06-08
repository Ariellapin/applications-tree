using System.Text.Json.Serialization;

namespace WPFToolbarTree.Config;

public sealed class RootDto
{
    [JsonPropertyName("items")]
    public List<NodeDto> Items { get; set; } = new();
}

public sealed class NodeDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "item";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("children")]
    public List<NodeDto>? Children { get; set; }

    [JsonPropertyName("iconSource")]
    public string? IconSource { get; set; }
}
