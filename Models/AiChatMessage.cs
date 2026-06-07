namespace Plants.Models;

public sealed class AiChatMessage
{
    public bool IsUser { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
}
