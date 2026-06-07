using System.Net.Http.Json;
using System.Text.Json;
using Plants.Models;
using Plants.Services.Database;

namespace Plants.Services;

public sealed class AiCareService
{
    private readonly DatabaseService _database;
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(AppConfig.ApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(90)
    };

    public AiCareService(DatabaseService database)
    {
        _database = database;
        if (!string.IsNullOrWhiteSpace(AppConfig.AppKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Plants-App-Key", AppConfig.AppKey);
        }
    }

    public async Task<string> AskAsync(
        IReadOnlyList<Plant> plants,
        IReadOnlyList<AiChatMessage> history,
        string question,
        string? imagePath)
    {
        var request = new AiChatRequest
        {
            Question = question,
            Messages = history.TakeLast(12)
                .Select(x => new AiChatHistoryItem
                {
                    Role = x.IsUser ? "user" : "assistant",
                    Text = x.Text
                })
                .ToList()
        };

        foreach (var plant in plants)
        {
            var schedules = await _database.GetSchedulesForPlantAsync(plant.Id);
            var photos = await _database.GetPhotosAsync(plant.Id);
            request.Plants.Add(new AiPlantContext
            {
                Name = plant.Name,
                Category = plant.Type,
                LatinName = plant.LatinName,
                LifecyclePhase = plant.LifecyclePhase,
                HealthStatus = plant.HealthStatus,
                Notes = plant.Notes,
                PhotoCount = photos.Count,
                LastPhotoDate = photos.FirstOrDefault()?.DateTaken,
                Schedules = schedules.Select(x => new AiScheduleContext
                {
                    Type = x.Type,
                    NextDate = x.StartDate,
                    PeriodDays = x.PeriodDays,
                    LastCompletedDate = x.LastCompletedDate
                }).ToList()
            });
        }

        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            var bytes = await File.ReadAllBytesAsync(imagePath);
            if (bytes.Length > 10 * 1024 * 1024)
            {
                throw new InvalidOperationException("Фотография больше 10 МБ. Выберите изображение меньшего размера.");
            }
            request.ImageBase64 = Convert.ToBase64String(bytes);
            request.ImageMediaType = DetectMediaType(imagePath);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/api/ai/chat", request);
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException(
                $"Не удалось подключиться к ИИ-серверу {AppConfig.ApiBaseUrl}. Проверьте интернет и доступность сервера.");
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("ИИ-сервер не ответил вовремя. Повторите запрос.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"ИИ-сервер вернул ошибку {(int)response.StatusCode}: {ExtractError(error)}");
            }
            var result = await response.Content.ReadFromJsonAsync<AiChatResponse>();
            return string.IsNullOrWhiteSpace(result?.Answer)
                ? throw new InvalidOperationException("ИИ не вернул ответ.")
                : result.Answer;
        }
    }

    private static string ExtractError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("detail", out var detail)
                ? detail.GetString() ?? json
                : json;
        }
        catch
        {
            return json;
        }
    }

    private static string DetectMediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}

public sealed class AiChatRequest
{
    public string Question { get; set; } = string.Empty;
    public List<AiChatHistoryItem> Messages { get; set; } = [];
    public List<AiPlantContext> Plants { get; set; } = [];
    public string? ImageBase64 { get; set; }
    public string? ImageMediaType { get; set; }
}

public sealed class AiChatHistoryItem
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class AiPlantContext
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string LatinName { get; set; } = string.Empty;
    public string LifecyclePhase { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int PhotoCount { get; set; }
    public DateTime? LastPhotoDate { get; set; }
    public List<AiScheduleContext> Schedules { get; set; } = [];
}

public sealed class AiScheduleContext
{
    public string Type { get; set; } = string.Empty;
    public DateTime NextDate { get; set; }
    public int PeriodDays { get; set; }
    public DateTime? LastCompletedDate { get; set; }
}

public sealed class AiChatResponse
{
    public string Answer { get; set; } = string.Empty;
}
