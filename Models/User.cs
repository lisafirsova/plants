namespace Plants.Models;

public class User
{
    public int Id { get; set; }
    public string GoogleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
