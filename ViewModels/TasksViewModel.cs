using System.Collections.ObjectModel;
using System.Windows.Input;
using Plants.Helpers;
using Plants.Models;
using Plants.Services;

namespace Plants.ViewModels;

public class TasksViewModel : BaseViewModel
{
    private readonly ITaskService _taskService;

    public TasksViewModel(ITaskService taskService)
    {
        _taskService = taskService;
        Tasks = [];
        MarkDoneCommand = new RelayCommand<CareTask>(task => UpdateStatus(task, CareTaskStatus.Done));
        MarkSkippedCommand = new RelayCommand<CareTask>(task => UpdateStatus(task, CareTaskStatus.Skipped));
        ReopenCommand = new RelayCommand<CareTask>(task => UpdateStatus(task, CareTaskStatus.Planned));
        LoadTasks();
    }

    public ObservableCollection<CareTask> Tasks { get; }
    public ICommand MarkDoneCommand { get; }
    public ICommand MarkSkippedCommand { get; }
    public ICommand ReopenCommand { get; }

    private void LoadTasks()
    {
        Tasks.Clear();
        foreach (var task in _taskService.GetTasks())
        {
            Tasks.Add(task);
        }
    }

    private void UpdateStatus(CareTask? task, CareTaskStatus status)
    {
        if (task is null)
        {
            return;
        }

        _taskService.UpdateTaskStatus(task.Id, status);
        LoadTasks();
    }
}
