using Plants.Models;

namespace Plants.Services.Mocks;

public class MockPlantService : IPlantService
{
    private readonly List<Plant> _plants =
    [
        new Plant
        {
            Id = 1,
            Name = "Монстера",
            LatinName = "Monstera deliciosa",
            Type = "Декоративно-лиственное",
            Location = "Гостиная, восточное окно",
            LastWatered = DateTime.Today.AddDays(-2),
            WateringHint = "Проверить верхние 3 см грунта",
            HealthStatus = "Здоровое",
            Notes = "Любит опору и регулярное протирание листьев.",
            AccentColor = "#6DD6A3"
        },
        new Plant
        {
            Id = 2,
            Name = "Фикус",
            LatinName = "Ficus elastica",
            Type = "Декоративно-лиственное",
            Location = "Кабинет",
            LastWatered = DateTime.Today.AddDays(-5),
            WateringHint = "Полив умеренный после просушки",
            HealthStatus = "Нужен осмотр",
            Notes = "Не любит перестановки и сквозняки.",
            AccentColor = "#8BC34A"
        },
        new Plant
        {
            Id = 3,
            Name = "Кактус",
            LatinName = "Mammillaria",
            Type = "Суккулент",
            Location = "Подоконник кухни",
            LastWatered = DateTime.Today.AddDays(-18),
            WateringHint = "Редкий полив, только сухой грунт",
            HealthStatus = "Здоровое",
            Notes = "Зимой держать почти сухим.",
            AccentColor = "#A5D6A7"
        },
        new Plant
        {
            Id = 4,
            Name = "Спатифиллум",
            LatinName = "Spathiphyllum wallisii",
            Type = "Цветущее",
            Location = "Спальня",
            LastWatered = DateTime.Today.AddDays(-1),
            WateringHint = "Поддерживать легкую влажность",
            HealthStatus = "Здоровое",
            Notes = "Опускает листья, когда хочет пить.",
            AccentColor = "#B7E4C7"
        },
        new Plant
        {
            Id = 5,
            Name = "Сансевиерия",
            LatinName = "Dracaena trifasciata",
            Type = "Суккулент",
            Location = "Прихожая",
            LastWatered = DateTime.Today.AddDays(-12),
            WateringHint = "Избегать воды в розетке",
            HealthStatus = "Здоровое",
            Notes = "Хорошо переносит полутень.",
            AccentColor = "#95D5B2"
        }
    ];

    public IReadOnlyList<Plant> GetPlants()
    {
        // TODO: подключить API для загрузки растений пользователя.
        return _plants.OrderBy(plant => plant.Name).ToList();
    }

    public Plant AddPlant(Plant plant)
    {
        // TODO: подключить API для создания растения на сервере.
        plant.Id = _plants.Count == 0 ? 1 : _plants.Max(item => item.Id) + 1;
        _plants.Add(plant);
        return plant;
    }

    public Plant UpdatePlant(Plant plant)
    {
        // TODO: подключить API для сохранения изменений растения.
        var index = _plants.FindIndex(item => item.Id == plant.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Растение не найдено.");
        }

        _plants[index] = plant;
        return plant;
    }

    public void DeletePlant(int plantId)
    {
        // TODO: подключить API для удаления растения.
        var plant = _plants.FirstOrDefault(item => item.Id == plantId);
        if (plant is not null)
        {
            _plants.Remove(plant);
        }
    }
}
