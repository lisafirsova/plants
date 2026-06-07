using Microsoft.Extensions.DependencyInjection;

namespace Plants.Views;

public partial class IssueDetailPage : ContentPage, IQueryAttributable
{
    private readonly ViewModels.IssueDetailViewModel _viewModel;

    public IssueDetailPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<ViewModels.IssueDetailViewModel>();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out var id))
        {
            _viewModel.LoadIssue(id);
        }
    }
}
