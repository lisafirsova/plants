namespace Plants.Models;

public sealed class PlantSpecies
{
    public int Id { get; set; }
    public int PlantTypeId { get; set; }
    public string PlantTypeName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LatinName { get; set; } = string.Empty;
    public string CareDescription { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(LatinName)
        ? Name
        : $"{Name} ({LatinName})";
}
