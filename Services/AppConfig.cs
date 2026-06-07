using System.Reflection;

namespace Plants.Services;

public static class AppConfig
{
    public static string ApiBaseUrl { get; } =
        ReadMetadata("PlantsApiBaseUrl", "http://10.0.2.2:5079").TrimEnd('/');

    public static string AppKey { get; } = ReadMetadata("PlantsAppKey", string.Empty);

    public static bool IsLocalEmulator =>
        ApiBaseUrl.Contains("10.0.2.2", StringComparison.OrdinalIgnoreCase) ||
        ApiBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);

    private static string ReadMetadata(string key, string fallback)
    {
        var value = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal))
            ?.Value;
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
