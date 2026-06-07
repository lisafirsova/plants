namespace Plants.Models;

public class Plant
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? SpeciesId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool WateringEnabled { get; set; }
    public bool FertilizerEnabled { get; set; }
    public string LatinName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime LastWatered { get; set; }
    public string WateringHint { get; set; } = string.Empty;
    public string LifecyclePhase { get; set; } = "Активный рост";
    public string HealthStatus { get; set; } = "Здорово";
    public string Notes { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#6DD6A3";
    public string LastWateredText => LastWatered.ToString("dd.MM.yyyy");
}
