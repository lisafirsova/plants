using Plants.Models;
using Plants.Services;

namespace Plants.ViewModels;

public class IssueDetailViewModel : BaseViewModel
{
    private readonly IPestService _pestService;
    private PestIssue? _issue;

    public IssueDetailViewModel(IPestService pestService)
    {
        _pestService = pestService;
    }

    public PestIssue? Issue
    {
        get => _issue;
        set => SetProperty(ref _issue, value);
    }

    public void LoadIssue(int id)
    {
        Issue = _pestService.GetById(id);
    }
}
