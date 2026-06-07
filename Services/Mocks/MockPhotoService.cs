using Plants.Models;

namespace Plants.Services.Mocks;

public class MockPhotoService : IPhotoService
{
    private readonly List<PlantPhoto> _photos =
    [
        new PlantPhoto { Id = 1, PlantId = 1, Caption = "Новый лист раскрылся", TakenAt = DateTime.Today.AddDays(-4), PlaceholderColor = "#2D6A4F" },
        new PlantPhoto { Id = 2, PlantId = 1, Caption = "После пересадки", TakenAt = DateTime.Today.AddDays(-22), PlaceholderColor = "#40916C" },
        new PlantPhoto { Id = 3, PlantId = 2, Caption = "Проверка листьев", TakenAt = DateTime.Today.AddDays(-3), PlaceholderColor = "#52796F" },
        new PlantPhoto { Id = 4, PlantId = 3, Caption = "Сухой период", TakenAt = DateTime.Today.AddDays(-12), PlaceholderColor = "#84A98C" },
        new PlantPhoto { Id = 5, PlantId = 4, Caption = "Первый бутон", TakenAt = DateTime.Today.AddDays(-1), PlaceholderColor = "#B7E4C7" }
    ];

    public IReadOnlyList<PlantPhoto> GetPhotos(int plantId)
    {
        // TODO: подключить API или облачное хранилище для загрузки фото растения.
        return _photos
            .Where(photo => photo.PlantId == plantId)
            .OrderByDescending(photo => photo.TakenAt)
            .ToList();
    }

    public PlantPhoto AddPhoto(PlantPhoto photo)
    {
        // TODO: подключить API для отправки фото и метаданных.
        photo.Id = _photos.Count == 0 ? 1 : _photos.Max(item => item.Id) + 1;
        _photos.Add(photo);
        return photo;
    }
}
