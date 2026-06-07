namespace Plants.Models;

public sealed class CareEvent
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public int PlantId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public CareTaskStatus Status { get; set; } = CareTaskStatus.Planned;
    public string Note { get; set; } = string.Empty;
}
