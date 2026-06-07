namespace Plants.Models;

public class PestIssue
{    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDisease { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Symptoms { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public string Prevention { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#F4B860";
    public string ImagePath { get; set; } = string.Empty;    public string TreatmentDescription
    {
        get => Treatment;
        set => Treatment = value;
    }    public string TypeText => IsDisease ? "Болезнь" : "Вредитель";
}

