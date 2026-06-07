namespace Plants.Models;

public class CareTask
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public string PlantName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public CareTaskStatus Status { get; set; }
    public string DueDateText => DueDate.ToString("dd.MM.yyyy");
    public string StatusText => Status switch
    {
        CareTaskStatus.Done => "Выполнено",
        CareTaskStatus.Skipped => "Пропущено",
        _ => "Запланировано"
    };
}

