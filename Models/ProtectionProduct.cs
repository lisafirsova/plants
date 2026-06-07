namespace Plants.Models;

public sealed class ProtectionProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ActiveIngredient { get; set; } = string.Empty;
    public string ApplicationDescription { get; set; } = string.Empty;
    public string HazardClass { get; set; } = string.Empty;
}
