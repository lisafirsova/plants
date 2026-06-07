using System.Windows.Input;
using Plants.Helpers;
using Plants.Services;

namespace Plants.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IPlantService _plantService;
    private readonly ITaskService _taskService;
    private readonly IPestService _pestService;

    public MainViewModel(IPlantService plantService, ITaskService taskService, IPestService pestService)
    {
        _plantService = plantService;
        _taskService = taskService;
        _pestService = pestService;
        GoToPlantsCommand = new RelayCommand(() => NavigationRequested?.Invoke("plants"));
        GoToTasksCommand = new RelayCommand(() => NavigationRequested?.Invoke("tasks"));
        GoToLibraryCommand = new RelayCommand(() => NavigationRequested?.Invoke("library"));
    }

    public event Action<string>? NavigationRequested;

    public int PlantCount => _plantService.GetPlants().Count;
    public int PlannedTaskCount => _taskService.GetTasks().Count(task => task.Status == Models.CareTaskStatus.Planned);
    public int LibraryCount => _pestService.GetPests().Count + _pestService.GetDiseases().Count;

    public ICommand GoToPlantsCommand { get; }
    public ICommand GoToTasksCommand { get; }
    public ICommand GoToLibraryCommand { get; }
}
