using System.Collections.ObjectModel;
using System.Windows.Input;
using Plants.Helpers;
using Plants.Models;
using Plants.Services;

namespace Plants.ViewModels;

public class PlantDetailViewModel : BaseViewModel
{
    private readonly IPlantService _plantService;
    private readonly IPhotoService _photoService;
    private Plant? _plant;

    public PlantDetailViewModel(IPlantService plantService, IPhotoService photoService)
    {
        _plantService = plantService;
        _photoService = photoService;
        Photos = [];
        AddPhotoCommand = new RelayCommand(AddPhoto);
    }

    public Plant? Plant
    {
        get => _plant;
        set => SetProperty(ref _plant, value);
    }

    public ObservableCollection<PlantPhoto> Photos { get; }

    public ICommand AddPhotoCommand { get; }

    public void LoadPlant(int plantId)
    {
        Plant = _plantService.GetPlants().FirstOrDefault(plant => plant.Id == plantId);
        LoadPhotos(plantId);
    }

    private void LoadPhotos(int plantId)
    {
        Photos.Clear();
        foreach (var photo in _photoService.GetPhotos(plantId))
        {
            Photos.Add(photo);
        }
    }

    private void AddPhoto()
    {
        if (Plant is null)
        {
            return;
        }

        _photoService.AddPhoto(new PlantPhoto
        {
            PlantId = Plant.Id,
            Caption = $"Наблюдение от {DateTime.Today:dd.MM}",
            TakenAt = DateTime.Today,
            PlaceholderColor = Plant.AccentColor
        });

        LoadPhotos(Plant.Id);
    }
}
