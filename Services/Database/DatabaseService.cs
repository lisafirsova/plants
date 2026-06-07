using System.Net.Http.Json;
using System.Text.Json;
using Plants.Models;

namespace Plants.Services.Database;

public sealed class DatabaseService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static DatabaseService Instance { get; } = new();

    private DatabaseService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(45)
        };
        if (!string.IsNullOrWhiteSpace(AppConfig.AppKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Plants-App-Key", AppConfig.AppKey);
        }
    }

    public async Task InitializeAsync() =>
        _ = await SendAsync<bool>("initialize", new { });

    public Task<List<User>> GetUsersAsync() =>
        SendAsync<List<User>>("users.list", new { });

    public Task<User?> GetUserAsync(int id) =>
        SendNullableAsync<User>("users.get", new { id });

    public Task<User?> GetUserByGoogleIdAsync(string googleId) =>
        SendNullableAsync<User>("users.by-google-id", new { value = googleId });

    public async Task<int> AddUserAsync(User user)
    {
        user.Id = await SendAsync<int>("users.add", user);
        return user.Id;
    }

    public Task<int> UpdateUserAsync(User user) =>
        SendAsync<int>("users.update", user);

    public Task<int> DeleteUserAsync(int id) =>
        SendAsync<int>("users.delete", new { id });

    public Task<List<Plant>> GetPlantsAsync() =>
        SendAsync<List<Plant>>("plants.list", new { });

    public Task<List<Plant>> GetPlantsByUserAsync(int userId) =>
        SendAsync<List<Plant>>("plants.by-user", new { id = userId });

    public Task<Plant?> GetPlantAsync(int id) =>
        SendNullableAsync<Plant>("plants.get", new { id });

    public async Task<int> AddPlantAsync(Plant plant)
    {
        plant.Id = await SendAsync<int>("plants.add", plant);
        return plant.Id;
    }

    public Task<int> UpdatePlantAsync(Plant plant) =>
        SendAsync<int>("plants.update", plant);

    public Task<int> DeletePlantAsync(int id) =>
        SendAsync<int>("plants.delete", new { id });

    public Task<List<PlantSpecies>> GetPlantSpeciesAsync(string? plantType = null) =>
        SendAsync<List<PlantSpecies>>("species.list", new { value = plantType });

    public async Task<int> AddPlantSpeciesAsync(PlantSpecies species)
    {
        species.Id = await SendAsync<int>("species.add", species);
        return species.Id;
    }

    public Task<int> UpdatePlantSpeciesAsync(PlantSpecies species) =>
        SendAsync<int>("species.update", species);

    public Task<int> DeletePlantSpeciesAsync(int id) =>
        SendAsync<int>("species.delete", new { id });

    public Task<List<WateringSchedule>> GetSchedulesAsync() =>
        SendAsync<List<WateringSchedule>>("schedules.list", new { });

    public Task<List<WateringSchedule>> GetSchedulesForPlantAsync(int plantId) =>
        SendAsync<List<WateringSchedule>>("schedules.by-plant", new { id = plantId });

    public Task<WateringSchedule?> GetScheduleAsync(int plantId, string type) =>
        SendNullableAsync<WateringSchedule>("schedules.get", new { plantId, type });

    public async Task<int> AddScheduleAsync(WateringSchedule schedule)
    {
        schedule.Id = await SendAsync<int>("schedules.add", schedule);
        return schedule.Id;
    }

    public Task<int> UpdateScheduleAsync(WateringSchedule schedule) =>
        SendAsync<int>("schedules.update", schedule);

    public Task<int> DeleteScheduleAsync(int id) =>
        SendAsync<int>("schedules.delete", new { id });

    public Task<CareEvent?> GetCareEventAsync(int scheduleId, DateTime scheduledDate) =>
        SendNullableAsync<CareEvent>("care-events.get", new { scheduleId, date = scheduledDate });

    public Task<List<CareEvent>> GetCareEventsAsync(int userId, DateTime from, DateTime to) =>
        SendAsync<List<CareEvent>>("care-events.list", new { userId, from, to });

    public async Task SaveCareEventAsync(CareEvent careEvent) =>
        _ = await SendAsync<bool>("care-events.save", careEvent);

    public async Task DeleteCareEventAsync(int scheduleId, DateTime scheduledDate) =>
        _ = await SendAsync<bool>("care-events.delete", new { scheduleId, date = scheduledDate });

    public async Task AddIssueObservationAsync(IssueObservation observation) =>
        observation.Id = await SendAsync<int>("observations.add", observation);

    public Task<Dictionary<string, decimal>> GetRecommendationSettingsAsync() =>
        SendAsync<Dictionary<string, decimal>>("recommendations.get", new { });

    public async Task SaveRecommendationSettingAsync(string code, decimal value) =>
        _ = await SendAsync<bool>("recommendations.save", new { code, value });

    public Task<ReportSheet> GetIssueStatisticsReportAsync(int? userId, DateTime from, DateTime to) =>
        SendAsync<ReportSheet>("reports.issues", new { userId, from, to });

    public Task<List<Photo>> GetPhotosAsync(int plantId) =>
        SendAsync<List<Photo>>("photos.list", new { id = plantId });

    public async Task<int> AddPhotoAsync(Photo photo)
    {
        photo.Id = await SendAsync<int>("photos.add", photo);
        return photo.Id;
    }

    public Task<int> UpdatePhotoAsync(Photo photo) =>
        SendAsync<int>("photos.update", photo);

    public Task<int> DeletePhotoAsync(int id) =>
        SendAsync<int>("photos.delete", new { id });

    public Task<List<ProtectionProduct>> GetProtectionProductsAsync() =>
        SendAsync<List<ProtectionProduct>>("protection-products.list", new { });

    public async Task<int> AddProtectionProductAsync(ProtectionProduct product)
    {
        product.Id = await SendAsync<int>("protection-products.add", product);
        return product.Id;
    }

    public Task<int> UpdateProtectionProductAsync(ProtectionProduct product) =>
        SendAsync<int>("protection-products.update", product);

    public Task<int> DeleteProtectionProductAsync(int id) =>
        SendAsync<int>("protection-products.delete", new { id });

    public Task<List<Pest>> GetPestsAsync(bool? isPest = null, string? plantType = null, string? query = null) =>
        SendAsync<List<Pest>>("reference.list", new { isPest, plantType, query });

    public Task<List<Pest>> GetPestsAsync() => GetPestsAsync(true);

    public Task<List<Pest>> GetDiseasesFromPestTableAsync() => GetPestsAsync(false);

    public Task<List<Pest>> GetAllPestRecordsAsync() => GetPestsAsync(null);

    public async Task<int> AddPestAsync(Pest pest)
    {
        pest.Id = await SendAsync<int>("reference.add", pest);
        return pest.Id;
    }

    public Task<int> UpdatePestAsync(Pest pest) =>
        SendAsync<int>("reference.update", pest);

    public Task<int> DeletePestAsync(int id) =>
        SendAsync<int>("reference.delete", new { id });

    private async Task<T> SendAsync<T>(string operation, object payload)
    {
        ApiEnvelope<T>? envelope;
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                $"/api/data/{operation}",
                payload,
                JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await ReadErrorAsync(response));
            }
            envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Сервер Plants недоступен по адресу {AppConfig.ApiBaseUrl}. Проверьте интернет и адрес API.",
                ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException(
                "Сервер Plants не ответил вовремя. Повторите действие.",
                ex);
        }

        return envelope is null
            ? throw new InvalidOperationException("Сервер Plants вернул пустой ответ.")
            : envelope.Data;
    }

    private async Task<T?> SendNullableAsync<T>(string operation, object payload)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                $"/api/data/{operation}",
                payload,
                JsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await ReadErrorAsync(response));
            }
            var envelope = await response.Content.ReadFromJsonAsync<NullableApiEnvelope<T>>(JsonOptions);
            return envelope is null ? default : envelope.Data;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Сервер Plants недоступен по адресу {AppConfig.ApiBaseUrl}. Проверьте интернет и адрес API.",
                ex);
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString() ?? text;
            }
        }
        catch (JsonException)
        {
        }
        return $"Сервер вернул ошибку {(int)response.StatusCode}: {text}";
    }
}

public sealed class ApiEnvelope<T>
{
    public T Data { get; set; } = default!;
}

public sealed class NullableApiEnvelope<T>
{
    public T? Data { get; set; }
}
