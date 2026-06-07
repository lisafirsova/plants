using Plants.Models;

namespace Plants.Services;

public interface IPhotoService
{
    IReadOnlyList<PlantPhoto> GetPhotos(int plantId);
    PlantPhoto AddPhoto(PlantPhoto photo);
}
