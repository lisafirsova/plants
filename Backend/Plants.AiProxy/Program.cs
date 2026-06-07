using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Plants.AiProxy.Data;
using Plants.Services.Database;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ServerDatabaseService>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    var expectedKey = Environment.GetEnvironmentVariable("PLANTS_APP_KEY");
    if (!string.IsNullOrWhiteSpace(expectedKey))
    {
        var suppliedKey = context.Request.Headers["X-Plants-App-Key"].ToString();
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(suppliedKey),
                Encoding.UTF8.GetBytes(expectedKey)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                detail = "Мобильное приложение не авторизовано для обращения к серверу."
            });
            return;
        }
    }

    await next();
});

app.MapPlantsDataApi();

app.MapPost("/api/ai/care-advice", async (
    AiCareRequest request,
    IHttpClientFactory clientFactory,
    CancellationToken cancellationToken) =>
{
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem(
            "На сервере не задана переменная окружения OPENAI_API_KEY.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5.4-mini";
    var prompt = BuildPrompt(request);
    var content = new List<object>
    {
        new { type = "input_text", text = prompt }
    };
    if (!string.IsNullOrWhiteSpace(request.ImageBase64))
    {
        content.Add(new
        {
            type = "input_image",
            image_url = $"data:{request.ImageMediaType ?? "image/jpeg"};base64,{request.ImageBase64}"
        });
    }

    var payload = new
    {
        model,
        input = new[]
        {
            new
            {
                role = "user",
                content
            }
        },
        text = new
        {
            format = new
            {
                type = "json_schema",
                name = "plant_care_recommendations",
                strict = true,
                schema = new
                {
                    type = "object",
                    properties = new
                    {
                        recommendations = new
                        {
                            type = "array",
                            minItems = 1,
                            maxItems = 8,
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    title = new { type = "string" },
                                    text = new { type = "string" },
                                    priority = new
                                    {
                                        type = "string",
                                        @enum = new[] { "Высокий", "Средний", "Низкий" }
                                    }
                                },
                                required = new[] { "title", "text", "priority" },
                                additionalProperties = false
                            }
                        }
                    },
                    required = new[] { "recommendations" },
                    additionalProperties = false
                }
            }
        }
    };

    var client = clientFactory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    using var response = await client.PostAsync(
        "https://api.openai.com/v1/responses",
        new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        cancellationToken);
    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(
            $"OpenAI API вернул ошибку {(int)response.StatusCode}: {responseJson}",
            statusCode: StatusCodes.Status502BadGateway);
    }

    using var document = JsonDocument.Parse(responseJson);
    var outputText = FindOutputText(document.RootElement);
    if (string.IsNullOrWhiteSpace(outputText))
    {
        return Results.Problem("OpenAI API не вернул текст рекомендации.", statusCode: 502);
    }

    var result = JsonSerializer.Deserialize<AiCareResponse>(
        outputText,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    return result is null
        ? Results.Problem("Не удалось разобрать ответ ИИ.", statusCode: 502)
        : Results.Ok(result);
});

app.MapPost("/api/ai/chat", async (
    AiChatRequest request,
    IHttpClientFactory clientFactory,
    CancellationToken cancellationToken) =>
{
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem(
            "На сервере не задана переменная окружения OPENAI_API_KEY.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    if (string.IsNullOrWhiteSpace(request.Question) && string.IsNullOrWhiteSpace(request.ImageBase64))
    {
        return Results.Problem("Передайте вопрос или фотографию.", statusCode: 400);
    }

    var content = new List<object>
    {
        new { type = "input_text", text = BuildChatPrompt(request) }
    };
    if (!string.IsNullOrWhiteSpace(request.ImageBase64))
    {
        content.Add(new
        {
            type = "input_image",
            image_url = $"data:{request.ImageMediaType ?? "image/jpeg"};base64,{request.ImageBase64}"
        });
    }

    var payload = new
    {
        model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5.4-mini",
        input = new[]
        {
            new
            {
                role = "user",
                content
            }
        }
    };
    var client = clientFactory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    using var response = await client.PostAsync(
        "https://api.openai.com/v1/responses",
        new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        cancellationToken);
    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(
            $"OpenAI API вернул ошибку {(int)response.StatusCode}: {ExtractOpenAiError(responseJson)}",
            statusCode: StatusCodes.Status502BadGateway);
    }

    using var document = JsonDocument.Parse(responseJson);
    var answer = FindOutputText(document.RootElement);
    return string.IsNullOrWhiteSpace(answer)
        ? Results.Problem("OpenAI API не вернул текст ответа.", statusCode: 502)
        : Results.Ok(new AiChatResponse { Answer = answer });
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "5079";
app.Run($"http://0.0.0.0:{port}");

static string BuildPrompt(AiCareRequest request)
{
    var plantsJson = JsonSerializer.Serialize(request.Plants);
    return $"""
        Ты эксперт по уходу за комнатными растениями. Проанализируй данные пользователя и,
        если приложено фото, визуальное состояние растения. Дай конкретные безопасные рекомендации:
        полив, подкормка, обрезка, пересадка, освещение и возможные признаки вредителей или болезней.
        Не ставь окончательный диагноз по одному фото. При серьёзных признаках советуй обратиться
        к специалисту. Отвечай только на русском языке по заданной JSON-схеме.

        Данные растений:
        {plantsJson}

        Название последнего фото: {request.ImageTitle ?? "не указано"}.
        """;
}

static string BuildChatPrompt(AiChatRequest request)
{
    var plants = JsonSerializer.Serialize(request.Plants);
    var history = string.Join(
        "\n",
        request.Messages.TakeLast(12).Select(x =>
            $"{(x.Role == "assistant" ? "ИИ" : "Пользователь")}: {x.Text}"));
    return $"""
        Ты русскоязычный консультант по комнатным растениям. Отвечай понятно, конкретно и
        доброжелательно. Используй карточки растений пользователя и историю диалога.

        Если приложено фото:
        - опиши только видимые признаки;
        - перечисли наиболее вероятные причины, но не выдавай предположение за точный диагноз;
        - оцени, нужны ли полив, подкормка, обрезка или пересадка;
        - предложи безопасные следующие шаги и признаки, при которых нужен специалист;
        - не советуй опасные смеси и не придумывай дозировку препаратов.

        Карточки растений:
        {plants}

        История:
        {history}

        Новый вопрос:
        {request.Question}
        """;
}

static string ExtractOpenAiError(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error) &&
            error.TryGetProperty("message", out var message))
        {
            return message.GetString() ?? json;
        }
    }
    catch
    {
    }
    return json;
}

static string? FindOutputText(JsonElement root)
{
    if (!root.TryGetProperty("output", out var output))
    {
        return null;
    }
    foreach (var item in output.EnumerateArray())
    {
        if (!item.TryGetProperty("content", out var content))
        {
            continue;
        }
        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("type", out var type) &&
                type.GetString() == "output_text" &&
                part.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }
        }
    }
    return null;
}

public sealed class AiCareRequest
{
    public List<AiPlantContext> Plants { get; set; } = [];
    public string? ImageBase64 { get; set; }
    public string? ImageMediaType { get; set; }
    public string? ImageTitle { get; set; }
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

public sealed class AiCareResponse
{
    public List<AiCareAdvice> Recommendations { get; set; } = [];
}

public sealed record AiCareAdvice(string Title, string Text, string Priority);

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

public sealed class AiChatResponse
{
    public string Answer { get; set; } = string.Empty;
}
