using Newtonsoft.Json;

namespace CanvasAfwezighedenVrijstellingen.Models;

public class CanvasAppointmentGroup
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("location_name")]
    public string? LocationName { get; set; }

    [JsonProperty("context_codes")]
    public List<string>? ContextCodes { get; set; }

    [JsonProperty("workflow_state")]
    public string? WorkflowState { get; set; }

    [JsonProperty("appointments")]
    public List<CanvasCalendarEvent>? Appointments { get; set; }
}
