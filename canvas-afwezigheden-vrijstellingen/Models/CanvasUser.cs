using Newtonsoft.Json;

namespace CanvasAfwezighedenVrijstellingen.Models;

public class CanvasUser
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("sortable_name")]
    public string? SortableName { get; set; }

    [JsonProperty("login_id")]
    public string? LoginId { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }
}
