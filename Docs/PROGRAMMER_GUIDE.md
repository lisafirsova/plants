# Руководство программиста Plants

## Технологии

- .NET 10 for Android, C#.
- PostgreSQL и Npgsql.
- Google Sign-In для Android.
- Android AlarmManager и BroadcastReceiver для локальных уведомлений.
- Gemini API через отдельный ASP.NET Core proxy; OpenAI доступен как необязательный резервный провайдер.

## База данных

Физическая схема находится в `Database/schema_3nf_postgresql.sql`. Миграция существующей базы находится в `Database/migration_2026_06_06.sql`.

Основные связи:

- `app_users 1:N app_plants`;
- `app_plant_types 1:N app_plant_species`;
- `app_plants 1:N app_watering_schedules`;
- `app_watering_schedules 1:N app_care_events`;
- `app_plants 1:N app_photos`;
- `app_reference_categories 1:N app_reference_items`;
- `app_reference_items M:N app_plant_types`;
- `app_plants 1:N app_issue_observations`.

Зависимые данные удаляются каскадно. Вид растения при удалении заменяется в карточке на `NULL`, чтобы не потерять пользовательское растение.

## AI proxy

Мобильный клиент отправляет вопрос, последние сообщения, контекст растений и выбранную фотографию в `POST /api/ai/chat` сервера `Plants.AiProxy`. Сервер вызывает Gemini `generateContent`, передавая текст и изображение. API key не хранится в APK.

Переменные среды:

```powershell
$env:GEMINI_API_KEY="ВАШ_КЛЮЧ"
$env:GEMINI_MODEL="gemini-2.5-flash"
dotnet run --project .\Backend\Plants.AiProxy\Plants.AiProxy.csproj
```

API key должен храниться только на сервере. Для production настройте HTTPS, аутентификацию мобильного клиента, ограничение размера запросов, rate limiting и секрет-хранилище.

## Отчёты

`ReportService` формирует SpreadsheetML, который открывается Microsoft Excel. Книга содержит:

1. план работ на выбранный месяц;
2. статистику болезней и вредителей;
3. динамику фотофиксации и признаки цветения;
4. количество растений по категориям;
5. активные посадки с фазой и состоянием.

## Сборка и проверка

```powershell
dotnet build Plants.csproj
dotnet build .\Backend\Plants.AiProxy\Plants.AiProxy.csproj
```

Для эмулятора PostgreSQL и AI proxy доступны через адрес хоста `10.0.2.2`. Для физического устройства используются HTTPS-адреса серверов либо IP компьютера в одной локальной сети.
