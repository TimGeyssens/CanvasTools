using Newtonsoft.Json;

namespace CanvasBamaflexExport.Models;

public class CanvasCourse
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("course_code")]
    public string? CourseCode { get; set; }

    [JsonProperty("workflow_state")]
    public string? WorkflowState { get; set; }
}
