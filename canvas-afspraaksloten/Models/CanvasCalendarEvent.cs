using Newtonsoft.Json;

namespace CanvasAfspraaksloten.Models;

public class CanvasCalendarEvent
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("start_at")]
    public DateTimeOffset? StartAt { get; set; }

    [JsonProperty("end_at")]
    public DateTimeOffset? EndAt { get; set; }

    [JsonProperty("context_code")]
    public string? ContextCode { get; set; }

    [JsonProperty("workflow_state")]
    public string? WorkflowState { get; set; }

    [JsonProperty("appointment_group_id")]
    public long? AppointmentGroupId { get; set; }

    [JsonProperty("parent_event_id")]
    public long? ParentEventId { get; set; }

    [JsonProperty("child_events_count")]
    public int? ChildEventsCount { get; set; }

    [JsonProperty("child_events")]
    public List<CanvasCalendarEvent>? ChildEvents { get; set; }

    [JsonProperty("user")]
    public CanvasUser? User { get; set; }
}
