namespace Plants.Models;

public class Pest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TreatmentDescription { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public bool IsPest { get; set; }
    public List<string> PlantTypes { get; set; } = [];

    public string PlantTypesLabel => PlantTypes.Count == 0
        ? "Все растения"
        : string.Join(", ", PlantTypes);
}
