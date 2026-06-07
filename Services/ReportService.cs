using System.Security;
using System.Text;
using Plants.Models;
using Plants.Services.Database;

namespace Plants.Services;

public sealed class ReportService
{
    private readonly DatabaseService _database;

    public ReportService(DatabaseService database)
    {
        _database = database;
    }

    public async Task<byte[]> GenerateExcelWorkbookAsync(int? userId, DateTime month)
    {
        var sheets = await GetReportSheetsAsync(userId, month);
        return Encoding.UTF8.GetBytes(BuildSpreadsheetXml(sheets));
    }

    public async Task<IReadOnlyList<ReportSheet>> GetReportSheetsAsync(int? userId, DateTime month)
    {
        var allPlants = userId is null
            ? await _database.GetPlantsAsync()
            : await _database.GetPlantsByUserAsync(userId.Value);
        var plantIds = allPlants.Select(x => x.Id).ToHashSet();
        var schedules = (await _database.GetSchedulesAsync())
            .Where(x => plantIds.Contains(x.PlantId))
            .ToList();
        var firstDay = new DateTime(month.Year, month.Month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        return new List<ReportSheet>
        {
            BuildMonthlyPlan(allPlants, schedules, firstDay, lastDay),
            await _database.GetIssueStatisticsReportAsync(userId, firstDay, lastDay),
            await BuildGrowthReportAsync(allPlants),
            BuildCategoryReport(allPlants),
            BuildActivePlantReport(allPlants)
        };
    }

    private static ReportSheet BuildMonthlyPlan(
        IReadOnlyList<Plant> plants,
        IReadOnlyList<WateringSchedule> schedules,
        DateTime firstDay,
        DateTime lastDay)
    {
        var plantMap = plants.ToDictionary(x => x.Id);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var schedule in schedules)
        {
            var date = schedule.StartDate.Date;
            while (date < firstDay)
            {
                date = date.AddDays(Math.Max(1, schedule.PeriodDays));
            }
            while (date <= lastDay)
            {
                if (plantMap.TryGetValue(schedule.PlantId, out var plant))
                {
                    rows.Add([
                        date.ToString("dd.MM.yyyy"),
                        plant.Name,
                        CareName(schedule.Type),
                        schedule.NotificationTime.ToString(@"hh\:mm"),
                        $"Раз в {schedule.PeriodValue} {PeriodName(schedule.PeriodUnit)}"
                    ]);
                }
                date = date.AddDays(Math.Max(1, schedule.PeriodDays));
            }
        }
        return new ReportSheet(
            "План работ на месяц",
            ["Дата", "Растение", "Мероприятие", "Время", "Периодичность"],
            rows.OrderBy(x => DateTime.ParseExact(x[0], "dd.MM.yyyy", null)).ToList());
    }

    private async Task<ReportSheet> BuildGrowthReportAsync(IReadOnlyList<Plant> plants)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var plant in plants)
        {
            var photos = await _database.GetPhotosAsync(plant.Id);
            var first = photos.LastOrDefault()?.DateTaken;
            var last = photos.FirstOrDefault()?.DateTaken;
            var observationDays = first is null || last is null ? 0 : (last.Value.Date - first.Value.Date).Days;
            var floweringMarks = photos.Count(x =>
                x.Title.Contains("цвет", StringComparison.OrdinalIgnoreCase) ||
                x.Title.Contains("бутон", StringComparison.OrdinalIgnoreCase));
            var observationsPerMonth = observationDays <= 0
                ? photos.Count
                : Math.Round(photos.Count / Math.Max(1d, observationDays / 30d), 1);
            var summary = photos.Count switch
            {
                0 => "Для анализа динамики добавьте фотографии растения.",
                1 => "Есть одна точка наблюдения; для сравнения требуется следующее фото.",
                _ when floweringMarks > 0 => $"Зафиксированы признаки цветения на {floweringMarks} фото. Сравните размеры и состояние растения на первом и последнем снимке.",
                _ => "Фотоархив позволяет сравнить внешний объём кроны и состояние листьев на первом и последнем снимке."
            };
            rows.Add([
                plant.Name,
                photos.Count.ToString(),
                first?.ToString("dd.MM.yyyy") ?? "Нет фото",
                last?.ToString("dd.MM.yyyy") ?? "Нет фото",
                observationDays.ToString(),
                observationsPerMonth.ToString("0.0"),
                floweringMarks.ToString(),
                summary,
                plant.Notes
            ]);
        }
        return new ReportSheet(
            "Динамика роста",
            ["Растение", "Количество фото", "Первое фото", "Последнее фото", "Период наблюдения, дней",
             "Фото в месяц", "Фото с цветением", "Аналитическая справка", "Заметки"],
            rows);
    }

    private static ReportSheet BuildCategoryReport(IReadOnlyList<Plant> plants) =>
        new(
            "Количество по категориям",
            ["Категория", "Количество"],
            plants.GroupBy(x => x.Type)
                .OrderBy(x => x.Key)
                .Select(x => (IReadOnlyList<string>)[x.Key, x.Count().ToString()])
                .ToList());

    private static ReportSheet BuildActivePlantReport(IReadOnlyList<Plant> plants) =>
        new(
            "Активные посадки",
            ["Название", "Категория", "Латинское название", "Фаза", "Состояние", "Расположение", "Заметки"],
            plants.OrderBy(x => x.Name)
                .Select(x => (IReadOnlyList<string>)[
                    x.Name, x.Type, x.LatinName, x.LifecyclePhase,
                    x.HealthStatus, x.Location, x.Notes])
                .ToList());

    private static string BuildSpreadsheetXml(IReadOnlyList<ReportSheet> sheets)
    {
        var xml = new StringBuilder();
        xml.AppendLine("""<?xml version="1.0" encoding="utf-8"?>""");
        xml.AppendLine("""<?mso-application progid="Excel.Sheet"?>""");
        xml.AppendLine("""<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">""");
        foreach (var sheet in sheets)
        {
            xml.AppendLine($"""<Worksheet ss:Name="{Escape(TrimSheetName(sheet.Name))}"><Table>""");
            AppendRow(xml, sheet.Headers);
            foreach (var row in sheet.Rows)
            {
                AppendRow(xml, row);
            }
            xml.AppendLine("</Table></Worksheet>");
        }
        xml.AppendLine("</Workbook>");
        return xml.ToString();
    }

    private static void AppendRow(StringBuilder xml, IReadOnlyList<string> values)
    {
        xml.Append("<Row>");
        foreach (var value in values)
        {
            xml.Append($"""<Cell><Data ss:Type="String">{Escape(value)}</Data></Cell>""");
        }
        xml.AppendLine("</Row>");
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
    private static string TrimSheetName(string value) => value.Length > 31 ? value[..31] : value;
    private static string CareName(string type) => type switch
    {
        "Fertilizer" => "Подкормка",
        "Pruning" => "Обрезка",
        "Repotting" => "Пересадка",
        _ => "Полив"
    };
    private static string PeriodName(string unit) => unit switch
    {
        "week" => "неделю",
        "month" => "месяц",
        _ => "день"
    };
}
