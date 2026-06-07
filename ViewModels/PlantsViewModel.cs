using System.Collections.ObjectModel;
using System.Windows.Input;
using Plants.Helpers;
using Plants.Models;
using Plants.Services;

namespace Plants.ViewModels;

public class PlantsViewModel : BaseViewModel
{
    private readonly IPlantService _plantService;
    private bool _isEditing;
    private int _editingId;
    private string _name = string.Empty;
    private string _latinName = string.Empty;
    private string _type = string.Empty;
    private string _location = string.Empty;
    private string _wateringHint = string.Empty;
    private string _healthStatus = "Здоровое";
    private string _notes = string.Empty;

    public PlantsViewModel(IPlantService plantService)
    {
        _plantService = plantService;
        Plants = [];
        LoadPlants();

        NewPlantCommand = new RelayCommand(StartNewPlant);
        SavePlantCommand = new RelayCommand(SavePlant);
        CancelEditCommand = new RelayCommand(ClearEditor);
        EditPlantCommand = new RelayCommand<Plant>(StartEditPlant);
        DeletePlantCommand = new RelayCommand<Plant>(DeletePlant);
        OpenPlantCommand = new RelayCommand<Plant>(plant =>
        {
            if (plant is not null)
            {
                PlantRequested?.Invoke(plant.Id);
            }
        });
    }

    public event Action<int>? PlantRequested;
    public event Action<string, string>? AlertRequested;

    public ObservableCollection<Plant> Plants { get; }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string LatinName
    {
        get => _latinName;
        set => SetProperty(ref _latinName, value);
    }

    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Location
    {
        get => _location;
        set => SetProperty(ref _location, value);
    }

    public string WateringHint
    {
        get => _wateringHint;
        set => SetProperty(ref _wateringHint, value);
    }

    public string HealthStatus
    {
        get => _healthStatus;
        set => SetProperty(ref _healthStatus, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string EditorTitle => _editingId == 0 ? "Новое растение" : "Редактирование";

    public ICommand NewPlantCommand { get; }
    public ICommand SavePlantCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand EditPlantCommand { get; }
    public ICommand DeletePlantCommand { get; }
    public ICommand OpenPlantCommand { get; }

    private void LoadPlants()
    {
        Plants.Clear();
        foreach (var plant in _plantService.GetPlants())
        {
            Plants.Add(plant);
        }
    }

    private void StartNewPlant()
    {
        ClearEditor();
        IsEditing = true;
    }

    private void StartEditPlant(Plant? plant)
    {
        if (plant is null)
        {
            return;
        }

        _editingId = plant.Id;
        Name = plant.Name;
        LatinName = plant.LatinName;
        Type = plant.Type;
        Location = plant.Location;
        WateringHint = plant.WateringHint;
        HealthStatus = plant.HealthStatus;
        Notes = plant.Notes;
        IsEditing = true;
        OnPropertyChanged(nameof(EditorTitle));
    }

    private void SavePlant()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            AlertRequested?.Invoke("Не хватает данных", "Введите название растения.");
            return;
        }

        var plant = new Plant
        {
            Id = _editingId,
            Name = Name.Trim(),
            LatinName = LatinName.Trim(),
            Type = string.IsNullOrWhiteSpace(Type) ? "Комнатное растение" : Type.Trim(),
            Location = Location.Trim(),
            LastWatered = DateTime.Today,
            WateringHint = WateringHint.Trim(),
            HealthStatus = string.IsNullOrWhiteSpace(HealthStatus) ? "Здоровое" : HealthStatus.Trim(),
            Notes = Notes.Trim(),
            AccentColor = _editingId == 0 ? "#6DD6A3" : _plantService.GetPlants().First(item => item.Id == _editingId).AccentColor
        };

        if (_editingId == 0)
        {
            _plantService.AddPlant(plant);
        }
        else
        {
            _plantService.UpdatePlant(plant);
        }

        LoadPlants();
        ClearEditor();
    }

    private void DeletePlant(Plant? plant)
    {
        if (plant is null)
        {
            return;
        }

        _plantService.DeletePlant(plant.Id);
        LoadPlants();
    }

    private void ClearEditor()
    {
        _editingId = 0;
        Name = string.Empty;
        LatinName = string.Empty;
        Type = string.Empty;
        Location = string.Empty;
        WateringHint = string.Empty;
        HealthStatus = "Здоровое";
        Notes = string.Empty;
        IsEditing = false;
        OnPropertyChanged(nameof(EditorTitle));
    }
}
