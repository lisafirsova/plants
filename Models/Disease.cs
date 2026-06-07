namespace Plants.Models;

public class Disease
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TreatmentDescription { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}
