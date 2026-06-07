namespace Plants.Models;

public sealed class IssueObservation
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public int ReferenceItemId { get; set; }
    public DateTime ObservedAt { get; set; }
    public string Note { get; set; } = string.Empty;
}
