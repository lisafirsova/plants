namespace Plants.Models;

public class PlantPhoto
{    public int Id { get; set; }    public int PlantId { get; set; }
    public string Caption { get; set; } = string.Empty;
    public DateTime TakenAt { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string PlaceholderColor { get; set; } = "#2F4858";    public string TakenAtText => TakenAt.ToString("dd.MM.yyyy");
}

