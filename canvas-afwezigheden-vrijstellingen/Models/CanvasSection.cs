using Newtonsoft.Json;

namespace CanvasAfwezighedenVrijstellingen.Models;

public class CanvasSection
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }
}
