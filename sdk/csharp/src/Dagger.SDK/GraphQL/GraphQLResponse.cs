using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dagger.GraphQL;

public class GraphQLError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public List<string>? Path { get; set; }

    [JsonPropertyName("locations")]
    public List<GraphQLErrorLocation>? Locations { get; set; }

    [JsonPropertyName("extensions")]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>
    /// Gets the error type from extensions, if available.
    /// </summary>
    public string? ErrorType
    {
        get
        {
            return Extensions?.TryGetValue("_type", out var type) == true
        ? type.GetString()
        : null;
        }
    }
}

public class GraphQLErrorLocation
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }
}

public class GraphQLResponse
{
    [JsonPropertyName("errors")]
    public List<GraphQLError>? Errors { get; set; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}
