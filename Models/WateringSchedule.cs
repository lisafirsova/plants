namespace Plants.Models;

public class WateringSchedule
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public DateTime StartDate { get; set; }
    public int PeriodValue { get; set; } = 1;
    public string PeriodUnit { get; set; } = "day";
    public int PeriodDays { get; set; }
    public TimeSpan NotificationTime { get; set; }
    public string Type { get; set; } = "Watering";
    public DateTime? LastCompletedDate { get; set; }
}
