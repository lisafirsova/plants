using System.Collections.ObjectModel;
using System.Windows.Input;
using Plants.Helpers;
using Plants.Models;
using Plants.Services;

namespace Plants.ViewModels;

public class PestLibraryViewModel : BaseViewModel
{
    private readonly IPestService _pestService;

    public PestLibraryViewModel(IPestService pestService)
    {
        _pestService = pestService;
        Pests = [];
        Diseases = [];
        OpenIssueCommand = new RelayCommand<PestIssue>(issue =>
        {
            if (issue is not null)
            {
                IssueRequested?.Invoke(issue.Id);
            }
        });
        LoadIssues();
    }

    public event Action<int>? IssueRequested;

    public ObservableCollection<PestIssue> Pests { get; }
    public ObservableCollection<PestIssue> Diseases { get; }
    public ICommand OpenIssueCommand { get; }

    private void LoadIssues()
    {
        Pests.Clear();
        Diseases.Clear();

        foreach (var pest in _pestService.GetPests())
        {
            Pests.Add(pest);
        }

        foreach (var disease in _pestService.GetDiseases())
        {
            Diseases.Add(disease);
        }
    }

}
