using Plants.Models;

namespace Plants.Services;

public interface IPlantService
{
    IReadOnlyList<Plant> GetPlants();
    Plant AddPlant(Plant plant);
    Plant UpdatePlant(Plant plant);
    void DeletePlant(int plantId);
}
