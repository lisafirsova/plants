namespace Plants.Services.Database;

public static class ServerDatabaseConfig
{
    public static string ConnectionString
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("DATABASE_URL")
                        ?? Environment.GetEnvironmentVariable("PLANTS_DATABASE_CONNECTION")
                        ?? throw new InvalidOperationException(
                            "На сервере не задана переменная DATABASE_URL или PLANTS_DATABASE_CONNECTION.");
            return value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                ? ConvertPostgresUri(value)
                : value;
        }
    }

    private static string ConvertPostgresUri(string value)
    {
        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("DATABASE_URL не содержит имя пользователя и пароль.");
        }

        var database = uri.AbsolutePath.Trim('/');
        return string.Join(';',
            $"Host={uri.Host}",
            $"Port={(uri.IsDefaultPort ? 5432 : uri.Port)}",
            $"Database={Uri.UnescapeDataString(database)}",
            $"Username={Uri.UnescapeDataString(credentials[0])}",
            $"Password={Uri.UnescapeDataString(credentials[1])}",
            "SSL Mode=Require",
            "Trust Server Certificate=true",
            "Timeout=15",
            "Command Timeout=30");
    }
}
