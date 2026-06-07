namespace Plants.Models;

public class Photo
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime DateTaken { get; set; }
}
