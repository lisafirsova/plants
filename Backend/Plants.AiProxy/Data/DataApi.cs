using System.Text.Json;
using Plants.Models;
using Plants.Services.Database;

namespace Plants.AiProxy.Data;

public static class DataApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapPlantsDataApi(this WebApplication app)
    {
        app.MapPost("/api/data/{operation}", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        string operation,
        JsonElement body,
        ServerDatabaseService database)
    {
        try
        {
            object? result = operation switch
            {
                "initialize" => await InitializeAsync(database),
                "users.list" => await database.GetUsersAsync(),
                "users.get" => await database.GetUserAsync(Read<IdRequest>(body).Id),
                "users.by-google-id" => await database.GetUserByGoogleIdAsync(Read<StringRequest>(body).Value),
                "users.add" => await AddUserAsync(database, Read<User>(body)),
                "users.update" => await database.UpdateUserAsync(Read<User>(body)),
                "users.delete" => await database.DeleteUserAsync(Read<IdRequest>(body).Id),

                "plants.list" => await database.GetPlantsAsync(),
                "plants.by-user" => await database.GetPlantsByUserAsync(Read<IdRequest>(body).Id),
                "plants.get" => await database.GetPlantAsync(Read<IdRequest>(body).Id),
                "plants.add" => await AddPlantAsync(database, Read<Plant>(body)),
                "plants.update" => await database.UpdatePlantAsync(Read<Plant>(body)),
                "plants.delete" => await database.DeletePlantAsync(Read<IdRequest>(body).Id),

                "species.list" => await database.GetPlantSpeciesAsync(Read<OptionalStringRequest>(body).Value),
                "species.add" => await AddSpeciesAsync(database, Read<PlantSpecies>(body)),
                "species.update" => await database.UpdatePlantSpeciesAsync(Read<PlantSpecies>(body)),
                "species.delete" => await database.DeletePlantSpeciesAsync(Read<IdRequest>(body).Id),

                "schedules.list" => await database.GetSchedulesAsync(),
                "schedules.by-plant" => await database.GetSchedulesForPlantAsync(Read<IdRequest>(body).Id),
                "schedules.get" => await GetScheduleAsync(database, Read<ScheduleKeyRequest>(body)),
                "schedules.add" => await AddScheduleAsync(database, Read<WateringSchedule>(body)),
                "schedules.update" => await database.UpdateScheduleAsync(Read<WateringSchedule>(body)),
                "schedules.delete" => await database.DeleteScheduleAsync(Read<IdRequest>(body).Id),

                "care-events.get" => await GetCareEventAsync(database, Read<CareEventKeyRequest>(body)),
                "care-events.list" => await GetCareEventsAsync(database, Read<DateRangeRequest>(body)),
                "care-events.save" => await SaveCareEventAsync(database, Read<CareEvent>(body)),
                "care-events.delete" => await DeleteCareEventAsync(database, Read<CareEventKeyRequest>(body)),

                "observations.add" => await AddObservationAsync(database, Read<IssueObservation>(body)),
                "recommendations.get" => await database.GetRecommendationSettingsAsync(),
                "recommendations.save" => await SaveRecommendationAsync(database, Read<RecommendationRequest>(body)),
                "reports.issues" => await GetIssueReportAsync(database, Read<NullableUserDateRangeRequest>(body)),

                "photos.list" => await database.GetPhotosAsync(Read<IdRequest>(body).Id),
                "photos.add" => await AddPhotoAsync(database, Read<Photo>(body)),
                "photos.update" => await database.UpdatePhotoAsync(Read<Photo>(body)),
                "photos.delete" => await database.DeletePhotoAsync(Read<IdRequest>(body).Id),

                "protection-products.list" => await database.GetProtectionProductsAsync(),
                "protection-products.add" => await AddProtectionProductAsync(database, Read<ProtectionProduct>(body)),
                "protection-products.update" => await database.UpdateProtectionProductAsync(Read<ProtectionProduct>(body)),
                "protection-products.delete" => await database.DeleteProtectionProductAsync(Read<IdRequest>(body).Id),

                "reference.list" => await GetReferenceItemsAsync(database, Read<PestFilterRequest>(body)),
                "reference.add" => await AddPestAsync(database, Read<Pest>(body)),
                "reference.update" => await database.UpdatePestAsync(Read<Pest>(body)),
                "reference.delete" => await database.DeletePestAsync(Read<IdRequest>(body).Id),
                _ => throw new ArgumentException($"Неизвестная операция API: {operation}.")
            };

            return Results.Ok(new ApiEnvelope(result));
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                $"Ошибка обработки данных: {ex.Message}",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static T Read<T>(JsonElement body) =>
        body.Deserialize<T>(JsonOptions)
        ?? throw new ArgumentException("Тело запроса отсутствует или имеет неверный формат.");

    private static async Task<bool> InitializeAsync(ServerDatabaseService database)
    {
        await database.InitializeAsync();
        return true;
    }

    private static async Task<int> AddUserAsync(ServerDatabaseService database, User value)
    {
        await database.AddUserAsync(value);
        return value.Id;
    }

    private static async Task<int> AddPlantAsync(ServerDatabaseService database, Plant value)
    {
        await database.AddPlantAsync(value);
        return value.Id;
    }

    private static async Task<int> AddSpeciesAsync(ServerDatabaseService database, PlantSpecies value)
    {
        await database.AddPlantSpeciesAsync(value);
        return value.Id;
    }

    private static async Task<WateringSchedule?> GetScheduleAsync(
        ServerDatabaseService database,
        ScheduleKeyRequest request) =>
        await database.GetScheduleAsync(request.PlantId, request.Type);

    private static async Task<int> AddScheduleAsync(ServerDatabaseService database, WateringSchedule value)
    {
        await database.AddScheduleAsync(value);
        return value.Id;
    }

    private static async Task<CareEvent?> GetCareEventAsync(
        ServerDatabaseService database,
        CareEventKeyRequest request) =>
        await database.GetCareEventAsync(request.ScheduleId, request.Date);

    private static async Task<List<CareEvent>> GetCareEventsAsync(
        ServerDatabaseService database,
        DateRangeRequest request) =>
        await database.GetCareEventsAsync(request.UserId, request.From, request.To);

    private static async Task<bool> SaveCareEventAsync(ServerDatabaseService database, CareEvent value)
    {
        await database.SaveCareEventAsync(value);
        return true;
    }

    private static async Task<bool> DeleteCareEventAsync(
        ServerDatabaseService database,
        CareEventKeyRequest request)
    {
        await database.DeleteCareEventAsync(request.ScheduleId, request.Date);
        return true;
    }

    private static async Task<int> AddObservationAsync(
        ServerDatabaseService database,
        IssueObservation value)
    {
        await database.AddIssueObservationAsync(value);
        return value.Id;
    }

    private static async Task<bool> SaveRecommendationAsync(
        ServerDatabaseService database,
        RecommendationRequest request)
    {
        await database.SaveRecommendationSettingAsync(request.Code, request.Value);
        return true;
    }

    private static async Task<ReportSheet> GetIssueReportAsync(
        ServerDatabaseService database,
        NullableUserDateRangeRequest request) =>
        await database.GetIssueStatisticsReportAsync(request.UserId, request.From, request.To);

    private static async Task<int> AddPhotoAsync(ServerDatabaseService database, Photo value)
    {
        await database.AddPhotoAsync(value);
        return value.Id;
    }

    private static async Task<int> AddProtectionProductAsync(
        ServerDatabaseService database,
        ProtectionProduct value)
    {
        await database.AddProtectionProductAsync(value);
        return value.Id;
    }

    private static async Task<List<Pest>> GetReferenceItemsAsync(
        ServerDatabaseService database,
        PestFilterRequest request) =>
        await database.GetPestsAsync(request.IsPest, request.PlantType, request.Query);

    private static async Task<int> AddPestAsync(ServerDatabaseService database, Pest value)
    {
        await database.AddPestAsync(value);
        return value.Id;
    }
}

public sealed record ApiEnvelope(object? Data);
public sealed record IdRequest(int Id);
public sealed record StringRequest(string Value);
public sealed record OptionalStringRequest(string? Value);
public sealed record ScheduleKeyRequest(int PlantId, string Type);
public sealed record CareEventKeyRequest(int ScheduleId, DateTime Date);
public sealed record DateRangeRequest(int UserId, DateTime From, DateTime To);
public sealed record NullableUserDateRangeRequest(int? UserId, DateTime From, DateTime To);
public sealed record RecommendationRequest(string Code, decimal Value);
public sealed record PestFilterRequest(bool? IsPest, string? PlantType, string? Query);
